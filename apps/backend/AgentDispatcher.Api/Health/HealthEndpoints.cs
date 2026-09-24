using AgentDispatcher.Domain.Health;

namespace AgentDispatcher.Api.Health;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/health");

        group.MapGet("/host", async (
            IHealthService health,
            CancellationToken cancellationToken) =>
        {
            var report = await health.CheckHostAsync(cancellationToken);
            return report.Available
                ? Results.Ok(report)
                : Results.Json(
                    report,
                    statusCode: StatusCodes.Status503ServiceUnavailable);
        });

        group.MapGet("/projects", async (
            IHealthService health,
            CancellationToken cancellationToken) =>
        {
            var reports = await health.CheckProjectsAsync(cancellationToken);
            return Results.Ok(reports);
        });

        group.MapGet("/projects/{id:guid}", async (
            Guid id,
            IHealthService health,
            CancellationToken cancellationToken) =>
        {
            var report = await health.CheckProjectAsync(id, cancellationToken);
            return report is null ? Results.NotFound() : Results.Ok(report);
        });

        return endpoints;
    }
}
