using InterLan.Application.CrmHost;
using InterLan.Server;

namespace InterLan.Desktop.Crm;

public sealed class CrmLeadDesktopSession : IAsyncDisposable
{
    private CrmLeadDesktopSession(
        LinuxHostRuntime runtime,
        LeadRuntimeViewModel viewModel,
        LeadCommandCenterWindow window)
    {
        Runtime = runtime;
        ViewModel = viewModel;
        Window = window;
    }

    public LinuxHostRuntime Runtime { get; }

    public LeadRuntimeViewModel ViewModel { get; }

    public LeadCommandCenterWindow Window { get; }

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

        return new CrmLeadDesktopSession(runtime, viewModel, window);
    }

    public async ValueTask DisposeAsync()
    {
        await Runtime.DisposeAsync();
    }
}
