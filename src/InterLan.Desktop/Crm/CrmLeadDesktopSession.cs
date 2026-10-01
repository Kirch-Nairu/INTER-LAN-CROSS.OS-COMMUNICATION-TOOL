using InterLan.Application.CrmHost;
using InterLan.Server;

namespace InterLan.Desktop.Crm;

public sealed class CrmLeadDesktopSession : IAsyncDisposable
{
    private CrmLeadDesktopSession(
        LinuxHostRuntime runtime,
        LeadRuntimeViewModel viewModel,
        LeadCommandCenterWindow window,
        HostSuspendResumeCoordinator powerLifecycle)
    {
        Runtime = runtime;
        ViewModel = viewModel;
        Window = window;
        PowerLifecycle = powerLifecycle;
    }

    public LinuxHostRuntime Runtime { get; }

    public LeadRuntimeViewModel ViewModel { get; }

    public LeadCommandCenterWindow Window { get; }

    public HostSuspendResumeCoordinator PowerLifecycle { get; }

    public static CrmLeadDesktopSession Create()
    {
        var paths = LinuxHostPaths.Resolve();
        var options = new HostRuntimeOptions
        {
            Paths = paths
        };
        options.Validate();

        var events = new InMemoryHostLifecycleEventSink();
        var serverAssemblyPath = typeof(InterLanServerHost).Assembly.Location;
        var runtime = new LinuxHostRuntime(
            options,
            serverAssemblyPath,
            events);
        var viewModel = new LeadRuntimeViewModel(runtime, runtime);
        var window = new LeadCommandCenterWindow(viewModel);
        var powerLifecycle = new HostSuspendResumeCoordinator(runtime, runtime, events);

        return new CrmLeadDesktopSession(runtime, viewModel, window, powerLifecycle);
    }

    public async ValueTask DisposeAsync()
    {
        await Runtime.DisposeAsync();
    }
}
