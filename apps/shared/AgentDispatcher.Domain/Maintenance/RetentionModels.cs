using AgentDispatcher.Domain.Executions;

namespace AgentDispatcher.Domain.Maintenance;

public sealed record RetentionCandidate(
    Guid ExecutionId,
    Guid ProjectId,
    int IssueNumber,
    ExecutionStatus Status,
    DateTimeOffset FinishedAt,
    int ExecutionRetentionDays,
    int FailureWorktreeRetentionDays);

public sealed record CleanupRunRecord(
    long Id,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    int WorktreesRemoved,
    int ExecutionsDeleted,
    int ErrorCount,
    IReadOnlyList<string> Errors);

public interface IRetentionRepository
{
    Task<IReadOnlyList<RetentionCandidate>> ListCandidatesAsync(
        CancellationToken cancellationToken = default);

    Task<bool> DeleteExecutionAsync(
        Guid executionId,
        CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> GetLastCleanupAtAsync(
        CancellationToken cancellationToken = default);

    Task<CleanupRunRecord> RecordCleanupAsync(
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        int worktreesRemoved,
        int executionsDeleted,
        IReadOnlyList<string> errors,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CleanupRunRecord>> ListRecentCleanupRunsAsync(
        int limit = 20,
        CancellationToken cancellationToken = default);
}

public interface IRetentionCleanup
{
    Task<CleanupRunRecord?> RunIfDueAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<CleanupRunRecord> RunAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
