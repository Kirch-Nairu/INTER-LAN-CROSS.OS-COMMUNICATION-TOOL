namespace InterLan.Contracts.Crm;

public sealed record CrmProjectRequest(string Key, string Name);
public sealed record CrmFeatureRequest(Guid ProjectId, string Key, string Name);
public sealed record CrmRoleGrantRequest(Guid UserId, string Role);

public sealed record CrmDraftWorkPackageRequest(
    string WorkPackageId,
    Guid ProjectId,
    Guid FeatureId,
    string Phase,
    string Layer,
    Guid TechnicalLeadUserId,
    CrmSourceContract Source,
    string Problem,
    string Solution,
    CrmValidationPolicyContract ValidationPolicy);

public sealed record CrmSourceContract(
    string Repository,
    string Branch,
    string Sha,
    bool Verified);

public sealed record CrmValidationPolicyContract(
    bool CiRequired,
    bool QaRequired,
    bool ScreenshotsRequired,
    IReadOnlyList<string> SelfChecks);

public sealed record CrmRequirementRequest(string Id, string Text);
public sealed record CrmDeliverableRequest(string Id, string Text);
public sealed record CrmAcceptanceCriterionRequest(string Id, string Text, IReadOnlyList<string> EvidenceKinds);
public sealed record CrmScopeRuleRequest(string Kind, string Pattern);
public sealed record CrmAssignmentRequest(Guid UserId, string Role);
public sealed record CrmTransitionRequest(long ExpectedVersion, string NextState);

public sealed record CrmCandidateSubmissionRequest(
    long ExpectedVersion,
    Guid CandidateId,
    int Sequence,
    string Branch,
    string SourceSha,
    string CandidateSha,
    int CommitCount,
    string? CommitRange,
    IReadOnlyList<CrmChangedFileContract> ChangedFiles,
    IReadOnlyList<string> SelfValidation,
    string? KnownLimitations,
    string? NotesToValidator);

public sealed record CrmChangedFileContract(
    string Path,
    string ChangeType,
    int AddedLines,
    int DeletedLines);

public sealed record CrmValidationResultRequest(
    long ExpectedVersion,
    Guid ValidationId,
    Guid CandidateId,
    string CandidateSha,
    string Result,
    string? Summary);

public sealed record CrmAcceptanceRequest(
    long ExpectedVersion,
    Guid CandidateId,
    string CandidateSha);

public sealed record CrmWorkPackageContract(
    string WorkPackageId,
    Guid ProjectId,
    Guid FeatureId,
    string Phase,
    string Layer,
    Guid TechnicalLeadUserId,
    string State,
    CrmSourceContract Source,
    string Problem,
    string Solution,
    CrmValidationPolicyContract ValidationPolicy,
    long Version,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc,
    string? AcceptedSha);

public sealed record CrmCandidateContract(
    Guid CandidateId,
    string WorkPackageId,
    int Sequence,
    string Branch,
    string SourceSha,
    string CandidateSha,
    int CommitCount,
    Guid SubmittedByUserId,
    DateTimeOffset SubmittedUtc);

public sealed record CrmAuditEventContract(
    Guid AuditEventId,
    Guid ActorUserId,
    Guid? CandidateId,
    string EventType,
    string? BeforeState,
    string? AfterState,
    string PayloadJson,
    DateTimeOffset CreatedUtc);
