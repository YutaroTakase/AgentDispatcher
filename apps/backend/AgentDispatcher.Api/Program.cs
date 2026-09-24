using AgentDispatcher.Api.Projects;
using AgentDispatcher.Domain.GitHub;
using AgentDispatcher.Domain.Projects;
using AgentDispatcher.Domain.Routing;
using AgentDispatcher.Infrastructure.GitHub;
using AgentDispatcher.Infrastructure.Persistence;
using AgentDispatcher.Infrastructure.Processes;

var builder = WebApplication.CreateBuilder(args);

var dataDirectory = builder.Configuration["AgentDispatcher:DataDirectory"];
if (string.IsNullOrWhiteSpace(dataDirectory))
{
    dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
}

var database = SqliteDatabase.FromFile(Path.Combine(dataDirectory, "agent-dispatcher.db"));
builder.Services.AddSingleton(database);
builder.Services.AddSingleton<IProjectRepository, SqliteProjectRepository>();
builder.Services.AddSingleton<IIssueSelectorRepository, SqliteIssueSelectorRepository>();
builder.Services.AddSingleton<IRoutingConfigurationRepository, SqliteRoutingConfigurationRepository>();
builder.Services.AddSingleton<IProcessRunner, SystemProcessRunner>();
builder.Services.AddSingleton<IGitHubIssueSource, GitHubCliClient>();

var app = builder.Build();

await database.InitializeAsync();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "AgentDispatcher.Api"
}));

app.MapProjectEndpoints();
app.MapProjectAutomationEndpoints();

app.Run();

public partial class Program;
