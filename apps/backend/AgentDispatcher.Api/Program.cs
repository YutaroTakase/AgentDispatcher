using AgentDispatcher.Api.Executions;
using AgentDispatcher.Api.Maintenance;
using AgentDispatcher.Api.Projects;
using AgentDispatcher.Domain.Codex;
using AgentDispatcher.Domain.Dispatching;
using AgentDispatcher.Domain.Executions;
using AgentDispatcher.Domain.GitHub;
using AgentDispatcher.Domain.Maintenance;
using AgentDispatcher.Domain.Projects;
using AgentDispatcher.Domain.Routing;
using AgentDispatcher.Domain.Workspaces;
using AgentDispatcher.Infrastructure.Codex;
using AgentDispatcher.Infrastructure.Dispatching;
using AgentDispatcher.Infrastructure.GitHub;
using AgentDispatcher.Infrastructure.Persistence;
using AgentDispatcher.Infrastructure.Processes;
using AgentDispatcher.Infrastructure.Workspaces;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddSingleton<IProcessRunner, SystemProcessRunner>();
builder.Services.AddSingleton<IGitHubIssueSource, GitHubCliClient>();
builder.Services.AddSingleton<IGitWorkspace>(_ =>
    new GitWorktreeManager(
        _.GetRequiredService<IProcessRunner>(),
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
        workerUser));
builder.Services.AddSingleton<IDispatchCoordinator, DispatchCoordinator>();

var app = builder.Build();

await database.InitializeAsync();

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

app.Run();

public partial class Program;
