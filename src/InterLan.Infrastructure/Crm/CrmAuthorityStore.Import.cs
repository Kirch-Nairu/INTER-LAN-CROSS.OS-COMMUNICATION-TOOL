using System.Text.Json;
using InterLan.Application.Crm;
using InterLan.Domain.Crm;

namespace InterLan.Infrastructure.Crm;

public sealed partial class CrmAuthorityStore : ICrmImportAuthorityLookup
{
    public async Task<bool> ProjectExistsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM crm_projects WHERE project_id = $id;";
        command.Parameters.AddWithValue("$id", projectId.ToString("D"));
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    public async Task<bool> FeatureExistsAsync(Guid projectId, Guid featureId, CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM crm_features WHERE project_id = $project AND feature_id = $feature;";
        command.Parameters.AddWithValue("$project", projectId.ToString("D"));
        command.Parameters.AddWithValue("$feature", featureId.ToString("D"));
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    public async Task<CrmImportResolvedStaff?> ResolveStaffAsync(string identity, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        await using var connection = database.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT user_id, role
            FROM users
            WHERE disabled_utc IS NULL
              AND (username = $identity COLLATE NOCASE OR display_name = $identity COLLATE NOCASE)
            ORDER BY CASE WHEN username = $identity COLLATE NOCASE THEN 0 ELSE 1 END
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$identity", identity.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var userId = Guid.Parse(reader.GetString(0));
        var legacyRole = reader.GetString(1);
        await reader.DisposeAsync();

        var roles = (await GetActiveRolesAsync(userId, cancellationToken)).ToList();
        if (string.Equals(legacyRole, "OWNER", StringComparison.Ordinal) && !roles.Contains(CrmStaffRole.TechnicalLead))
            roles.Add(CrmStaffRole.TechnicalLead);
        return new CrmImportResolvedStaff(userId, roles);
    }

    public async Task<Guid> ImportDraftAsync(
        CrmValidatedImport import,
        string sourceMethod,
        string payloadSha256,
        Guid actorUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(import);
        var method = sourceMethod.Trim().ToUpperInvariant();
        if (method is not ("FILE" or "PASTE" or "GUIDED_FORM"))
            throw new ArgumentOutOfRangeException(nameof(sourceMethod), "Unsupported CRM import source method.");
        if (payloadSha256.Length != 64 || payloadSha256.Any(character => !char.IsAsciiHexDigit(character)))
            throw new ArgumentException("Import payload fingerprint must be a SHA-256 hexadecimal value.", nameof(payloadSha256));

        await using var connection = database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        await using (var workPackage = connection.CreateCommand())
        {
            workPackage.Transaction = transaction;
            workPackage.CommandText =
                """
                INSERT INTO crm_work_packages (
                    work_package_id, project_id, feature_id, phase, layer,
                    technical_lead_user_id, state, source_repository, source_branch,
                    source_sha, source_verified, problem, solution, ci_required,
                    qa_required, screenshots_required, self_checks_json, version,
                    created_by_user_id, created_utc, updated_utc)
                VALUES ($id, $project, $feature, $phase, $layer,
                    $lead, 'DRAFT', $repository, $branch,
                    $sourceSha, $verified, $problem, $solution, $ci,
                    $qa, $screenshots, $selfChecks, 1,
                    $actor, $utc, $utc);
                """;
            workPackage.Parameters.AddWithValue("$id", import.WorkPackageId.Value);
            workPackage.Parameters.AddWithValue("$project", import.ProjectId.ToString("D"));
            workPackage.Parameters.AddWithValue("$feature", import.FeatureId.ToString("D"));
            workPackage.Parameters.AddWithValue("$phase", import.Phase);
            workPackage.Parameters.AddWithValue("$layer", CrmWorkflowVocabulary.Serialize(import.Layer));
            workPackage.Parameters.AddWithValue("$lead", import.TechnicalLeadUserId.ToString("D"));
            workPackage.Parameters.AddWithValue("$repository", import.Source.Repository.Value);
            workPackage.Parameters.AddWithValue("$branch", import.Source.Branch);
            workPackage.Parameters.AddWithValue("$sourceSha", import.Source.Sha.Value);
            workPackage.Parameters.AddWithValue("$verified", import.Source.Verified ? 1 : 0);
            workPackage.Parameters.AddWithValue("$problem", import.Problem);
            workPackage.Parameters.AddWithValue("$solution", import.Solution);
            workPackage.Parameters.AddWithValue("$ci", import.ValidationPolicy.CiRequired ? 1 : 0);
            workPackage.Parameters.AddWithValue("$qa", import.ValidationPolicy.QaRequired ? 1 : 0);
            workPackage.Parameters.AddWithValue("$screenshots", import.ValidationPolicy.ScreenshotsRequired ? 1 : 0);
            workPackage.Parameters.AddWithValue("$selfChecks", JsonSerializer.Serialize(import.ValidationPolicy.SelfChecks));
            workPackage.Parameters.AddWithValue("$actor", actorUserId.ToString("D"));
            workPackage.Parameters.AddWithValue("$utc", now.ToString("O"));
            await workPackage.ExecuteNonQueryAsync(cancellationToken);
        }

        for (var index = 0; index < import.Requirements.Count; index++)
            await InsertTextItemAsync(connection, transaction, "crm_requirements", "requirement_id", "requirement_text", import.WorkPackageId, import.Requirements[index].Id, import.Requirements[index].Text, index, cancellationToken);
        for (var index = 0; index < import.Deliverables.Count; index++)
            await InsertTextItemAsync(connection, transaction, "crm_deliverables", "deliverable_id", "deliverable_text", import.WorkPackageId, import.Deliverables[index].Id, import.Deliverables[index].Text, index, cancellationToken);

        for (var index = 0; index < import.AcceptanceCriteria.Count; index++)
        {
            var criterion = import.AcceptanceCriteria[index];
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO crm_acceptance_criteria (work_package_id, criterion_id, criterion_text, evidence_kinds_json, ordinal) VALUES ($id, $criterion, $text, $evidence, $ordinal);";
            command.Parameters.AddWithValue("$id", import.WorkPackageId.Value);
            command.Parameters.AddWithValue("$criterion", criterion.Id);
            command.Parameters.AddWithValue("$text", criterion.Text);
            command.Parameters.AddWithValue("$evidence", JsonSerializer.Serialize(criterion.EvidenceKinds));
            command.Parameters.AddWithValue("$ordinal", index);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var scope in import.Scope)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO crm_scope_rules (scope_rule_id, work_package_id, scope_kind, pattern) VALUES ($rule, $id, $kind, $pattern);";
            command.Parameters.AddWithValue("$rule", Guid.NewGuid().ToString("D"));
            command.Parameters.AddWithValue("$id", import.WorkPackageId.Value);
            command.Parameters.AddWithValue("$kind", scope.Kind == CrmScopeKind.Owned ? "OWNED" : "PROHIBITED");
            command.Parameters.AddWithValue("$pattern", scope.Pattern);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var assignment in import.Assignments)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO crm_assignments (assignment_id, work_package_id, user_id, crm_role, assigned_by_user_id, assigned_utc, revoked_utc) VALUES ($assignment, $id, $user, $role, $actor, $utc, NULL);";
            command.Parameters.AddWithValue("$assignment", Guid.NewGuid().ToString("D"));
            command.Parameters.AddWithValue("$id", import.WorkPackageId.Value);
            command.Parameters.AddWithValue("$user", assignment.UserId.ToString("D"));
            command.Parameters.AddWithValue("$role", CrmCapabilityPolicy.Serialize(assignment.Role));
            command.Parameters.AddWithValue("$actor", actorUserId.ToString("D"));
            command.Parameters.AddWithValue("$utc", now.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var receiptId = Guid.NewGuid();
        await using (var receipt = connection.CreateCommand())
        {
            receipt.Transaction = transaction;
            receipt.CommandText =
                """
                INSERT INTO crm_import_receipts (
                    import_receipt_id, importer_user_id, declared_work_package_id,
                    resulting_work_package_id, schema_version, source_method,
                    payload_sha256, validation_result, warnings_json, created_utc)
                VALUES ($receipt, $actor, $declared, $resulting, $schema, $method,
                    $hash, 'ACCEPTED_DRAFT', $warnings, $utc);
                """;
            receipt.Parameters.AddWithValue("$receipt", receiptId.ToString("D"));
            receipt.Parameters.AddWithValue("$actor", actorUserId.ToString("D"));
            receipt.Parameters.AddWithValue("$declared", import.WorkPackageId.Value);
            receipt.Parameters.AddWithValue("$resulting", import.WorkPackageId.Value);
            receipt.Parameters.AddWithValue("$schema", CrmImportParser.SupportedSchema);
            receipt.Parameters.AddWithValue("$method", method);
            receipt.Parameters.AddWithValue("$hash", payloadSha256.ToLowerInvariant());
            receipt.Parameters.AddWithValue("$warnings", JsonSerializer.Serialize(import.Warnings));
            receipt.Parameters.AddWithValue("$utc", now.ToString("O"));
            await receipt.ExecuteNonQueryAsync(cancellationToken);
        }

        await AppendAuditAsync(connection, transaction, actorUserId, import.WorkPackageId, null, "CRM_WORK_PACKAGE_IMPORTED_DRAFT", null, CrmWorkPackageState.Draft, new { receiptId, schema = CrmImportParser.SupportedSchema, method, payloadSha256 = payloadSha256.ToLowerInvariant(), import.Warnings }, now, cancellationToken);
        transaction.Commit();
        return receiptId;
    }

    private static async Task InsertTextItemAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        Microsoft.Data.Sqlite.SqliteTransaction transaction,
        string table,
        string keyColumn,
        string textColumn,
        CrmWorkPackageId id,
        string itemId,
        string text,
        int ordinal,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"INSERT INTO {table} (work_package_id, {keyColumn}, {textColumn}, ordinal) VALUES ($id, $itemId, $text, $ordinal);";
        command.Parameters.AddWithValue("$id", id.Value);
        command.Parameters.AddWithValue("$itemId", itemId);
        command.Parameters.AddWithValue("$text", text);
        command.Parameters.AddWithValue("$ordinal", ordinal);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
