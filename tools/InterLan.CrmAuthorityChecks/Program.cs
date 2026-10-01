using InterLan.Domain.Crm;

var failures = new List<string>();

void Check(bool condition, string name)
{
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")} {name}");
    if (!condition)
        failures.Add(name);
}

bool Throws<T>(Action action) where T : Exception
{
    try
    {
        action();
        return false;
    }
    catch (T)
    {
        return true;
    }
}

var lead = Guid.NewGuid();
var developer = Guid.NewGuid();
var ci = Guid.NewGuid();
var qa = Guid.NewGuid();
var sourceSha = new CrmGitSha(new string('a', 40));
var candidateSha = new CrmGitSha(new string('b', 40));
var source = new CrmSourceIdentity(
    new CrmRepositoryIdentity("Kirch-Nairu/example"),
    "KIRCH-EXAMPLE-A01-B",
    sourceSha,
    Verified: true);

var package = CrmWorkPackage.Draft(
    new CrmWorkPackageId("EX-A01-B"),
    Guid.NewGuid(),
    Guid.NewGuid(),
    "A01",
    CrmLayer.Backend,
    lead,
    source,
    "Need authoritative work-package state.",
    "Implement the bounded authority core.",
    CrmValidationPolicy.Create(true, true, false, ["build", "checks"]),
    DateTimeOffset.UtcNow);

Check(
    Throws<InvalidOperationException>(() =>
        package.Transition(
            CrmWorkPackageState.Authorized,
            lead,
            CrmStaffRole.TechnicalLead,
            DateTimeOffset.UtcNow)),
    "authorization fails without requirements criteria and assignment");

package.AddRequirement(new CrmRequirement("REQ-01", "Persist authoritative state."), DateTimeOffset.UtcNow);
package.AddAcceptanceCriterion(
    new CrmAcceptanceCriterion("AC-01", "Exact candidate validation is enforced.", ["RUNTIME_CHECK"]),
    DateTimeOffset.UtcNow);
package.AddScopeRule(new CrmScopeRule(CrmScopeKind.Owned, "src/InterLan.Domain/Crm/**"), DateTimeOffset.UtcNow);

Check(
    Throws<InvalidOperationException>(() =>
        package.Assign(
            developer,
            CrmStaffRole.FrontendDeveloper,
            lead,
            DateTimeOffset.UtcNow)),
    "incompatible role-layer assignment fails closed");

package.Assign(developer, CrmStaffRole.BackendDeveloper, lead, DateTimeOffset.UtcNow);

Check(
    Throws<UnauthorizedAccessException>(() =>
        package.Transition(
            CrmWorkPackageState.Authorized,
            developer,
            CrmStaffRole.BackendDeveloper,
            DateTimeOffset.UtcNow)),
    "developer cannot authorize work package");

package.Transition(
    CrmWorkPackageState.Authorized,
    lead,
    CrmStaffRole.TechnicalLead,
    DateTimeOffset.UtcNow);

Check(
    Throws<InvalidOperationException>(() =>
        package.Transition(
            CrmWorkPackageState.CandidateSubmitted,
            developer,
            CrmStaffRole.BackendDeveloper,
            DateTimeOffset.UtcNow)),
    "illegal state jump fails closed");

package.Transition(
    CrmWorkPackageState.Implementing,
    developer,
    CrmStaffRole.BackendDeveloper,
    DateTimeOffset.UtcNow);

var candidate = new CrmCandidateSubmission(
    Guid.NewGuid(),
    package.Id,
    1,
    "KIRCH-EXAMPLE-A01-B",
    sourceSha,
    candidateSha,
    4,
    "aaaa...bbbb",
    [new CrmChangedFileEvidence("src/InterLan.Domain/Crm/CrmWorkPackage.cs", "MODIFIED", 12, 2)],
    ["build PASS"],
    developer,
    DateTimeOffset.UtcNow);

package.SubmitCandidate(
    candidate,
    developer,
    CrmStaffRole.BackendDeveloper,
    DateTimeOffset.UtcNow);
package.Transition(CrmWorkPackageState.CiPending, ci, CrmStaffRole.CiCdValidator, DateTimeOffset.UtcNow);
package.Transition(CrmWorkPackageState.CiRunning, ci, CrmStaffRole.CiCdValidator, DateTimeOffset.UtcNow);

Check(
    Throws<InvalidOperationException>(() =>
        package.RecordValidation(
            new CrmCandidateValidation(
                Guid.NewGuid(),
                candidate.CandidateId,
                new CrmGitSha(new string('c', 40)),
                CrmValidationGate.Ci,
                CrmValidationResult.Passed,
                ci,
                DateTimeOffset.UtcNow,
                "wrong SHA"),
            CrmStaffRole.CiCdValidator,
            DateTimeOffset.UtcNow)),
    "validation with mismatched candidate SHA fails closed");

package.RecordValidation(
    new CrmCandidateValidation(
        Guid.NewGuid(),
        candidate.CandidateId,
        candidate.CandidateSha,
        CrmValidationGate.Ci,
        CrmValidationResult.Passed,
        ci,
        DateTimeOffset.UtcNow,
        "CI PASS"),
    CrmStaffRole.CiCdValidator,
    DateTimeOffset.UtcNow);

package.Transition(CrmWorkPackageState.QaPending, qa, CrmStaffRole.QualityAssurance, DateTimeOffset.UtcNow);
package.Transition(CrmWorkPackageState.QaRunning, qa, CrmStaffRole.QualityAssurance, DateTimeOffset.UtcNow);
package.RecordValidation(
    new CrmCandidateValidation(
        Guid.NewGuid(),
        candidate.CandidateId,
        candidate.CandidateSha,
        CrmValidationGate.Qa,
        CrmValidationResult.Passed,
        qa,
        DateTimeOffset.UtcNow,
        "QA PASS"),
    CrmStaffRole.QualityAssurance,
    DateTimeOffset.UtcNow);

package.Transition(CrmWorkPackageState.LeadReview, lead, CrmStaffRole.TechnicalLead, DateTimeOffset.UtcNow);
package.Transition(CrmWorkPackageState.LeadAccepted, lead, CrmStaffRole.TechnicalLead, DateTimeOffset.UtcNow);

Check(
    Throws<InvalidOperationException>(() =>
        package.Transition(
            CrmWorkPackageState.IntegrationAuthorized,
            lead,
            CrmStaffRole.TechnicalLead,
            DateTimeOffset.UtcNow)),
    "integration authorization requires explicit accepted SHA");

package.RecordAcceptedSha(
    candidate.CandidateId,
    candidate.CandidateSha,
    CrmStaffRole.TechnicalLead,
    DateTimeOffset.UtcNow);
package.Transition(
    CrmWorkPackageState.IntegrationAuthorized,
    lead,
    CrmStaffRole.TechnicalLead,
    DateTimeOffset.UtcNow);

Check(
    package.AcceptedSha == candidateSha &&
    package.State == CrmWorkPackageState.IntegrationAuthorized,
    "exact candidate can reach integration authorization only after CI QA and lead acceptance");

if (failures.Count > 0)
{
    Console.Error.WriteLine($"CRM authority checks failed: {string.Join(", ", failures)}");
    return 1;
}

Console.WriteLine("INTER-LAN CRM AUTHORITY DOMAIN CHECKS: PASS");
return 0;
