using AgentDispatcher.Domain.Projects;

namespace AgentDispatcher.Domain.Workspaces;

public sealed record WorktreePreparation(
    string RepositoryPath,
    string WorktreePath,
    string BaseRevision);

public interface IGitWorkspace
{
    Task<WorktreePreparation> PrepareAsync(
        Project project,
        int issueNumber,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        Project project,
        int issueNumber,
        CancellationToken cancellationToken = default);
}
