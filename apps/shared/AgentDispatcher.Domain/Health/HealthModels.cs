namespace AgentDispatcher.Domain.Health;

public enum HealthState
{
    Healthy,
    Warning,
    Unhealthy
}

public sealed record HealthCheck(
    string Key,
    string Name,
    HealthState State,
    string Detail);

public sealed record HostHealthReport(
    DateTimeOffset CheckedAt,
    IReadOnlyList<HealthCheck> Checks,
    long? FreeDiskBytes)
{
    public bool Available =>
        Checks.All(check => check.State != HealthState.Unhealthy);
}

public sealed record ProjectHealthReport(
    Guid ProjectId,
    string ProjectName,
    string Repository,
    DateTimeOffset CheckedAt,
    IReadOnlyList<HealthCheck> Checks,
    int WorktreeCount)
{
    public bool Available =>
        Checks.All(check => check.State != HealthState.Unhealthy);
}

public interface IHealthService
{
    Task<HostHealthReport> CheckHostAsync(
        CancellationToken cancellationToken = default);

    Task<ProjectHealthReport?> CheckProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectHealthReport>> CheckProjectsAsync(
        CancellationToken cancellationToken = default);
}
