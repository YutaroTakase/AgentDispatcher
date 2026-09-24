using AgentDispatcher.Domain.Executions;

namespace AgentDispatcher.Domain.Dispatching;

public enum DispatchDecisionKind
{
    Queued,
    ProjectDisabled,
    HealthUnavailable,
    ConfigurationMissing,
    IssueNotFound,
    DuplicateActiveIssue,
    ConcurrencyLimitReached,
    GitHubUnavailable
}

public sealed record DispatchDecision(
    int? IssueNumber,
    DispatchDecisionKind Kind,
    string Detail,
    Guid? ExecutionId = null);

public sealed record DispatchResult(
    IReadOnlyList<ExecutionRecord> QueuedExecutions,
    IReadOnlyList<DispatchDecision> Decisions)
{
    public int QueuedCount => QueuedExecutions.Count;
}

public interface IDispatchCoordinator
{
    Task<DispatchResult> ScanProjectAsync(
        Guid projectId,
        ExecutionTrigger trigger,
        CancellationToken cancellationToken = default);

    Task<DispatchDecision> QueueIssueAsync(
        Guid projectId,
        int issueNumber,
        ExecutionTrigger trigger,
        CancellationToken cancellationToken = default);

    Task ProcessQueuedAsync(CancellationToken cancellationToken = default);

    Task<bool> CancelAsync(
        Guid executionId,
        CancellationToken cancellationToken = default);
}
