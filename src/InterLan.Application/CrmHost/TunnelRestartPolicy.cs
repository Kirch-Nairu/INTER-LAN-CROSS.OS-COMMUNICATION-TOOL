namespace InterLan.Application.CrmHost;

public sealed record TunnelRestartDecision(
    bool ShouldRestart,
    int Attempt,
    TimeSpan Delay,
    string Reason);

public sealed class TunnelRestartPolicy
{
    private readonly int _maxAttempts;
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;

    public TunnelRestartPolicy(int maxAttempts, TimeSpan baseDelay, TimeSpan? maxDelay = null)
    {
        if (maxAttempts < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        }

        if (baseDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(baseDelay));
        }

        _maxAttempts = maxAttempts;
        _baseDelay = baseDelay;
        _maxDelay = maxDelay ?? TimeSpan.FromSeconds(30);
    }

    public TunnelRestartDecision Decide(int completedAttempts, bool networkAvailable)
    {
        if (!networkAvailable)
        {
            return new TunnelRestartDecision(
                false,
                completedAttempts,
                TimeSpan.Zero,
                "Network is unavailable; preserve local authority and wait for connectivity before restarting the tunnel.");
        }

        var nextAttempt = completedAttempts + 1;
        if (nextAttempt > _maxAttempts)
        {
            return new TunnelRestartDecision(
                false,
                nextAttempt,
                TimeSpan.Zero,
                "Quick Tunnel restart budget is exhausted.");
        }

        var multiplier = Math.Pow(2, Math.Max(0, nextAttempt - 1));
        var delayTicks = Math.Min(_maxDelay.Ticks, (long)(_baseDelay.Ticks * multiplier));
        return new TunnelRestartDecision(
            true,
            nextAttempt,
            TimeSpan.FromTicks(delayTicks),
            $"Quick Tunnel restart attempt {nextAttempt} is permitted.");
    }
}
