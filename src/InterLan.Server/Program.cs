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
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var dataDirectory = Environment.GetEnvironmentVariable("INTERLAN_DATA_DIR");
if (string.IsNullOrWhiteSpace(dataDirectory))
{
    dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
}

var certificate = ServerCertificateManager.LoadOrCreate(dataDirectory);
var port = builder.Configuration.GetValue("InterLan:Server:Port", 7443);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(port, listen => listen.UseHttps(certificate.Certificate));
});

var database = new SqliteDatabase(Path.Combine(dataDirectory, "interlan.db"));
await database.InitializeAsync();

builder.Services.AddSingleton(database);
builder.Services.AddSingleton(certificate);
builder.Services.AddSingleton<IServerIdentityStore, SqliteServerIdentityStore>();
builder.Services.AddSingleton<EnrollmentStore>();
builder.Services.AddHostedService<LanDiscoveryBroadcaster>();
builder.Services.AddSignalR();
builder.Services.AddProblemDetails();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 8;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });

    options.AddFixedWindowLimiter("join", limiter =>
    {
        limiter.PermitLimit = 20;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseRateLimiter();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    apiVersion = ApiContractVersion.Current,
    utc = DateTimeOffset.UtcNow
}));

app.MapGet("/api/v1/server/info", async (
    IServerIdentityStore identities,
    ServerCertificateDescriptor cert,
    CancellationToken cancellationToken) =>
{
    var identity = await identities.GetAsync(cancellationToken);
    return Results.Ok(new
    {
        server = new ServerInfoResponse(
            ApiContractVersion.Current,
            identity is not null,
            identity?.ServerId,
            identity?.ServerName,
            identity is null ? RuntimeMode.Unconfigured : RuntimeMode.ServerOwner,
            NativeDesktopSupported: true,
            WebClientSupported: true),
        httpsPort = port,
        certificateSha256 = cert.Sha256Fingerprint
    });
});

app.MapPost("/api/v1/bootstrap/server", async (
    HttpContext context,
    BootstrapServerRequest request,
    EnrollmentStore enrollment,
    ServerCertificateDescriptor cert,
    CancellationToken cancellationToken) =>
{
    var remote = context.Connection.RemoteIpAddress;
    if (remote is null || !IPAddress.IsLoopback(remote))
        return Results.Forbid();

    try
    {
        var identity = await enrollment.BootstrapOwnerAsync(
            request.ServerName,
            request.OwnerUsername,
            request.OwnerDisplayName,
            request.OwnerPassword,
            Path.Combine(dataDirectory, "files"),
            port,
            builder.Configuration.GetValue("InterLan:Server:DiscoveryEnabled", true),
            cancellationToken);

        return Results.Ok(new BootstrapServerResponse(
            identity.ServerId,
            identity.OwnerUserId,
            identity.ServerName,
            cert.Sha256Fingerprint));
    }
    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or SqliteException)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
}).RequireRateLimiting("auth");

app.MapPost("/api/v1/auth/owner/login", async (
    LoginRequest request,
    EnrollmentStore enrollment,
    CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await enrollment.LoginOwnerAsync(
            request.Username,
            request.Password,
            TimeSpan.FromHours(12),
            cancellationToken));
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
}).RequireRateLimiting("auth");

app.MapPost("/api/v1/invites", async (
    HttpContext context,
    CreateInviteRequest request,
    EnrollmentStore enrollment,
    CancellationToken cancellationToken) =>
{
    try
    {
        var owner = await AuthorizationHelpers.RequireOwnerAsync(context, enrollment, cancellationToken);
        var minutes = Math.Clamp(request.ValidMinutes, 1, 10_080);
        return Results.Ok(await enrollment.CreateInviteAsync(
            owner.UserId,
            TimeSpan.FromMinutes(minutes),
            cancellationToken));
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
});

app.MapPost("/api/v1/join", async (
    SubmitJoinRequest request,
    EnrollmentStore enrollment,
    CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Accepted(
            value: await enrollment.SubmitJoinAsync(request, cancellationToken));
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
}).RequireRateLimiting("join");

app.MapGet("/api/v1/join/pending", async (
    HttpContext context,
    EnrollmentStore enrollment,
    CancellationToken cancellationToken) =>
{
    try
    {
        var owner = await AuthorizationHelpers.RequireOwnerAsync(context, enrollment, cancellationToken);
        return Results.Ok(await enrollment.ListPendingJoinsAsync(owner.UserId, cancellationToken));
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
});

app.MapGet("/api/v1/devices", async (
    HttpContext context,
    EnrollmentStore enrollment,
    CancellationToken cancellationToken) =>
{
    try
    {
        var owner = await AuthorizationHelpers.RequireOwnerAsync(context, enrollment, cancellationToken);
        return Results.Ok(await enrollment.ListDevicesAsync(owner.UserId, cancellationToken));
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
});

app.MapPost("/api/v1/join/{requestId:guid}/decision", async (
    Guid requestId,
    HttpContext context,
    JoinDecisionRequest request,
    EnrollmentStore enrollment,
    CancellationToken cancellationToken) =>
{
    try
    {
        var owner = await AuthorizationHelpers.RequireOwnerAsync(context, enrollment, cancellationToken);
        return Results.Ok(await enrollment.DecideJoinAsync(
            owner.UserId,
            requestId,
            request.Approve,
            cancellationToken));
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
    catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException or SqliteException)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.MapPost("/api/v1/join/exchange", async (
    ExchangeJoinRequest request,
    EnrollmentStore enrollment,
    CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await enrollment.ExchangeApprovedJoinAsync(
            request.RequestId,
            request.EnrollmentSecret,
            TimeSpan.FromDays(14),
            cancellationToken));
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
}).RequireRateLimiting("join");

app.MapPost("/api/v1/sessions/{sessionId:guid}/revoke", async (
    Guid sessionId,
    HttpContext context,
    EnrollmentStore enrollment,
    CancellationToken cancellationToken) =>
{
    try
    {
        var owner = await AuthorizationHelpers.RequireOwnerAsync(context, enrollment, cancellationToken);
        await enrollment.RevokeSessionAsync(owner.UserId, sessionId, cancellationToken);
        return Results.NoContent();
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

app.MapPost("/api/v1/devices/{deviceId:guid}/revoke", async (
    Guid deviceId,
    HttpContext context,
    EnrollmentStore enrollment,
    CancellationToken cancellationToken) =>
{
    try
    {
        var owner = await AuthorizationHelpers.RequireOwnerAsync(context, enrollment, cancellationToken);
        await enrollment.RevokeDeviceAsync(owner.UserId, deviceId, cancellationToken);
        return Results.NoContent();
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
});

app.MapHub<ControlHub>("/hubs/control");
app.MapFallbackToFile("/client/{*path:nonfile}", "client/index.html");

await app.RunAsync();
