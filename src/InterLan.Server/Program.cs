using InterLan.Application;
using InterLan.Contracts;
using InterLan.Domain;
using InterLan.Infrastructure;
using InterLan.Server.Realtime;

var builder = WebApplication.CreateBuilder(args);

var dataDirectory = Environment.GetEnvironmentVariable("INTERLAN_DATA_DIR");
if (string.IsNullOrWhiteSpace(dataDirectory))
{
    dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
}

var database = new SqliteDatabase(Path.Combine(dataDirectory, "interlan.db"));
await database.InitializeAsync();

builder.Services.AddSingleton(database);
builder.Services.AddSingleton<IServerIdentityStore, SqliteServerIdentityStore>();
builder.Services.AddSignalR();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    apiVersion = ApiContractVersion.Current,
    utc = DateTimeOffset.UtcNow
}));

app.MapGet("/api/v1/server/info", async (IServerIdentityStore identities, CancellationToken cancellationToken) =>
{
    var identity = await identities.GetAsync(cancellationToken);
    return Results.Ok(new ServerInfoResponse(
        ApiContractVersion.Current,
        identity is not null,
        identity?.ServerId,
        identity?.ServerName,
        identity is null ? RuntimeMode.Unconfigured : RuntimeMode.ServerOwner,
        NativeDesktopSupported: true,
        WebClientSupported: true));
});

app.MapHub<ControlHub>("/hubs/control");
app.MapFallbackToFile("/client/{*path:nonfile}", "client/index.html");

await app.RunAsync();
