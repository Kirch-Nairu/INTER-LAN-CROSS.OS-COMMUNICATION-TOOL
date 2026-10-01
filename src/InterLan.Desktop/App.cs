using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using InterLan.Desktop.Crm;

namespace InterLan.Desktop;

public sealed class App : Avalonia.Application
{
    private CrmLeadDesktopSession? _crmSession;

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _crmSession = CrmLeadDesktopSession.Create();
            desktop.MainWindow = _crmSession.Window;
            desktop.Exit += (_, _) =>
            {
                if (_crmSession is not null)
                {
                    _crmSession.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    _crmSession = null;
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
