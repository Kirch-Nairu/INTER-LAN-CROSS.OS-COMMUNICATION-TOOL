using InterLan.Contracts;
using Microsoft.AspNetCore.SignalR.Client;

namespace InterLan.Application;

public sealed class InterLanRealtimeClient : IAsyncDisposable
{
    private readonly HubConnection _connection;

    public InterLanRealtimeClient(
        ClientPairingState pairing,
        string bearerToken)
    {
        ArgumentNullException.ThrowIfNull(pairing);
        ArgumentException.ThrowIfNullOrWhiteSpace(bearerToken);

        var hubUri = new Uri(
            new Uri(pairing.ServerUri),
            "/hubs/chat");

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.Headers["Authorization"] =
                    $"Bearer {bearerToken}";
                options.HttpMessageHandlerFactory =
                    _ => PinnedTransportFactory.CreateHandler(pairing);
            })
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10)
            })
            .Build();

        _connection.On<MessageResponse>(
            "MessageCreated",
            message => MessageCreated?.Invoke(message));
    }

    public event Action<MessageResponse>? MessageCreated;

    public HubConnectionState State => _connection.State;

    public Task StartAsync(
        CancellationToken cancellationToken = default) =>
        _connection.StartAsync(cancellationToken);

    public Task StopAsync(
        CancellationToken cancellationToken = default) =>
        _connection.StopAsync(cancellationToken);

    public ValueTask DisposeAsync() =>
        _connection.DisposeAsync();
}
