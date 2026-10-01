using System.Security.Cryptography;
using System.Text.Json;
using InterLan.Domain.Crm;

namespace InterLan.Application.Crm;

public static class CrmImportFingerprint
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static string Compute(CrmValidatedImport import)
    {
        ArgumentNullException.ThrowIfNull(import);

        var canonical = new
        {
            schema = CrmImportParser.SupportedSchema,
            workPackageId = import.WorkPackageId.Value,
            projectId = import.ProjectId,
            featureId = import.FeatureId,
            phase = import.Phase,
            layer = CrmWorkflowVocabulary.Serialize(import.Layer),
            technicalLeadUserId = import.TechnicalLeadUserId,
            source = new
            {
                repository = import.Source.Repository.Value,
                branch = import.Source.Branch,
                sha = import.Source.Sha.Value,
                verified = import.Source.Verified
            },
            problem = import.Problem,
            solution = import.Solution,
            requirements = import.Requirements.OrderBy(item => item.Id, StringComparer.Ordinal).Select(item => new { id = item.Id, text = item.Text }),
            deliverables = import.Deliverables.OrderBy(item => item.Id, StringComparer.Ordinal).Select(item => new { id = item.Id, text = item.Text }),
            acceptanceCriteria = import.AcceptanceCriteria.OrderBy(item => item.Id, StringComparer.Ordinal).Select(item => new
            {
                id = item.Id,
                text = item.Text,
                evidenceKinds = item.EvidenceKinds.OrderBy(value => value, StringComparer.Ordinal)
            }),
            scope = import.Scope.OrderBy(item => item.Pattern, StringComparer.Ordinal).ThenBy(item => item.Kind).Select(item => new
            {
                kind = item.Kind == CrmScopeKind.Owned ? "OWNED" : "PROHIBITED",
                pattern = item.Pattern
            }),
            assignments = import.Assignments.OrderBy(item => item.UserId).ThenBy(item => item.Role).Select(item => new
            {
                userId = item.UserId,
                role = CrmCapabilityPolicy.Serialize(item.Role)
            }),
            validation = new
            {
                ciRequired = import.ValidationPolicy.CiRequired,
                qaRequired = import.ValidationPolicy.QaRequired,
                screenshotsRequired = import.ValidationPolicy.ScreenshotsRequired,
                selfChecks = import.ValidationPolicy.SelfChecks.OrderBy(value => value, StringComparer.Ordinal)
            }
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(canonical, Options);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
