var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<AgentDispatcher.Worker.Worker>();

var host = builder.Build();
host.Run();
