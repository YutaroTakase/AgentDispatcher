using AgentDispatcher.Domain.GitHub;
using AgentDispatcher.Domain.Projects;
using AgentDispatcher.Domain.Routing;

namespace AgentDispatcher.Domain.Executions;

public enum ExecutionStatus
{
    Queued,
    Preparing,
    Running,
    Succeeded,
    Failed,
    Canceled
}

public enum ExecutionTrigger
{
    Scheduled,
    Manual
}

public enum ExecutionCreateFailure
{
    None,
    DuplicateActiveIssue,
    ConcurrencyLimitReached
}

public sealed record ExecutionRecord(
    Guid Id,
    Guid ProjectId,
    int IssueNumber,
    string IssueTitle,
    IReadOnlyList<string> IssueLabels,
    ExecutionTrigger Trigger,
    string ModelIdentifier,
    string ReasoningEffort,
    Guid? MatchedRuleId,
    string? BaseRevision,
    string? WorktreePath,
    ExecutionStatus Status,
    string? ProcessIdentifier,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    int? ExitCode,
    string? FinalResult,
    string? StandardOutputLogPath,
    string? StandardErrorLogPath,
    string? FailureSummary);

public sealed record ExecutionEvent(
    long Id,
    Guid ExecutionId,
    ExecutionStatus Status,
    DateTimeOffset OccurredAt,
    string? Detail);

public sealed record ExecutionCreateResult(
    ExecutionRecord? Execution,
    ExecutionCreateFailure Failure)
{
    public bool Success => Execution is not null;

    public static ExecutionCreateResult Created(ExecutionRecord execution) =>
        new(execution, ExecutionCreateFailure.None);

    public static ExecutionCreateResult Rejected(ExecutionCreateFailure failure) =>
        new(null, failure);
}

public sealed record ExecutionTransitionData(
    string? BaseRevision = null,
    string? WorktreePath = null,
    string? ProcessIdentifier = null,
    int? ExitCode = null,
    string? FinalResult = null,
    string? StandardOutputLogPath = null,
    string? StandardErrorLogPath = null,
    string? FailureSummary = null,
    string? Detail = null);

public sealed record ExecutionQuery(
    Guid? ProjectId = null,
    ExecutionStatus? Status = null,
    string? ModelIdentifier = null,
    ExecutionTrigger? Trigger = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Limit = 100);

public interface IExecutionRepository
{
    Task<ExecutionCreateResult> TryCreateQueuedAsync(
        Project project,
        IssueCandidate issue,
        RoutingDecision routing,
        ExecutionTrigger trigger,
        CancellationToken cancellationToken = default);

    Task<ExecutionRecord?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExecutionRecord>> ListActiveByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<ExecutionRecord> TransitionAsync(
        Guid id,
        ExecutionStatus targetStatus,
        ExecutionTransitionData? data = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExecutionEvent>> ListEventsAsync(
        Guid executionId,
        CancellationToken cancellationToken = default);
}

public interface IExecutionQueryRepository
{
    Task<IReadOnlyList<ExecutionRecord>> ListAsync(
        ExecutionQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExecutionRecord>> ListQueuedAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);
}
