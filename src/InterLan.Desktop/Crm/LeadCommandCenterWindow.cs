using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace InterLan.Desktop.Crm;

public sealed class LeadCommandCenterWindow : Window
{
    private readonly LeadRuntimeViewModel _viewModel;
    private readonly TextBlock _phase;
    private readonly TextBlock _detail;
    private readonly TextBlock _backend;
    private readonly TextBlock _gateway;
    private readonly TextBlock _tunnel;
    private readonly TextBlock _localUrl;
    private readonly TextBlock _remoteUrl;
    private readonly TextBlock _operation;
    private readonly Button _startLocal;
    private readonly Button _startRemote;
    private readonly Button _stop;
    private readonly Button _enableRemote;
    private readonly Button _restartRemote;
    private readonly Button _disableRemote;
    private readonly Button _openRemote;
    private bool _busy;

    public LeadCommandCenterWindow(LeadRuntimeViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        Title = "INTER-LAN — Developer Operations Control Plane";
        Width = 1180;
        Height = 760;
        MinWidth = 920;
        MinHeight = 620;
        Background = new SolidColorBrush(Color.Parse("#08111F"));

        _phase = ValueText(24, FontWeight.Bold);
        _detail = ValueText(14, FontWeight.Normal);
        _detail.TextWrapping = TextWrapping.Wrap;
        _backend = ValueText();
        _gateway = ValueText();
        _tunnel = ValueText();
        _localUrl = ValueText();
        _remoteUrl = ValueText();
        _remoteUrl.TextWrapping = TextWrapping.Wrap;
        _operation = ValueText(13, FontWeight.Normal);

        _startLocal = ActionButton("Start Local Host");
        _startRemote = ActionButton("Start + Remote Access");
        _stop = ActionButton("Stop Host");
        _enableRemote = ActionButton("Enable Remote Access");
        _restartRemote = ActionButton("Restart Tunnel");
        _disableRemote = ActionButton("Disable Remote Access");
        _openRemote = ActionButton("Open Remote Gateway");

        _startLocal.Click += async (_, _) => await RunAsync(_viewModel.StartLocalAsync);
        _startRemote.Click += async (_, _) => await RunAsync(_viewModel.StartWithRemoteAccessAsync);
        _stop.Click += async (_, _) => await RunAsync(_viewModel.StopAsync);
        _enableRemote.Click += async (_, _) => await RunAsync(_viewModel.EnableRemoteAccessAsync);
        _restartRemote.Click += async (_, _) => await RunAsync(_viewModel.RestartRemoteAccessAsync);
        _disableRemote.Click += async (_, _) => await RunAsync(_viewModel.DisableRemoteAccessAsync);
        _openRemote.Click += (_, _) => OpenRemoteGateway();

        _viewModel.Changed += (_, _) => Dispatcher.UIThread.Post(Render);

        var title = new TextBlock
        {
            Text = "INTER-LAN",
            FontSize = 32,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White
        };

        var subtitle = new TextBlock
        {
            Text = "Developer Operations Control Plane · Local Technical Lead Authority",
            FontSize = 15,
            Foreground = new SolidColorBrush(Color.Parse("#8EA6C8"))
        };

        var commandPanel = Card(
            "HOST CONTROL",
            new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    _startLocal,
                    _startRemote,
                    _stop
                }
            });

        var runtimePanel = Card(
            "RUNTIME STATUS",
            new StackPanel
            {
                Spacing = 9,
                Children =
                {
                    Label("Host phase"), _phase,
                    Label("Canonical backend"), _backend,
                    Label("Developer gateway"), _gateway,
                    Label("Detail"), _detail,
                    Label("Local gateway"), _localUrl
                }
            });

        var remotePanel = Card(
            "REMOTE ACCESS · QUICK TUNNEL",
            new StackPanel
            {
                Spacing = 9,
                Children =
                {
                    Label("Tunnel"), _tunnel,
                    Label("Public gateway URL"), _remoteUrl,
                    new WrapPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Children =
                        {
                            _enableRemote,
                            _restartRemote,
                            _disableRemote,
                            _openRemote
                        }
                    }
                }
            });

        var authorityNotice = new TextBlock
        {
            Text = "Privileged Lead authority remains in this native application. The tunnel exposes only the subordinate browser gateway.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#8EA6C8")),
            FontSize = 13
        };

        Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(34),
                Spacing = 18,
                Children =
                {
                    title,
                    subtitle,
                    new Border { Height = 6 },
                    commandPanel,
                    runtimePanel,
                    remotePanel,
                    authorityNotice,
                    _operation
                }
            }
        };

        Render();
    }

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        SetButtonsEnabled(false);
        _operation.Text = "Applying runtime operation…";

        try
        {
            await action(CancellationToken.None);
            _viewModel.Refresh();
            _operation.Text = "Operation completed.";
        }
        catch (Exception ex)
        {
            _operation.Text = $"Operation failed: {ex.Message}";
        }
        finally
        {
            _busy = false;
            Render();
        }
    }

    private void Render()
    {
        var snapshot = _viewModel.Snapshot;
        _phase.Text = _viewModel.PhaseLabel;
        _detail.Text = _viewModel.Detail;
        _backend.Text = snapshot.Backend.ToString();
        _gateway.Text = snapshot.Gateway.ToString();
        _tunnel.Text = _viewModel.Tunnel.Phase.ToString();
        _localUrl.Text = _viewModel.LocalGateway;
        _remoteUrl.Text = _viewModel.RemoteGateway;

        var running = snapshot.Phase is not InterLan.Application.CrmHost.HostRuntimePhase.Stopped;
        var mutable = snapshot.Phase is InterLan.Application.CrmHost.HostRuntimePhase.Ready or
            InterLan.Application.CrmHost.HostRuntimePhase.Degraded;

        _startLocal.IsEnabled = !_busy && !running;
        _startRemote.IsEnabled = !_busy && !running;
        _stop.IsEnabled = !_busy && running;
        _enableRemote.IsEnabled = !_busy && mutable;
        _restartRemote.IsEnabled = !_busy && mutable;
        _disableRemote.IsEnabled = !_busy && mutable;
        _openRemote.IsEnabled = !_busy && _viewModel.Tunnel.PublicUri is not null;
    }

    private void OpenRemoteGateway()
    {
        var uri = _viewModel.Tunnel.PublicUri;
        if (uri is null)
        {
            _operation.Text = "Remote gateway is unavailable.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });
            _operation.Text = "Opened the role-limited remote gateway in the system browser.";
        }
        catch (Exception ex)
        {
            _operation.Text = $"Could not open the system browser: {ex.Message}";
        }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        _startLocal.IsEnabled = enabled;
        _startRemote.IsEnabled = enabled;
        _stop.IsEnabled = enabled;
        _enableRemote.IsEnabled = enabled;
        _restartRemote.IsEnabled = enabled;
        _disableRemote.IsEnabled = enabled;
        _openRemote.IsEnabled = enabled;
    }

    private static Border Card(string heading, Control body) => new()
    {
        Padding = new Thickness(20),
        Background = new SolidColorBrush(Color.Parse("#101D2F")),
        CornerRadius = new CornerRadius(10),
        Child = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = heading,
                    FontSize = 12,
                    FontWeight = FontWeight.Bold,
                    Foreground = new SolidColorBrush(Color.Parse("#55A7FF"))
                },
                body
            }
        }
    };

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        FontSize = 11,
        Foreground = new SolidColorBrush(Color.Parse("#7088A8"))
    };

    private static TextBlock ValueText(double size = 14, FontWeight? weight = null) => new()
    {
        FontSize = size,
        FontWeight = weight ?? FontWeight.SemiBold,
        Foreground = Brushes.White
    };

    private static Button ActionButton(string text) => new()
    {
        Content = text,
        MinHeight = 40,
        MinWidth = 150,
        Margin = new Thickness(0, 0, 10, 10)
    };
}
