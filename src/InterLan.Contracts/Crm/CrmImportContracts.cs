namespace InterLan.Contracts.Crm;

public sealed record CrmWorkPackageImportV1(
    string Schema,
    string WorkPackageId,
    Guid ProjectId,
    Guid FeatureId,
    string Phase,
    string Layer,
    string TechnicalLead,
    CrmImportSource Source,
    string Problem,
    string Solution,
    IReadOnlyList<CrmImportTextItem> Requirements,
    IReadOnlyList<CrmImportTextItem> Deliverables,
    IReadOnlyList<CrmImportCriterion> AcceptanceCriteria,
    IReadOnlyList<CrmImportScopeRule> Scope,
    IReadOnlyList<CrmImportAssignment> Assignments,
    CrmValidationPolicyContract Validation);

public sealed record CrmImportSource(
    string Repository,
    string Branch,
    string Sha,
    bool Verified);

public sealed record CrmImportTextItem(string Id, string Text);

public sealed record CrmImportCriterion(
    string Id,
    string Text,
    IReadOnlyList<string> EvidenceKinds);

public sealed record CrmImportScopeRule(string Kind, string Pattern);
public sealed record CrmImportAssignment(string Staff, string Role);

public sealed record CrmImportPreviewContract(
    bool Valid,
    string Schema,
    string? WorkPackageId,
    string? NormalizedPayloadSha256,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    string ResultingState);

public sealed record CrmImportDraftResultContract(
    Guid ImportReceiptId,
    string WorkPackageId,
    string State,
    string PayloadSha256,
    IReadOnlyList<string> Warnings);
