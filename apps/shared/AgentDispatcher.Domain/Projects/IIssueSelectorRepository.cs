namespace AgentDispatcher.Domain.Projects;

public interface IIssueSelectorRepository
{
    Task<IssueSelectorSettings?> GetAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(
        IssueSelectorSettings settings,
        CancellationToken cancellationToken = default);
}
