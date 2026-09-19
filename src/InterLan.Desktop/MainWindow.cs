using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace InterLan.Desktop;

public sealed class MainWindow : Window
{
    private readonly TextBlock _status;

    public MainWindow()
    {
        Title = "INTER-LAN";
        Width = 920;
        Height = 620;
        MinWidth = 720;
        MinHeight = 480;

        var title = new TextBlock
        {
            Text = "INTER-LAN",
            FontSize = 34,
            FontWeight = FontWeight.Bold
        };

        var subtitle = new TextBlock
        {
            Text = "Native LAN communication · P0 foundation",
            FontSize = 15,
            Opacity = 0.72
        };

        _status = new TextBlock
        {
            Text = "Choose a runtime direction. P0 does not perform network enrollment yet.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.8
        };

        var createServer = new Button
        {
            Content = "Create Server",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 48
        };
        createServer.Click += (_, _) =>
            _status.Text = "Server Owner setup is intentionally staged for P1. P0 has established the ownership contract and durable schema.";

        var joinServer = new Button
        {
            Content = "Join Server",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 48
        };
        joinServer.Click += (_, _) =>
            _status.Text = "Client discovery/join is intentionally staged for P1. The native client boundary is active.";

        var panel = new StackPanel
        {
            Spacing = 16,
            Width = 520,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                title,
                subtitle,
                new Border { Height = 8 },
                createServer,
                joinServer,
                new Border { Height = 8 },
                _status
            }
        };

        Content = new Grid
        {
            Margin = new Thickness(32),
            Children = { panel }
        };
    }
}
