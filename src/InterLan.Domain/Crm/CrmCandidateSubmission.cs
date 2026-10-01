namespace InterLan.Domain.Crm;

public enum CrmValidationGate
{
    Ci,
    Qa,
    Lead
}

public enum CrmValidationResult
{
    Running,
    Passed,
    Failed
}

public sealed record CrmChangedFileEvidence
{
    public CrmChangedFileEvidence(
        string path,
        string changeType,
        int addedLines,
        int deletedLines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(changeType);
        if (addedLines < 0 || deletedLines < 0)
            throw new ArgumentOutOfRangeException(nameof(addedLines), "Changed-line counts cannot be negative.");

        Path = path.Trim().Replace('\\', '/');
        ChangeType = changeType.Trim().ToUpperInvariant();
        AddedLines = addedLines;
        DeletedLines = deletedLines;
    }

    public string Path { get; }
    public string ChangeType { get; }
    public int AddedLines { get; }
    public int DeletedLines { get; }
}

public sealed record CrmCandidateSubmission
{
    public CrmCandidateSubmission(
        Guid candidateId,
        CrmWorkPackageId workPackageId,
        int sequence,
        string branch,
        CrmGitSha sourceSha,
        CrmGitSha candidateSha,
        int commitCount,
        string? commitRange,
        IReadOnlyList<CrmChangedFileEvidence> changedFiles,
        IReadOnlyList<string> selfValidation,
        Guid submittedByUserId,
        DateTimeOffset submittedUtc,
        string? knownLimitations = null,
        string? notesToValidator = null)
    {
        if (candidateId == Guid.Empty)
            throw new ArgumentException("Candidate ID cannot be empty.", nameof(candidateId));
        if (sequence <= 0)
            throw new ArgumentOutOfRangeException(nameof(sequence));
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);
        if (commitCount < 0)
            throw new ArgumentOutOfRangeException(nameof(commitCount));
        ArgumentNullException.ThrowIfNull(changedFiles);
        ArgumentNullException.ThrowIfNull(selfValidation);
        if (submittedByUserId == Guid.Empty)
            throw new ArgumentException("Submitting user cannot be empty.", nameof(submittedByUserId));

        CandidateId = candidateId;
        WorkPackageId = workPackageId;
        Sequence = sequence;
        Branch = branch.Trim();
        SourceSha = sourceSha;
        CandidateSha = candidateSha;
        CommitCount = commitCount;
        CommitRange = string.IsNullOrWhiteSpace(commitRange) ? null : commitRange.Trim();
        ChangedFiles = changedFiles.ToArray();
        SelfValidation = selfValidation.Select(value => value.Trim()).Where(value => value.Length > 0).ToArray();
        SubmittedByUserId = submittedByUserId;
        SubmittedUtc = submittedUtc;
        KnownLimitations = string.IsNullOrWhiteSpace(knownLimitations) ? null : knownLimitations.Trim();
        NotesToValidator = string.IsNullOrWhiteSpace(notesToValidator) ? null : notesToValidator.Trim();
    }

    public Guid CandidateId { get; }
    public CrmWorkPackageId WorkPackageId { get; }
    public int Sequence { get; }
    public string Branch { get; }
    public CrmGitSha SourceSha { get; }
    public CrmGitSha CandidateSha { get; }
    public int CommitCount { get; }
    public string? CommitRange { get; }
    public IReadOnlyList<CrmChangedFileEvidence> ChangedFiles { get; }
    public IReadOnlyList<string> SelfValidation { get; }
    public Guid SubmittedByUserId { get; }
    public DateTimeOffset SubmittedUtc { get; }
    public string? KnownLimitations { get; }
    public string? NotesToValidator { get; }
}

public sealed record CrmCandidateValidation(
    Guid ValidationId,
    Guid CandidateId,
    CrmGitSha CandidateSha,
    CrmValidationGate Gate,
    CrmValidationResult Result,
    Guid ActorUserId,
    DateTimeOffset RecordedUtc,
    string? Summary);
