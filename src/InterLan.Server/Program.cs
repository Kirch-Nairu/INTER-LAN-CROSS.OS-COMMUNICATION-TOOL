using InterLan.Server;
using InterLan.Server.CrmGateway;

if (args.Contains(CrmGatewayHost.ModeArgument, StringComparer.Ordinal))
{
    var gateway = CrmGatewayHost.Build(args);
    await gateway.RunAsync();
    return;
}

var app = await InterLanServerHost.BuildAsync(args);
await app.RunAsync();
