namespace AgentDispatcher.Domain.Dispatching;

public interface IProjectScanStateRepository
{
    Task<DateTimeOffset?> GetLastScanAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task SetLastScanAsync(
        Guid projectId,
        DateTimeOffset scannedAt,
        CancellationToken cancellationToken = default);
}
