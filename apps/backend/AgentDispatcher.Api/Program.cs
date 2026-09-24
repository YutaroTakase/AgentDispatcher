using AgentDispatcher.Api.Projects;
using AgentDispatcher.Domain.Projects;
using AgentDispatcher.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var dataDirectory = builder.Configuration["AgentDispatcher:DataDirectory"];
if (string.IsNullOrWhiteSpace(dataDirectory))
{
    dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
}

var database = SqliteDatabase.FromFile(Path.Combine(dataDirectory, "agent-dispatcher.db"));
builder.Services.AddSingleton(database);
builder.Services.AddSingleton<IProjectRepository, SqliteProjectRepository>();

var app = builder.Build();

await database.InitializeAsync();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "AgentDispatcher.Api"
}));

app.MapProjectEndpoints();

app.Run();

public partial class Program;
