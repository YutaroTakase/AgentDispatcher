var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "AgentDispatcher.Api"
}));

app.Run();

public partial class Program;
