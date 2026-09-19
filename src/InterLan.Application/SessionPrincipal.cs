namespace InterLan.Application;

public sealed record SessionPrincipal(
    Guid SessionId,
    Guid UserId,
    Guid? DeviceId,
    string Role,
    DateTimeOffset ExpiresUtc);
