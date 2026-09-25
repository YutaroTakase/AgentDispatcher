using System.Text.Json.Serialization;
using AgentDispatcher.Api.Executions;
using AgentDispatcher.Api.Health;
using AgentDispatcher.Api.Maintenance;
using AgentDispatcher.Api.Projects;
using AgentDispatcher.Domain.Codex;
using AgentDispatcher.Domain.Dispatching;
using AgentDispatcher.Domain.Executions;
using AgentDispatcher.Domain.GitHub;
using AgentDispatcher.Domain.Health;
using AgentDispatcher.Domain.Maintenance;
using AgentDispatcher.Domain.Projects;
using AgentDispatcher.Domain.Routing;
using AgentDispatcher.Domain.Workspaces;
using AgentDispatcher.Infrastructure.Codex;
using AgentDispatcher.Infrastructure.Dispatching;
using AgentDispatcher.Infrastructure.GitHub;
using AgentDispatcher.Infrastructure.Health;
using AgentDispatcher.Infrastructure.Persistence;
using AgentDispatcher.Infrastructure.Processes;
using AgentDispatcher.Infrastructure.Workspaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var dataDirectory = builder.Configuration["AgentDispatcher:DataDirectory"];
if (string.IsNullOrWhiteSpace(dataDirectory))
{
    dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
}

var workerUser = builder.Configuration["AgentDispatcher:WorkerUser"];
if (string.IsNullOrWhiteSpace(workerUser))
{
    workerUser = "agent-dispatcher-worker";
}

var workerHomeDirectory = builder.Configuration["AgentDispatcher:WorkerHomeDirectory"];
if (string.IsNullOrWhiteSpace(workerHomeDirectory))
{
    workerHomeDirectory = $"/var/lib/{workerUser}";
}

var database = SqliteDatabase.FromFile(Path.Combine(dataDirectory, "agent-dispatcher.db"));
builder.Services.AddSingleton(database);
builder.Services.AddSingleton<IProjectRepository, SqliteProjectRepository>();
builder.Services.AddSingleton<IIssueSelectorRepository, SqliteIssueSelectorRepository>();
builder.Services.AddSingleton<IRoutingConfigurationRepository, SqliteRoutingConfigurationRepository>();
builder.Services.AddSingleton<IExecutionRepository, SqliteExecutionRepository>();
builder.Services.AddSingleton<IExecutionQueryRepository, SqliteExecutionQueryRepository>();
builder.Services.AddSingleton<IProjectScanStateRepository, SqliteProjectScanStateRepository>();
builder.Services.AddSingleton<IRetentionRepository, SqliteRetentionRepository>();
builder.Services.AddSingleton<SystemProcessRunner>();
builder.Services.AddSingleton<IProcessRunner>(
    provider => provider.GetRequiredService<SystemProcessRunner>());
builder.Services.AddSingleton<IWorkerProcessRunner>(
    provider => new SudoWorkerProcessRunner(
        provider.GetRequiredService<IProcessRunner>(),
        workerUser));
builder.Services.AddSingleton<IGitHubIssueSource, GitHubCliClient>();
builder.Services.AddSingleton<IGitWorkspace>(_ =>
    new GitWorktreeManager(
        _.GetRequiredService<IWorkerProcessRunner>(),
        dataDirectory));
builder.Services.AddSingleton<ICodexRunner>(_ =>
    new CodexCliRunner(
        _.GetRequiredService<IProcessRunner>(),
        dataDirectory,
        workerUser,
        workerHomeDirectory));
builder.Services.AddSingleton<IWorkerHealthProbe>(_ =>
    new SystemWorkerHealthProbe(
        _.GetRequiredService<IProcessRunner>(),
        _.GetRequiredService<IWorkerProcessRunner>(),
        workerUser));
builder.Services.AddSingleton<IDispatchCoordinator, DispatchCoordinator>();
builder.Services.AddSingleton<IHealthService>(provider =>
    new HealthService(
        provider.GetRequiredService<IProjectRepository>(),
        provider.GetRequiredService<IGitHubIssueSource>(),
        provider.GetRequiredService<IProcessRunner>(),
        provider.GetRequiredService<IWorkerProcessRunner>(),
        dataDirectory));

var app = builder.Build();

await database.InitializeAsync();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "AgentDispatcher.Api"
}));

app.MapProjectEndpoints();
app.MapProjectAutomationEndpoints();
app.MapDispatchEndpoints();
app.MapExecutionEndpoints();
app.MapMaintenanceEndpoints();
app.MapHealthEndpoints();

var indexFile = Path.Combine(app.Environment.WebRootPath ?? string.Empty, "index.html");
if (File.Exists(indexFile))
{
    app.MapFallbackToFile("index.html");
}

app.Run();

public partial class Program;
