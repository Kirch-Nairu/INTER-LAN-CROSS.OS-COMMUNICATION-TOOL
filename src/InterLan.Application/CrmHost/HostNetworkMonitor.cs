using System.Net.NetworkInformation;

namespace InterLan.Application.CrmHost;

public interface IHostNetworkStatus
{
    bool IsNetworkAvailable { get; }

    event EventHandler<bool>? AvailabilityChanged;
}

public sealed class HostNetworkMonitor : IHostNetworkStatus, IDisposable
{
    private int _disposed;

    public HostNetworkMonitor()
    {
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
    }

    public bool IsNetworkAvailable => NetworkInterface.GetIsNetworkAvailable();

    public event EventHandler<bool>? AvailabilityChanged;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs args)
    {
        AvailabilityChanged?.Invoke(this, args.IsAvailable);
    }
}

public sealed class FixedHostNetworkStatus(bool isAvailable) : IHostNetworkStatus
{
    public bool IsNetworkAvailable { get; private set; } = isAvailable;

    public event EventHandler<bool>? AvailabilityChanged;

    public void SetAvailable(bool available)
    {
        if (IsNetworkAvailable == available)
        {
            return;
        }

        IsNetworkAvailable = available;
        AvailabilityChanged?.Invoke(this, available);
    }
}
