namespace AgentDispatcher.Domain.Dispatching;

public sealed record WorkerHealthResult(
    bool Available,
    string? Detail);

public interface IWorkerHealthProbe
{
    Task<WorkerHealthResult> CheckAsync(
        CancellationToken cancellationToken = default);
}
