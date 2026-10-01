using System.Net;
using InterLan.Application.CrmHost;

namespace InterLan.Server.CrmGateway;

public static class CrmGatewayHost
{
    public const string ModeArgument = "--crm-gateway";

    public static WebApplication Build(string[] args)
    {
        var filteredArgs = args
            .Where(argument => !string.Equals(argument, ModeArgument, StringComparison.Ordinal))
            .ToArray();

        var builder = WebApplication.CreateBuilder(filteredArgs);
        var port = builder.Configuration.GetValue<int?>("CrmGateway:Port") ?? 5080;
        if (port is <= 0 or > 65535)
        {
            throw new InvalidOperationException("CrmGateway:Port must be between 1 and 65535.");
        }

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, port);
        });

        builder.Services.AddProblemDetails();
        var app = builder.Build();
        app.UseExceptionHandler();

        app.MapGet(GatewayProcessPlan.HealthPath, () => Results.Ok(new
        {
            status = "ready",
            surface = "crm-staff-gateway",
            leadAuthority = "local-native"
        }));

        app.MapFallback(() => Results.NotFound());
        return app;
    }
}
