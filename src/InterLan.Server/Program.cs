using System.Net;
using InterLan.Application;
using InterLan.Contracts;
using InterLan.Domain;
using InterLan.Infrastructure;
using InterLan.Server;
using InterLan.Server.Networking;
using InterLan.Server.Realtime;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Browser SignalR transports may need the standard access_token query parameter.
// Suppress framework request-start logging so bearer material is not written to logs.
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);

var dataDirectory = Environment.GetEnvironmentVariable("INTERLAN_DATA_DIR");
if (string.IsNullOrWhiteSpace(dataDirectory))
{
    dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
}

dataDirectory = Path.GetFullPath(dataDirectory);

var database = new SqliteDatabase(Path.Combine(dataDirectory, "interlan.db"));
await database.InitializeAsync();

var persistedSettings = await new ServerSettingsStore(database)
    .LoadAsync(dataDirectory);
var runtimeSettings = ServerRuntimeSettingsResolver.ApplyExplicitOverrides(
    persistedSettings,
    builder.Configuration);

if (!IPAddress.TryParse(runtimeSettings.BindAddress, out var bindAddress))
{
    throw new InvalidOperationException(
        $"Resolved server bind address is invalid: {runtimeSettings.BindAddress}");
}

var certificate = ServerCertificateManager.LoadOrCreate(dataDirectory);
var port = runtimeSettings.Port;

builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(bindAddress, runtimeSettings.Port, listen => listen.UseHttps(certificate.Certificate));
});

builder.Services.AddSingleton(database);
builder.Services.AddSingleton(runtimeSettings);
builder.Services.AddSingleton<ServerSettingsStore>();
builder.Services.AddSingleton(certificate);
builder.Services.AddSingleton<IServerIdentityStore, SqliteServerIdentityStore>();
builder.Services.AddSingleton<EnrollmentStore>();
builder.Services.AddSingleton<ChatStore>();
builder.Services.AddSingleton<RealtimeConnectionRegistry>();
builder.Services.AddHostedService<LanDiscoveryBroadcaster>();
builder.Services.AddSignalR();
builder.Services.AddProblemDetails();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            RateLimitPartitionKeys.RemoteAddress(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 8,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("join", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            RateLimitPartitionKeys.RemoteAddress(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("message", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            RateLimitPartitionKeys.AuthenticatedClient(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseRateLimiter();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapInterLanSystemEndpoints();

app.MapInterLanEnrollmentEndpoints();

app.MapGet("/api/v1/users", async (
    HttpContext context,
    EnrollmentStore enrollment,
    ChatStore chat,
    CancellationToken cancellationToken) =>
{
    try
    {
        var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(context, enrollment, cancellationToken);
        return Results.Ok(await chat.ListUsersAsync(principal.UserId, cancellationToken));
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
});

app.MapGet("/api/v1/direct", async (
    HttpContext context,
    EnrollmentStore enrollment,
    ChatStore chat,
    CancellationToken cancellationToken) =>
{
    try
    {
        var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(context, enrollment, cancellationToken);
        return Results.Ok(await chat.ListDirectConversationsAsync(principal.UserId, cancellationToken));
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
});

app.MapPost("/api/v1/direct", async (
    HttpContext context,
    OpenDirectConversationRequest request,
    EnrollmentStore enrollment,
    ChatStore chat,
    CancellationToken cancellationToken) =>
{
    try
    {
        var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(context, enrollment, cancellationToken);
        return Results.Ok(await chat.GetOrCreateDirectConversationAsync(
            principal.UserId,
            request.OtherUserId,
            cancellationToken));
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
});

app.MapGet("/api/v1/direct/{conversationId:guid}/messages", async (
    Guid conversationId,
    Guid? afterMessageId,
    int? limit,
    HttpContext context,
    EnrollmentStore enrollment,
    ChatStore chat,
    CancellationToken cancellationToken) =>
{
    try
    {
        var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(context, enrollment, cancellationToken);
        return Results.Ok(await chat.GetDirectHistoryAsync(
            principal.UserId,
            conversationId,
            afterMessageId,
            Math.Clamp(limit ?? 100, 1, 250),
            cancellationToken));
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
});

app.MapPost("/api/v1/direct/{conversationId:guid}/messages", async (
    Guid conversationId,
    HttpContext context,
    SendMessageRequest request,
    EnrollmentStore enrollment,
    ChatStore chat,
    Microsoft.AspNetCore.SignalR.IHubContext<ChatHub> hub,
    CancellationToken cancellationToken) =>
{
    try
    {
        var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(context, enrollment, cancellationToken);
        var result = await chat.SendDirectMessageAsync(
            principal.UserId,
            conversationId,
            request,
            cancellationToken);

        if (result.Created)
        {
            var members = await chat.GetDirectMemberIdsAsync(principal.UserId, conversationId, cancellationToken);
            foreach (var memberId in members)
            {
                await hub.Clients.Group(ChatHub.UserGroup(memberId))
                    .SendAsync("MessageCreated", result.Message, cancellationToken);
            }
        }

        return Results.Ok(result.Message);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new { error = exception.Message });
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
}).RequireRateLimiting("message");

app.MapGet("/api/v1/messages/{messageId:guid}/receipts", async (
    Guid messageId,
    HttpContext context,
    EnrollmentStore enrollment,
    ChatStore chat,
    CancellationToken cancellationToken) =>
{
    try
    {
        var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(context, enrollment, cancellationToken);
        return Results.Ok(await chat.GetReceiptsAsync(principal.UserId, messageId, cancellationToken));
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
});

app.MapPost("/api/v1/messages/{messageId:guid}/delivered", async (
    Guid messageId,
    HttpContext context,
    EnrollmentStore enrollment,
    ChatStore chat,
    CancellationToken cancellationToken) =>
{
    try
    {
        var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(context, enrollment, cancellationToken);
        await chat.MarkDeliveredAsync(principal.UserId, messageId, cancellationToken);
        return Results.NoContent();
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new { error = exception.Message });
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
});

app.MapPost("/api/v1/messages/{messageId:guid}/read", async (
    Guid messageId,
    HttpContext context,
    EnrollmentStore enrollment,
    ChatStore chat,
    CancellationToken cancellationToken) =>
{
    try
    {
        var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(context, enrollment, cancellationToken);
        await chat.MarkReadAsync(principal.UserId, messageId, cancellationToken);
        return Results.NoContent();
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new { error = exception.Message });
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
});

app.MapHub<ControlHub>("/hubs/control");
app.MapHub<ChatHub>("/hubs/chat");
app.MapFallbackToFile("/client/{*path:nonfile}", "client/index.html");

await app.RunAsync();
