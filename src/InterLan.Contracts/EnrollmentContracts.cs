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
    DateTimeOffset ExpiresUtc);

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

public sealed record JoinDecisionRequest(bool Approve);

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
