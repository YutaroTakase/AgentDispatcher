using AgentDispatcher.Domain.Dispatching;
using AgentDispatcher.Domain.Executions;

namespace AgentDispatcher.Api.Projects;

public static class DispatchEndpoints
{
    public static IEndpointRouteBuilder MapDispatchEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/projects/{id:guid}");

        group.MapPost("/scan", async (
            Guid id,
            IDispatchCoordinator dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.ScanProjectAsync(
                id,
                ExecutionTrigger.Manual,
                cancellationToken);

            return Results.Ok(result);
        });

        group.MapPost("/issues/{issueNumber:int}/dispatch", async (
            Guid id,
            int issueNumber,
            IDispatchCoordinator dispatcher,
            CancellationToken cancellationToken) =>
        {
            var decision = await dispatcher.QueueIssueAsync(
                id,
                issueNumber,
                ExecutionTrigger.Manual,
                cancellationToken);

            return decision.Kind == DispatchDecisionKind.Queued
                ? Results.Accepted(
                    $"/api/executions/{decision.ExecutionId}",
                    decision)
                : Results.Conflict(decision);
        });

        return endpoints;
    }
}
