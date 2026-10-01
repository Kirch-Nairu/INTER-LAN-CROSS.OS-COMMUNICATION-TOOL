using InterLan.Domain.Crm;
using InterLan.Infrastructure;
using InterLan.Infrastructure.Crm;

var failures = new List<string>();
void Check(bool condition, string name)
{
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")} {name}");
    if (!condition) failures.Add(name);
}

bool Throws<T>(Action action) where T : Exception
{
    try { action(); return false; }
    catch (T) { return true; }
}

async Task<bool> ThrowsAsync<T>(Func<Task> action) where T : Exception
{
    try { await action(); return false; }
    catch (T) { return true; }
}

var lead = Guid.NewGuid();
var developer = Guid.NewGuid();
var ci = Guid.NewGuid();
var qa = Guid.NewGuid();
var sourceSha = new CrmGitSha(new string('a', 40));
var candidateSha = new CrmGitSha(new string('b', 40));
var source = new CrmSourceIdentity(new CrmRepositoryIdentity("Kirch-Nairu/example"), "KIRCH-EXAMPLE-A01-B", sourceSha, true);

var package = CrmWorkPackage.Draft(
    new CrmWorkPackageId("EX-A01-B"), Guid.NewGuid(), Guid.NewGuid(), "A01", CrmLayer.Backend,
    lead, source, "Need authoritative work-package state.", "Implement the bounded authority core.",
    CrmValidationPolicy.Create(true, true, false, ["build", "checks"]), DateTimeOffset.UtcNow);

Check(Throws<InvalidOperationException>(() => package.Transition(CrmWorkPackageState.Authorized, lead, CrmStaffRole.TechnicalLead, DateTimeOffset.UtcNow)),
    "authorization fails without requirements deliverables criteria and assignment");
package.AddRequirement(new CrmRequirement("REQ-01", "Persist authoritative state."), DateTimeOffset.UtcNow);
package.AddDeliverable(new CrmDeliverable("DEL-01", "Persist the authority core."), DateTimeOffset.UtcNow);
package.AddAcceptanceCriterion(new CrmAcceptanceCriterion("AC-01", "Exact candidate validation is enforced.", ["RUNTIME_CHECK"]), DateTimeOffset.UtcNow);
package.AddScopeRule(new CrmScopeRule(CrmScopeKind.Owned, "src/InterLan.Domain/Crm/**"), DateTimeOffset.UtcNow);
Check(Throws<InvalidOperationException>(() => package.Assign(developer, CrmStaffRole.FrontendDeveloper, lead, DateTimeOffset.UtcNow)),
    "incompatible role-layer assignment fails closed");
package.Assign(developer, CrmStaffRole.BackendDeveloper, lead, DateTimeOffset.UtcNow);
Check(Throws<UnauthorizedAccessException>(() => package.Transition(CrmWorkPackageState.Authorized, developer, CrmStaffRole.BackendDeveloper, DateTimeOffset.UtcNow)),
    "developer cannot authorize work package");
package.Transition(CrmWorkPackageState.Authorized, lead, CrmStaffRole.TechnicalLead, DateTimeOffset.UtcNow);
Check(Throws<InvalidOperationException>(() => package.Transition(CrmWorkPackageState.CandidateSubmitted, developer, CrmStaffRole.BackendDeveloper, DateTimeOffset.UtcNow)),
    "illegal state jump fails closed");
package.Transition(CrmWorkPackageState.Implementing, developer, CrmStaffRole.BackendDeveloper, DateTimeOffset.UtcNow);

var candidate = new CrmCandidateSubmission(
    Guid.NewGuid(), package.Id, 1, "KIRCH-EXAMPLE-A01-B", sourceSha, candidateSha, 4, "aaaa...bbbb",
    [new CrmChangedFileEvidence("src/InterLan.Domain/Crm/CrmWorkPackage.cs", "MODIFIED", 12, 2)],
    ["build PASS"], developer, DateTimeOffset.UtcNow);
package.SubmitCandidate(candidate, developer, CrmStaffRole.BackendDeveloper, DateTimeOffset.UtcNow);
package.Transition(CrmWorkPackageState.CiPending, ci, CrmStaffRole.CiCdValidator, DateTimeOffset.UtcNow);
package.Transition(CrmWorkPackageState.CiRunning, ci, CrmStaffRole.CiCdValidator, DateTimeOffset.UtcNow);
Check(Throws<InvalidOperationException>(() => package.RecordValidation(
        new CrmCandidateValidation(Guid.NewGuid(), candidate.CandidateId, new CrmGitSha(new string('c', 40)), CrmValidationGate.Ci, CrmValidationResult.Passed, ci, DateTimeOffset.UtcNow, "wrong SHA"),
        CrmStaffRole.CiCdValidator, DateTimeOffset.UtcNow)),
    "validation with mismatched candidate SHA fails closed");
package.RecordValidation(new CrmCandidateValidation(Guid.NewGuid(), candidate.CandidateId, candidate.CandidateSha, CrmValidationGate.Ci, CrmValidationResult.Passed, ci, DateTimeOffset.UtcNow, "CI PASS"), CrmStaffRole.CiCdValidator, DateTimeOffset.UtcNow);
package.Transition(CrmWorkPackageState.QaPending, qa, CrmStaffRole.QualityAssurance, DateTimeOffset.UtcNow);
package.Transition(CrmWorkPackageState.QaRunning, qa, CrmStaffRole.QualityAssurance, DateTimeOffset.UtcNow);
package.RecordValidation(new CrmCandidateValidation(Guid.NewGuid(), candidate.CandidateId, candidate.CandidateSha, CrmValidationGate.Qa, CrmValidationResult.Passed, qa, DateTimeOffset.UtcNow, "QA PASS"), CrmStaffRole.QualityAssurance, DateTimeOffset.UtcNow);
package.Transition(CrmWorkPackageState.LeadReview, lead, CrmStaffRole.TechnicalLead, DateTimeOffset.UtcNow);
package.Transition(CrmWorkPackageState.LeadAccepted, lead, CrmStaffRole.TechnicalLead, DateTimeOffset.UtcNow);
Check(Throws<InvalidOperationException>(() => package.Transition(CrmWorkPackageState.IntegrationAuthorized, lead, CrmStaffRole.TechnicalLead, DateTimeOffset.UtcNow)),
    "integration authorization requires explicit accepted SHA");
package.RecordAcceptedSha(candidate.CandidateId, candidate.CandidateSha, CrmStaffRole.TechnicalLead, DateTimeOffset.UtcNow);
package.Transition(CrmWorkPackageState.IntegrationAuthorized, lead, CrmStaffRole.TechnicalLead, DateTimeOffset.UtcNow);
Check(package.AcceptedSha == candidateSha && package.State == CrmWorkPackageState.IntegrationAuthorized,
    "exact candidate reaches integration authorization only after CI QA and lead acceptance");

var root = Path.Combine(Path.GetTempPath(), "interlan-crm-authority-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var databasePath = Path.Combine(root, "crm.db");
    var database = new SqliteDatabase(databasePath);
    await database.InitializeAsync();

    await using (var connection = database.OpenConnection())
    {
        foreach (var user in new[]
        {
            (lead, "lead", "Lead", "OWNER"),
            (developer, "developer", "Developer", "MEMBER"),
            (ci, "ci", "CI", "MEMBER"),
            (qa, "qa", "QA", "MEMBER")
        })
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO users (user_id, username, display_name, role, created_utc) VALUES ($id, $username, $display, $role, $utc);";
            insert.Parameters.AddWithValue("$id", user.Item1.ToString("D"));
            insert.Parameters.AddWithValue("$username", user.Item2);
            insert.Parameters.AddWithValue("$display", user.Item3);
            insert.Parameters.AddWithValue("$role", user.Item4);
            insert.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToString("O"));
            await insert.ExecuteNonQueryAsync();
        }
    }

    var store = new CrmAuthorityStore(database);
    var projectId = Guid.NewGuid();
    var featureId = Guid.NewGuid();
    var persistedId = new CrmWorkPackageId("DB-A01-B");
    var now = DateTimeOffset.UtcNow;
    await store.CreateProjectAsync(projectId, "DB", "Database Project", lead, now);
    await store.CreateFeatureAsync(featureId, projectId, "A01", "Authority Feature", lead, now);
    await store.GrantRoleAsync(developer, CrmStaffRole.BackendDeveloper, lead, now);
    await store.GrantRoleAsync(ci, CrmStaffRole.CiCdValidator, lead, now);
    await store.GrantRoleAsync(qa, CrmStaffRole.QualityAssurance, lead, now);
    await store.CreateDraftWorkPackageAsync(persistedId, projectId, featureId, "A01", CrmLayer.Backend, lead, source,
        "Persist authority.", "Use transactional SQLite authority.", CrmValidationPolicy.Create(true, true, false, ["build"]), lead, now);
    await store.AddRequirementAsync(persistedId, new CrmRequirement("REQ-01", "Persist authority."), lead, now);
    await store.AddDeliverableAsync(persistedId, new CrmDeliverable("DEL-01", "Durable authority."), lead, now);
    await store.AddAcceptanceCriterionAsync(persistedId, new CrmAcceptanceCriterion("AC-01", "Restart preserves state.", ["RUNTIME_CHECK"]), lead, now);
    await store.AddScopeRuleAsync(persistedId, new CrmScopeRule(CrmScopeKind.Owned, "src/InterLan.Infrastructure/Crm/**"), lead, now);
    await store.AssignAsync(persistedId, developer, CrmStaffRole.BackendDeveloper, lead, now);

    var draft = await store.GetWorkPackageAsync(persistedId) ?? throw new InvalidOperationException("Draft missing.");
    var version = await store.TransitionAsync(persistedId, draft.Version, CrmWorkPackageState.Authorized, lead, CrmStaffRole.TechnicalLead, now);
    var implementingVersion = await store.TransitionAsync(persistedId, version, CrmWorkPackageState.Implementing, developer, CrmStaffRole.BackendDeveloper, now);
    Check(await ThrowsAsync<InvalidOperationException>(() => store.TransitionAsync(persistedId, version, CrmWorkPackageState.Implementing, developer, CrmStaffRole.BackendDeveloper, now)),
        "stale expected version rejects competing authority transition");

    var persistedCandidate = new CrmCandidateSubmission(
        Guid.NewGuid(), persistedId, 1, source.Branch, sourceSha, candidateSha, 2, null,
        [new CrmChangedFileEvidence("src/example.cs", "MODIFIED", 4, 1)], ["focused checks"], developer, now);
    var candidateVersion = await store.SubmitCandidateAsync(persistedCandidate, implementingVersion, developer, CrmStaffRole.BackendDeveloper, now);

    var reopenedDatabase = new SqliteDatabase(databasePath);
    await reopenedDatabase.InitializeAsync();
    var reopened = new CrmAuthorityStore(reopenedDatabase);
    var afterRestart = await reopened.GetWorkPackageAsync(persistedId) ?? throw new InvalidOperationException("Reopened work package missing.");
    var history = await reopened.ListCandidatesAsync(persistedId);
    var audit = await reopened.ListAuditAsync(persistedId);
    Check(afterRestart.State == CrmWorkPackageState.CandidateSubmitted && afterRestart.Version == candidateVersion,
        "work-package authority state survives database reopen");
    Check(history.Count == 1 && history[0].CandidateSha == candidateSha,
        "immutable candidate evidence survives database reopen");
    Check(audit.Count >= 7 && audit.Any(item => item.EventType == "CRM_CANDIDATE_SUBMITTED"),
        "append-oriented authority audit survives database reopen");
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"CRM authority checks failed: {string.Join(", ", failures)}");
    return 1;
}

Console.WriteLine("INTER-LAN CRM AUTHORITY CHECKS: PASS");
return 0;
