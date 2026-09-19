using InterLan.Domain;

namespace InterLan.Application;

public interface IServerIdentityStore
{
    Task<ServerIdentity?> GetAsync(CancellationToken cancellationToken = default);
    Task CreateAsync(ServerIdentity identity, CancellationToken cancellationToken = default);
    Task TransferOwnerAsync(Guid expectedCurrentOwnerUserId, Guid nextOwnerUserId, CancellationToken cancellationToken = default);
}
