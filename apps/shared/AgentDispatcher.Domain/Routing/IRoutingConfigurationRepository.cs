namespace AgentDispatcher.Domain.Routing;

public interface IRoutingConfigurationRepository
{
    Task<ExecutionRoute?> GetDefaultRouteAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task UpsertDefaultRouteAsync(
        Guid projectId,
        ExecutionRoute route,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoutingRule>> ListRulesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task ReplaceRulesAsync(
        Guid projectId,
        IReadOnlyList<RoutingRule> rules,
        CancellationToken cancellationToken = default);
}
