using System.Collections.Concurrent;
using InterLan.Application;

namespace InterLan.Server.Realtime;

public sealed class RealtimeConnectionRegistry
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, Action>> _sessions = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, Action>> _devices = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, Action>> _users = new();

    public IDisposable Register(
        SessionPrincipal principal,
        string connectionId,
        Action abort)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentNullException.ThrowIfNull(abort);

        Add(_sessions, principal.SessionId, connectionId, abort);
        Add(_users, principal.UserId, connectionId, abort);
        if (principal.DeviceId is { } deviceId)
            Add(_devices, deviceId, connectionId, abort);

        return new Lease(
            this,
            principal.UserId,
            principal.SessionId,
            principal.DeviceId,
            connectionId);
    }

    public int RevokeSession(Guid sessionId) =>
        Revoke(_sessions, sessionId);

    public int RevokeDevice(Guid deviceId) =>
        Revoke(_devices, deviceId);

    public bool IsUserOnline(Guid userId) =>
        _users.TryGetValue(userId, out var bucket) && !bucket.IsEmpty;

    public int GetUserConnectionCount(Guid userId) =>
        _users.TryGetValue(userId, out var bucket) ? bucket.Count : 0;

    public IReadOnlyList<Guid> GetOnlineUserIds() =>
        _users
            .Where(pair => !pair.Value.IsEmpty)
            .Select(pair => pair.Key)
            .OrderBy(id => id)
            .ToArray();

    private static void Add(
        ConcurrentDictionary<Guid, ConcurrentDictionary<string, Action>> index,
        Guid authorityId,
        string connectionId,
        Action abort)
    {
        var bucket = index.GetOrAdd(authorityId, _ => new ConcurrentDictionary<string, Action>(StringComparer.Ordinal));
        bucket[connectionId] = abort;
    }

    private static int Revoke(
        ConcurrentDictionary<Guid, ConcurrentDictionary<string, Action>> index,
        Guid authorityId)
    {
        if (!index.TryRemove(authorityId, out var bucket))
            return 0;

        var aborted = 0;
        foreach (var abort in bucket.Values)
        {
            try
            {
                abort();
                aborted++;
            }
            catch
            {
                // Revocation is best effort at the transport layer.
                // Durable authorization is still enforced by session validation.
            }
        }

        return aborted;
    }

    private void Unregister(
        Guid userId,
        Guid sessionId,
        Guid? deviceId,
        string connectionId)
    {
        Remove(_users, userId, connectionId);
        Remove(_sessions, sessionId, connectionId);
        if (deviceId is { } id)
            Remove(_devices, id, connectionId);
    }

    private static void Remove(
        ConcurrentDictionary<Guid, ConcurrentDictionary<string, Action>> index,
        Guid authorityId,
        string connectionId)
    {
        if (!index.TryGetValue(authorityId, out var bucket))
            return;

        bucket.TryRemove(connectionId, out _);
        if (bucket.IsEmpty)
            index.TryRemove(new KeyValuePair<Guid, ConcurrentDictionary<string, Action>>(authorityId, bucket));
    }

    private sealed class Lease(
        RealtimeConnectionRegistry owner,
        Guid userId,
        Guid sessionId,
        Guid? deviceId,
        string connectionId) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            owner.Unregister(userId, sessionId, deviceId, connectionId);
        }
    }
}
