using Trackside.RigAgent;

// Entry point for the future rig-side Trackside agent process.
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
