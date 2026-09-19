namespace InterLan.Contracts;

public sealed record BootstrapServerRequest(
    string ServerName,
    string OwnerUsername,
    string OwnerDisplayName,
    string OwnerPassword);

public sealed record BootstrapServerResponse(
    Guid ServerId,
    Guid OwnerUserId,
    string ServerName,
    string CertificateSha256);

public sealed record LoginRequest(
    string Username,
    string Password);

public sealed record SessionResponse(
    Guid SessionId,
    Guid UserId,
    Guid? DeviceId,
    string Role,
    string BearerToken,
    DateTimeOffset ExpiresUtc,
    string? DeviceCredential = null);

public sealed record RenewDeviceSessionRequest(
    Guid DeviceId,
    string DeviceCredential);

public sealed record CreateInviteRequest(int ValidMinutes = 60);

public sealed record InviteResponse(
    Guid InviteId,
    string InviteToken,
    DateTimeOffset ExpiresUtc);

public sealed record SubmitJoinRequest(
    string InviteToken,
    string Username,
    string DisplayName,
    string DeviceName,
    string Platform,
    string EnrollmentSecret);

public sealed record SubmitJoinResponse(
    Guid RequestId,
    string Status);

public sealed record JoinDecisionRequest(
    bool Approve,
    Guid? ExistingUserId = null);

public sealed record JoinDecisionResponse(
    Guid RequestId,
    string Status,
    Guid? UserId,
    Guid? DeviceId);

public sealed record ExchangeJoinRequest(
    Guid RequestId,
    string EnrollmentSecret);

public sealed record PendingJoinRequestResponse(
    Guid RequestId,
    string Username,
    string DisplayName,
    string DeviceName,
    string Platform,
    DateTimeOffset CreatedUtc);

public sealed record DeviceSummaryResponse(
    Guid DeviceId,
    Guid UserId,
    string Username,
    string DisplayName,
    string DeviceName,
    string Platform,
    DateTimeOffset? ApprovedUtc,
    DateTimeOffset? RevokedUtc,
    DateTimeOffset? LastSeenUtc);

public sealed record ServerSettingsResponse(
    string BindAddress,
    int Port,
    bool DiscoveryEnabled,
    bool ClientApprovalRequired,
    string StoragePath);

public sealed record ServerSettingsSnapshotResponse(
    ServerSettingsResponse Active,
    ServerSettingsResponse Persisted,
    bool RestartRequired);

public sealed record UpdateServerSettingsRequest(
    string BindAddress,
    int Port,
    bool DiscoveryEnabled,
    bool ClientApprovalRequired,
    string StoragePath);

public sealed record SessionSummaryResponse(
    Guid SessionId,
    Guid? DeviceId,
    DateTimeOffset CreatedUtc,
    DateTimeOffset ExpiresUtc,
    DateTimeOffset? RevokedUtc,
    DateTimeOffset? LastSeenUtc,
    bool Current);

public sealed record RevokeOtherSessionsResponse(
    int RevokedCount);

public sealed record CurrentDeviceSecurityResponse(
    Guid DeviceId,
    string DeviceName,
    string Platform,
    DateTimeOffset ApprovedUtc,
    DateTimeOffset? LastSeenUtc,
    DateTimeOffset? CredentialCreatedUtc,
    DateTimeOffset? CredentialRotatedUtc,
    DateTimeOffset? CredentialLastUsedUtc);
