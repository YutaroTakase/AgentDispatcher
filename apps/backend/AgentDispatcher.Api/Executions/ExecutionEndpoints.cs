using AgentDispatcher.Domain.Dispatching;
using AgentDispatcher.Domain.Executions;
using AgentDispatcher.Domain.Projects;

namespace AgentDispatcher.Api.Executions;

public static class ExecutionEndpoints
{
    public static IEndpointRouteBuilder MapExecutionEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/executions");

        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapGet("/{id:guid}/logs", GetLogsAsync);
        group.MapPost("/{id:guid}/cancel", CancelAsync);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid? projectId,
        ExecutionStatus? status,
        string? model,
        ExecutionTrigger? trigger,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int? limit,
        IExecutionQueryRepository queries,
        IProjectRepository projects,
        CancellationToken cancellationToken)
    {
        var query = new ExecutionQuery(
            projectId,
            status,
            model,
            trigger,
            from,
            to,
            limit ?? 100);

        IReadOnlyList<ExecutionRecord> executions;
        try
        {
            executions = await queries.ListAsync(query, cancellationToken);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }

        var projectMap = (await projects.ListAsync(cancellationToken))
            .ToDictionary(project => project.Id);

        return Results.Ok(executions.Select(execution =>
            ToSummary(execution, projectMap.GetValueOrDefault(execution.ProjectId))));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        IExecutionRepository executions,
        IProjectRepository projects,
        CancellationToken cancellationToken)
    {
        var execution = await executions.GetAsync(id, cancellationToken);
        if (execution is null)
        {
            return Results.NotFound();
        }

        var project = await projects.GetAsync(execution.ProjectId, cancellationToken);
        var events = await executions.ListEventsAsync(id, cancellationToken);

        return Results.Ok(new
        {
            Execution = ToSummary(execution, project),
            Events = events,
            execution.BaseRevision,
            execution.WorktreePath,
            execution.ProcessIdentifier,
            execution.ExitCode,
            execution.FinalResult,
            execution.FailureSummary,
            DurationSeconds = GetDurationSeconds(execution),
            HasStandardOutput = !string.IsNullOrWhiteSpace(execution.StandardOutputLogPath),
            HasStandardError = !string.IsNullOrWhiteSpace(execution.StandardErrorLogPath)
        });
    }

    private static async Task<IResult> GetLogsAsync(
        Guid id,
        IExecutionRepository executions,
        CancellationToken cancellationToken)
    {
        var execution = await executions.GetAsync(id, cancellationToken);
        if (execution is null)
        {
            return Results.NotFound();
        }

        var stdout = await ReadLogAsync(
            execution.StandardOutputLogPath,
            cancellationToken);
        var stderr = await ReadLogAsync(
            execution.StandardErrorLogPath,
            cancellationToken);

        return Results.Ok(new
        {
            StandardOutput = stdout,
            StandardError = stderr
        });
    }

    private static async Task<IResult> CancelAsync(
        Guid id,
        IExecutionRepository executions,
        IDispatchCoordinator dispatcher,
        CancellationToken cancellationToken)
    {
        var execution = await executions.GetAsync(id, cancellationToken);
        if (execution is null)
        {
            return Results.NotFound();
        }

        if (ExecutionStateMachine.IsTerminal(execution.Status))
        {
            return Results.Conflict(new
            {
                message = "終了済みの実行は取り消せません。"
            });
        }

        var canceled = await dispatcher.CancelAsync(id, cancellationToken);
        return canceled
            ? Results.Accepted($"/api/executions/{id}")
            : Results.Conflict();
    }

    private static object ToSummary(
        ExecutionRecord execution,
        Project? project)
    {
        var issueUrl = project is null
            ? null
            : $"https://github.com/{project.Repository}/issues/{execution.IssueNumber}";

        return new
        {
            execution.Id,
            execution.ProjectId,
            ProjectName = project?.DisplayName,
            execution.IssueNumber,
            execution.IssueTitle,
            IssueUrl = issueUrl,
            execution.IssueLabels,
            execution.Trigger,
            execution.ModelIdentifier,
            execution.ReasoningEffort,
            execution.MatchedRuleId,
            execution.Status,
            execution.CreatedAt,
            execution.StartedAt,
            execution.FinishedAt,
            DurationSeconds = GetDurationSeconds(execution),
            execution.ExitCode,
            execution.FailureSummary
        };
    }

    private static double? GetDurationSeconds(ExecutionRecord execution)
    {
        if (execution.StartedAt is null)
        {
            return null;
        }

        var end = execution.FinishedAt ?? DateTimeOffset.UtcNow;
        return Math.Max(0, (end - execution.StartedAt.Value).TotalSeconds);
    }

    private static async Task<string?> ReadLogAsync(
        string? path,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        return await File.ReadAllTextAsync(path, cancellationToken);
    }
}
