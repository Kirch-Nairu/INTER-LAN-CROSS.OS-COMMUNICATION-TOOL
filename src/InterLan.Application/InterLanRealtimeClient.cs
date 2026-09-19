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

        _connection.On<MessageResponse>(
            "MessageEdited",
            message => MessageEdited?.Invoke(message));

        _connection.On<MessageDeletedResponse>(
            "MessageDeleted",
            message => MessageDeleted?.Invoke(message));

        _connection.On<MessageReceiptsChangedResponse>(
            "ReceiptUpdated",
            receipt => ReceiptUpdated?.Invoke(receipt));

        _connection.On<GroupMessageDeletedResponse>(
            "GroupMessageDeleted",
            message => GroupMessageDeleted?.Invoke(message));

        _connection.On<GroupMessageReceiptsChangedResponse>(
            "GroupReceiptUpdated",
            receipt => GroupReceiptUpdated?.Invoke(receipt));

        _connection.On<GroupTypingIndicatorResponse>(
            "GroupTypingChanged",
            typing => GroupTypingChanged?.Invoke(typing));

        _connection.On<UserPresenceResponse>(
            "PresenceChanged",
            presence => PresenceChanged?.Invoke(presence));

        _connection.On<TypingIndicatorResponse>(
            "TypingChanged",
            typing => TypingChanged?.Invoke(typing));

        _connection.Reconnecting += exception =>
        {
            Reconnecting?.Invoke(exception);
            return Task.CompletedTask;
        };

        _connection.Reconnected += connectionId =>
        {
            Reconnected?.Invoke(connectionId);
            return Task.CompletedTask;
        };

        _connection.Closed += exception =>
        {
            Closed?.Invoke(exception);
            return Task.CompletedTask;
        };
    }

    public event Action<MessageResponse>? MessageCreated;
    public event Action<MessageResponse>? MessageEdited;
    public event Action<MessageDeletedResponse>? MessageDeleted;
    public event Action<MessageReceiptsChangedResponse>? ReceiptUpdated;
    public event Action<GroupMessageDeletedResponse>? GroupMessageDeleted;
    public event Action<GroupMessageReceiptsChangedResponse>? GroupReceiptUpdated;
    public event Action<GroupTypingIndicatorResponse>? GroupTypingChanged;
    public event Action<UserPresenceResponse>? PresenceChanged;
    public event Action<TypingIndicatorResponse>? TypingChanged;
    public event Action<Exception?>? Reconnecting;
    public event Action<string?>? Reconnected;
    public event Action<Exception?>? Closed;

    public HubConnectionState State => _connection.State;

    public Task StartAsync(
        CancellationToken cancellationToken = default) =>
        _connection.StartAsync(cancellationToken);

    public Task StopAsync(
        CancellationToken cancellationToken = default) =>
        _connection.StopAsync(cancellationToken);

    public Task SetTypingAsync(
        Guid conversationId,
        bool isTyping,
        CancellationToken cancellationToken = default) =>
        _connection.InvokeAsync(
            "SetTyping",
            conversationId,
            isTyping,
            cancellationToken);

    public Task SetGroupTypingAsync(
        Guid groupId,
        bool isTyping,
        CancellationToken cancellationToken = default) =>
        _connection.InvokeAsync(
            "SetGroupTyping",
            groupId,
            isTyping,
            cancellationToken);

    public ValueTask DisposeAsync() =>
        _connection.DisposeAsync();
}
