namespace InterLan.Domain;

public sealed record ServerIdentity(
    Guid ServerId,
    string ServerName,
    Guid OwnerUserId,
    DateTimeOffset CreatedUtc);
