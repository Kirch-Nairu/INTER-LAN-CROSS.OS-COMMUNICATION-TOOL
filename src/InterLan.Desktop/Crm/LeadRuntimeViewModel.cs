using InterLan.Application.CrmHost;

namespace InterLan.Desktop.Crm;

public sealed class LeadRuntimeViewModel
{
    private readonly IHostRuntimeOperations _host;
    private readonly IRemoteAccessHostOperations _remote;

    public LeadRuntimeViewModel(
        IHostRuntimeOperations host,
        IRemoteAccessHostOperations remote)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _remote = remote ?? throw new ArgumentNullException(nameof(remote));
        Snapshot = host.Snapshot;
        Tunnel = remote.RemoteAccessState;
    }

    public HostRuntimeSnapshot Snapshot { get; private set; }

    public TunnelRuntimeState Tunnel { get; private set; }

    public string PhaseLabel => Snapshot.Phase.ToString().ToUpperInvariant();

    public string LocalGateway => Snapshot.LocalGatewayUri?.AbsoluteUri ?? "Unavailable";

    public string RemoteGateway => Tunnel.PublicUri?.AbsoluteUri ?? "Remote access unavailable";

    public string Detail => Snapshot.Detail ?? Tunnel.Detail ?? "No runtime detail available.";

    public event EventHandler? Changed;

    public async Task StartLocalAsync(CancellationToken cancellationToken = default)
    {
        await _host.StartAsync(remoteAccess: false, cancellationToken);
        Refresh();
    }

    public async Task StartWithRemoteAccessAsync(CancellationToken cancellationToken = default)
    {
        await _host.StartAsync(remoteAccess: true, cancellationToken);
        Refresh();
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _host.StopAsync(cancellationToken);
        Refresh();
    }

    public async Task EnableRemoteAccessAsync(CancellationToken cancellationToken = default)
    {
        await _remote.EnableRemoteAccessAsync(cancellationToken);
        Refresh();
    }

    public async Task RestartRemoteAccessAsync(CancellationToken cancellationToken = default)
    {
        await _remote.RestartRemoteAccessAsync(cancellationToken);
        Refresh();
    }

    public async Task DisableRemoteAccessAsync(CancellationToken cancellationToken = default)
    {
        await _remote.DisableRemoteAccessAsync(cancellationToken);
        Refresh();
    }

    public void Refresh()
    {
        Snapshot = _host.Snapshot;
        Tunnel = _remote.RemoteAccessState;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
