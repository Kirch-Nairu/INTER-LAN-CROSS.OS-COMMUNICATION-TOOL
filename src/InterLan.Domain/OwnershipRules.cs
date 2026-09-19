namespace InterLan.Domain;

public static class OwnershipRules
{
    public static void ValidateOwner(ServerIdentity identity, Guid actorUserId)
    {
        ArgumentNullException.ThrowIfNull(identity);

        if (actorUserId == Guid.Empty || identity.OwnerUserId != actorUserId)
        {
            throw new UnauthorizedAccessException("The actor is not the configured server owner.");
        }
    }

    public static ServerIdentity Transfer(ServerIdentity identity, Guid actorUserId, Guid nextOwnerUserId)
    {
        ValidateOwner(identity, actorUserId);

        if (nextOwnerUserId == Guid.Empty)
        {
            throw new ArgumentException("A server owner must have a non-empty user ID.", nameof(nextOwnerUserId));
        }

        return identity with { OwnerUserId = nextOwnerUserId };
    }
}
