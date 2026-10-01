using InterLan.Application.Crm;
using InterLan.Contracts.Crm;
using InterLan.Domain.Crm;
using InterLan.Infrastructure;
using InterLan.Infrastructure.Crm;

namespace InterLan.Server.Crm;

public static partial class CrmEndpointMappings
{
    private static void MapImportEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/import/preview", async (HttpContext context, EnrollmentStore enrollment, CrmAuthorityStore authority, CancellationToken ct) =>
        {
            try
            {
                await CrmAuthorization.RequireCapabilityAsync(context, enrollment, authority, CrmCapability.ImportDraft, ct);
                var json = await ReadImportBodyAsync(context, ct);
                var parsed = new CrmImportParser().Parse(json);
                var validated = await new CrmImportValidator(authority).ValidateAsync(parsed, ct);
                var fingerprint = CrmImportFingerprint.Compute(validated);
                return Results.Ok(new CrmImportPreviewContract(true, CrmImportParser.SupportedSchema, validated.WorkPackageId.Value, fingerprint, [], validated.Warnings, "DRAFT"));
            }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException)
            {
                return Results.BadRequest(new CrmImportPreviewContract(false, CrmImportParser.SupportedSchema, null, null, [ex.Message], [], "DRAFT"));
            }
        });

        group.MapPost("/import", async (HttpContext context, EnrollmentStore enrollment, CrmAuthorityStore authority, CancellationToken ct) =>
        {
            try
            {
                var actor = await CrmAuthorization.RequireCapabilityAsync(context, enrollment, authority, CrmCapability.ImportDraft, ct);
                var json = await ReadImportBodyAsync(context, ct);
                var parsed = new CrmImportParser().Parse(json);
                var validated = await new CrmImportValidator(authority).ValidateAsync(parsed, ct);
                if (validated.TechnicalLeadUserId != actor.Principal.UserId)
                    throw new UnauthorizedAccessException("An imported work package must designate the authenticated importing Technical Lead.");

                var fingerprint = CrmImportFingerprint.Compute(validated);
                var sourceMethod = context.Request.Headers.TryGetValue("X-InterLan-Import-Method", out var method)
                    ? method.ToString()
                    : "PASTE";
                var receiptId = await authority.ImportDraftAsync(validated, sourceMethod, fingerprint, actor.Principal.UserId, DateTimeOffset.UtcNow, ct);
                return Results.Ok(new CrmImportDraftResultContract(receiptId, validated.WorkPackageId.Value, "DRAFT", fingerprint, validated.Warnings));
            }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
    }

    private static async Task<string> ReadImportBodyAsync(HttpContext context, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
        var json = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidDataException("CRM import request body is empty.");
        return json;
    }
}
