using System.Text.Json;
using InterLan.Application.Crm;
using InterLan.Contracts.Crm;
using InterLan.Domain.Crm;

var failures = new List<string>();
void Check(bool condition, string name)
{
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")} {name}");
    if (!condition) failures.Add(name);
}

async Task<bool> ThrowsAsync<T>(Func<Task> action) where T : Exception
{
    try { await action(); return false; }
    catch (T) { return true; }
}

bool Throws<T>(Action action) where T : Exception
{
    try { action(); return false; }
    catch (T) { return true; }
}

var projectId = Guid.NewGuid();
var featureId = Guid.NewGuid();
var leadId = Guid.NewGuid();
var developerId = Guid.NewGuid();
var lookup = new FakeLookup(projectId, featureId, leadId, developerId);
var validator = new CrmImportValidator(lookup);
var parser = new CrmImportParser();

var payload = new CrmWorkPackageImportV1(
    CrmImportParser.SupportedSchema,
    "EX-A01-B",
    projectId,
    featureId,
    "A01",
    "BACKEND",
    "lead",
    new CrmImportSource("Kirch-Nairu/example", "KIRCH-EX-A01-B", new string('a', 40), true),
    "Need bounded authority.",
    "Implement exact authority state.",
    [new CrmImportTextItem("REQ-01", "Persist state.")],
    [new CrmImportTextItem("DEL-01", "Ship authority store.")],
    [new CrmImportCriterion("AC-01", "Exact SHA gates pass.", ["RUNTIME_CHECK"])],
    [new CrmImportScopeRule("OWNED", "src/InterLan.Domain/Crm/**")],
    [new CrmImportAssignment("developer", "BACKEND_DEVELOPER")],
    new CrmValidationPolicyContract(true, true, false, ["build", "checks"]));

var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
var parsed = parser.Parse(json);
var validated = await validator.ValidateAsync(parsed);
Check(validated.WorkPackageId.Value == "EX-A01-B", "valid declarative import resolves authority identities");
Check(CrmImportFingerprint.Compute(validated) == CrmImportFingerprint.Compute(validated), "normalized import fingerprint is deterministic");

var forbidden = json.Insert(1, "\"command\":\"echo unsafe\",");
Check(Throws<InvalidDataException>(() => parser.Parse(forbidden)), "executable command field is rejected before deserialization");

var unknown = json.Insert(1, "\"unexpected\":true,");
Check(Throws<InvalidDataException>(() => parser.Parse(unknown)), "unknown schema field fails closed");

var unsupported = json.Replace(CrmImportParser.SupportedSchema, "interlan.work-package.v2", StringComparison.Ordinal);
Check(Throws<InvalidDataException>(() => parser.Parse(unsupported)), "unsupported schema version is rejected");

var badSha = payload with { Source = payload.Source with { Sha = "not-a-sha" } };
Check(await ThrowsAsync<ArgumentException>(() => validator.ValidateAsync(badSha)), "invalid source SHA is rejected semantically");

var collision = payload with
{
    Scope =
    [
        new CrmImportScopeRule("OWNED", "src/example/**"),
        new CrmImportScopeRule("PROHIBITED", "src/example/**")
    ]
};
Check(await ThrowsAsync<InvalidDataException>(() => validator.ValidateAsync(collision)), "owned and prohibited scope collision is rejected");

var unverified = payload with { Source = payload.Source with { Verified = false } };
var unverifiedValidated = await validator.ValidateAsync(unverified);
Check(unverifiedValidated.Warnings.Any(warning => warning.Contains("unverified", StringComparison.OrdinalIgnoreCase)), "explicit unverified source remains DRAFT with warning");

var wrongRole = payload with { Assignments = [new CrmImportAssignment("developer", "FRONTEND_DEVELOPER")] };
Check(await ThrowsAsync<InvalidOperationException>(() => validator.ValidateAsync(wrongRole)), "layer-incompatible assignment is rejected");

if (failures.Count > 0)
{
    Console.Error.WriteLine($"CRM import checks failed: {string.Join(", ", failures)}");
    return 1;
}

Console.WriteLine("INTER-LAN CRM IMPORT CHECKS: PASS");
return 0;

sealed class FakeLookup(Guid projectId, Guid featureId, Guid leadId, Guid developerId) : ICrmImportAuthorityLookup
{
    public Task<bool> ProjectExistsAsync(Guid candidate, CancellationToken cancellationToken = default) =>
        Task.FromResult(candidate == projectId);

    public Task<bool> FeatureExistsAsync(Guid project, Guid feature, CancellationToken cancellationToken = default) =>
        Task.FromResult(project == projectId && feature == featureId);

    public Task<CrmImportResolvedStaff?> ResolveStaffAsync(string identity, CancellationToken cancellationToken = default) =>
        Task.FromResult<CrmImportResolvedStaff?>(identity switch
        {
            "lead" => new CrmImportResolvedStaff(leadId, [CrmStaffRole.TechnicalLead]),
            "developer" => new CrmImportResolvedStaff(developerId, [CrmStaffRole.BackendDeveloper]),
            _ => null
        });
}
