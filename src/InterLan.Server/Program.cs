using InterLan.Server;

var app = await InterLanServerHost.BuildAsync(args);
await app.RunAsync();
