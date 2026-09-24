using AgentDispatcher.Domain.Maintenance;

namespace AgentDispatcher.Api.Maintenance;

public static class MaintenanceEndpoints
{
    public static IEndpointRouteBuilder MapMaintenanceEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/maintenance/cleanup-runs", async (
            int? limit,
            IRetentionRepository repository,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var items = await repository.ListRecentCleanupRunsAsync(
                    limit ?? 20,
                    cancellationToken);
                return Results.Ok(items);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                return Results.BadRequest(new { message = exception.Message });
            }
        });

        return endpoints;
    }
}
