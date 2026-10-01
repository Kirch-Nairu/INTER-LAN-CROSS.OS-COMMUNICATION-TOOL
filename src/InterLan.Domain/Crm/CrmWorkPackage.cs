namespace InterLan.Domain.Crm;

public sealed class CrmWorkPackage
{
    private readonly List<CrmRequirement> requirements = [];
    private readonly List<CrmAcceptanceCriterion> acceptanceCriteria = [];
    private readonly List<CrmScopeRule> scopeRules = [];
    private readonly List<CrmAssignment> assignments = [];
    private readonly List<CrmCandidateSubmission> candidates = [];
    private readonly List<CrmCandidateValidation> validations = [];

    private CrmWorkPackage(
        CrmWorkPackageId id,
        Guid projectId,
        Guid featureId,
        string phase,
        CrmLayer layer,
        Guid technicalLeadUserId,
        CrmSourceIdentity source,
        string problem,
        string solution,
        CrmValidationPolicy validationPolicy,
        DateTimeOffset createdUtc)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("Project ID cannot be empty.", nameof(projectId));
        if (featureId == Guid.Empty)
            throw new ArgumentException("Feature ID cannot be empty.", nameof(featureId));
        if (technicalLeadUserId == Guid.Empty)
            throw new ArgumentException("Technical Lead user ID cannot be empty.", nameof(technicalLeadUserId));

        ArgumentException.ThrowIfNullOrWhiteSpace(phase);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(validationPolicy);

        Id = id;
        ProjectId = projectId;
        FeatureId = featureId;
        Phase = phase.Trim().ToUpperInvariant();
        Layer = layer;
        TechnicalLeadUserId = technicalLeadUserId;
        Source = source.Normalize();
        Problem = CrmRequirement.NormalizeText(problem, nameof(problem));
        Solution = CrmRequirement.NormalizeText(solution, nameof(solution));
        ValidationPolicy = validationPolicy;
        State = CrmWorkPackageState.Draft;
        Version = 1;
        CreatedUtc = createdUtc;
        UpdatedUtc = createdUtc;
    }

    public CrmWorkPackageId Id { get; }
    public Guid ProjectId { get; }
    public Guid FeatureId { get; }
    public string Phase { get; }
    public CrmLayer Layer { get; }
    public Guid TechnicalLeadUserId { get; }
    public CrmSourceIdentity Source { get; }
    public string Problem { get; }
    public string Solution { get; }
    public CrmValidationPolicy ValidationPolicy { get; }
    public CrmWorkPackageState State { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset CreatedUtc { get; }
    public DateTimeOffset UpdatedUtc { get; private set; }
    public CrmGitSha? AcceptedSha { get; private set; }

    public IReadOnlyList<CrmRequirement> Requirements => requirements;
    public IReadOnlyList<CrmAcceptanceCriterion> AcceptanceCriteria => acceptanceCriteria;
    public IReadOnlyList<CrmScopeRule> ScopeRules => scopeRules;
    public IReadOnlyList<CrmAssignment> Assignments => assignments;
    public IReadOnlyList<CrmCandidateSubmission> Candidates => candidates;
    public IReadOnlyList<CrmCandidateValidation> Validations => validations;
    public CrmCandidateSubmission? CurrentCandidate => candidates.Count == 0 ? null : candidates[^1];

    public static CrmWorkPackage Draft(
        CrmWorkPackageId id,
        Guid projectId,
        Guid featureId,
        string phase,
        CrmLayer layer,
        Guid technicalLeadUserId,
        CrmSourceIdentity source,
        string problem,
        string solution,
        CrmValidationPolicy validationPolicy,
        DateTimeOffset createdUtc) =>
        new(
            id,
            projectId,
            featureId,
            phase,
            layer,
            technicalLeadUserId,
            source,
            problem,
            solution,
            validationPolicy,
            createdUtc);

    public void AddRequirement(CrmRequirement requirement, DateTimeOffset now)
    {
        RequireDefinitionMutable();
        ArgumentNullException.ThrowIfNull(requirement);
        if (requirements.Any(existing => existing.Id == requirement.Id))
            throw new InvalidOperationException($"Requirement {requirement.Id} already exists.");

        requirements.Add(requirement);
        Touch(now);
    }

    public void AddAcceptanceCriterion(CrmAcceptanceCriterion criterion, DateTimeOffset now)
    {
        RequireDefinitionMutable();
        ArgumentNullException.ThrowIfNull(criterion);
        if (acceptanceCriteria.Any(existing => existing.Id == criterion.Id))
            throw new InvalidOperationException($"Acceptance criterion {criterion.Id} already exists.");

        acceptanceCriteria.Add(criterion);
        Touch(now);
    }

    public void AddScopeRule(CrmScopeRule rule, DateTimeOffset now)
    {
        RequireDefinitionMutable();
        ArgumentNullException.ThrowIfNull(rule);
        if (scopeRules.Any(existing =>
                existing.Kind == rule.Kind &&
                string.Equals(existing.Pattern, rule.Pattern, StringComparison.Ordinal)))
        {
            return;
        }

        var opposite = rule.Kind == CrmScopeKind.Owned ? CrmScopeKind.Prohibited : CrmScopeKind.Owned;
        if (scopeRules.Any(existing =>
                existing.Kind == opposite &&
                string.Equals(existing.Pattern, rule.Pattern, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Scope pattern '{rule.Pattern}' cannot be both owned and prohibited.");
        }

        scopeRules.Add(rule);
        Touch(now);
    }

    public void Assign(
        Guid userId,
        CrmStaffRole role,
        Guid assignedByUserId,
        DateTimeOffset now)
    {
        if (State is not (CrmWorkPackageState.Draft or CrmWorkPackageState.Authorized))
            throw new InvalidOperationException("Assignments can only change before implementation begins.");
        if (userId == Guid.Empty || assignedByUserId == Guid.Empty)
            throw new ArgumentException("Assignment user IDs cannot be empty.");

        CrmAssignmentPolicy.RequireCompatible(role, Layer);
        if (assignments.Any(existing => existing.UserId == userId && existing.Role == role))
            return;

        assignments.Add(new CrmAssignment(userId, role, now, assignedByUserId));
        Touch(now);
    }

    public void SubmitCandidate(
        CrmCandidateSubmission candidate,
        Guid actorUserId,
        CrmStaffRole actorRole,
        DateTimeOffset now)
    {
        if (State != CrmWorkPackageState.Implementing)
            throw new InvalidOperationException("Candidates may only be submitted while the work package is IMPLEMENTING.");

        CrmCapabilityPolicy.Require(actorRole, CrmCapability.SubmitCandidate);
        CrmAssignmentPolicy.RequireAssignedImplementationActor(actorUserId, actorRole, Layer, assignments);
        ArgumentNullException.ThrowIfNull(candidate);

        if (candidate.WorkPackageId != Id)
            throw new InvalidOperationException("Candidate work-package identity does not match the aggregate.");
        if (candidate.SourceSha != Source.Sha)
            throw new InvalidOperationException("Candidate source SHA does not match the authorized source SHA.");
        if (candidate.Sequence != candidates.Count + 1)
            throw new InvalidOperationException("Candidate sequence must be append-only and contiguous.");
        if (candidates.Any(existing =>
                existing.CandidateId == candidate.CandidateId ||
                existing.CandidateSha == candidate.CandidateSha))
        {
            throw new InvalidOperationException("Candidate identity or SHA has already been submitted.");
        }

        candidates.Add(candidate);
        State = CrmWorkPackageState.CandidateSubmitted;
        Touch(now);
    }

    public void RecordValidation(
        CrmCandidateValidation validation,
        CrmStaffRole actorRole,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(validation);
        var candidate = RequireCurrentCandidate(validation.CandidateId, validation.CandidateSha);

        switch (validation.Gate)
        {
            case CrmValidationGate.Ci:
                CrmCapabilityPolicy.Require(actorRole, CrmCapability.RecordCiValidation);
                if (State != CrmWorkPackageState.CiRunning)
                    throw new InvalidOperationException("CI result requires the work package to be CI_RUNNING.");
                if (validation.Result == CrmValidationResult.Running)
                    throw new InvalidOperationException("A recorded CI result must be terminal PASS or FAIL.");
                validations.Add(validation);
                State = validation.Result == CrmValidationResult.Passed
                    ? CrmWorkPackageState.CiPassed
                    : CrmWorkPackageState.CiFailed;
                break;

            case CrmValidationGate.Qa:
                CrmCapabilityPolicy.Require(actorRole, CrmCapability.RecordQaValidation);
                if (State != CrmWorkPackageState.QaRunning)
                    throw new InvalidOperationException("QA result requires the work package to be QA_RUNNING.");
                if (validation.Result == CrmValidationResult.Running)
                    throw new InvalidOperationException("A recorded QA result must be terminal PASS or FAIL.");
                RequireGatePass(candidate, CrmValidationGate.Ci);
                validations.Add(validation);
                State = validation.Result == CrmValidationResult.Passed
                    ? CrmWorkPackageState.QaPassed
                    : CrmWorkPackageState.QaFailed;
                break;

            default:
                throw new InvalidOperationException("Lead disposition is recorded by the explicit lead transition path.");
        }

        Touch(now);
    }

    public void RecordAcceptedSha(
        Guid candidateId,
        CrmGitSha candidateSha,
        CrmStaffRole actorRole,
        DateTimeOffset now)
    {
        CrmCapabilityPolicy.Require(actorRole, CrmCapability.LeadDisposition);
        if (State != CrmWorkPackageState.LeadAccepted)
            throw new InvalidOperationException("Accepted SHA may only be recorded after LEAD_ACCEPTED.");

        var candidate = RequireCurrentCandidate(candidateId, candidateSha);
        if (ValidationPolicy.CiRequired)
            RequireGatePass(candidate, CrmValidationGate.Ci);
        if (ValidationPolicy.QaRequired)
            RequireGatePass(candidate, CrmValidationGate.Qa);

        AcceptedSha = candidate.CandidateSha;
        Touch(now);
    }

    public void Transition(
        CrmWorkPackageState next,
        Guid actorUserId,
        CrmStaffRole actorRole,
        DateTimeOffset now)
    {
        var capability = CrmTransitionPolicy.RequiredCapability(State, next);
        CrmCapabilityPolicy.Require(actorRole, capability);

        if (next == CrmWorkPackageState.Authorized)
        {
            if (requirements.Count == 0)
                throw new InvalidOperationException("A work package cannot be authorized without requirements.");
            if (acceptanceCriteria.Count == 0)
                throw new InvalidOperationException("A work package cannot be authorized without acceptance criteria.");
            if (assignments.Count == 0)
                throw new InvalidOperationException("A work package cannot be authorized without an assignment.");
        }

        if (next == CrmWorkPackageState.Implementing)
        {
            CrmAssignmentPolicy.RequireAssignedImplementationActor(
                actorUserId,
                actorRole,
                Layer,
                assignments);
        }

        if (next == CrmWorkPackageState.QaPending && ValidationPolicy.CiRequired)
            RequireGatePass(RequireCurrentCandidate(), CrmValidationGate.Ci);

        if (next == CrmWorkPackageState.LeadReview && ValidationPolicy.QaRequired)
            RequireGatePass(RequireCurrentCandidate(), CrmValidationGate.Qa);

        if (next == CrmWorkPackageState.LeadAccepted)
        {
            var candidate = RequireCurrentCandidate();
            if (ValidationPolicy.CiRequired)
                RequireGatePass(candidate, CrmValidationGate.Ci);
            if (ValidationPolicy.QaRequired)
                RequireGatePass(candidate, CrmValidationGate.Qa);
        }

        if (next == CrmWorkPackageState.IntegrationAuthorized && AcceptedSha is null)
            throw new InvalidOperationException("Integration cannot be authorized before an explicit accepted SHA is recorded.");

        State = next;
        Touch(now);
    }

    private CrmCandidateSubmission RequireCurrentCandidate(
        Guid? candidateId = null,
        CrmGitSha? candidateSha = null)
    {
        var current = CurrentCandidate
            ?? throw new InvalidOperationException("The work package has no submitted candidate.");

        if (candidateId is { } id && current.CandidateId != id)
            throw new InvalidOperationException("Validation belongs to a non-current candidate.");
        if (candidateSha is { } sha && current.CandidateSha != sha)
            throw new InvalidOperationException("Validation SHA does not match the current candidate SHA.");

        return current;
    }

    private void RequireGatePass(CrmCandidateSubmission candidate, CrmValidationGate gate)
    {
        var passed = validations.Any(validation =>
            validation.CandidateId == candidate.CandidateId &&
            validation.CandidateSha == candidate.CandidateSha &&
            validation.Gate == gate &&
            validation.Result == CrmValidationResult.Passed);

        if (!passed)
            throw new InvalidOperationException($"{gate} PASS is required for current candidate {candidate.CandidateSha}.");
    }

    private void RequireDefinitionMutable()
    {
        if (State != CrmWorkPackageState.Draft)
            throw new InvalidOperationException("Requirements, acceptance criteria and scope are immutable after authorization.");
    }

    private void Touch(DateTimeOffset now)
    {
        Version++;
        UpdatedUtc = now;
    }
}
