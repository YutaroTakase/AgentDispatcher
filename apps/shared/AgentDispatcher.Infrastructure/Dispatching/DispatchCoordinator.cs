using AgentDispatcher.Domain.Codex;
using AgentDispatcher.Domain.Dispatching;
using AgentDispatcher.Domain.Executions;
using AgentDispatcher.Domain.GitHub;
using AgentDispatcher.Domain.Projects;
using AgentDispatcher.Domain.Routing;
using AgentDispatcher.Domain.Workspaces;

namespace AgentDispatcher.Infrastructure.Dispatching;

public sealed class DispatchCoordinator(
    IProjectRepository projects,
    IIssueSelectorRepository selectors,
    IRoutingConfigurationRepository routing,
    IExecutionRepository executions,
    IExecutionQueryRepository executionQueries,
    IGitHubIssueSource gitHub,
    IWorkerHealthProbe health,
    IGitWorkspace workspaces,
    ICodexRunner codex) : IDispatchCoordinator
{
    public async Task<DispatchResult> ScanProjectAsync(
        Guid projectId,
        ExecutionTrigger trigger,
        CancellationToken cancellationToken = default)
    {
        var project = await projects.GetAsync(projectId, cancellationToken);
        if (project is null)
        {
            return Result(new DispatchDecision(
                null,
                DispatchDecisionKind.ConfigurationMissing,
                "プロジェクトが見つかりません。"));
        }

        var guard = await CheckGuardsAsync(project, cancellationToken);
        if (guard is not null)
        {
            return Result(guard);
        }

        var selector = await selectors.GetAsync(projectId, cancellationToken);
        var defaultRoute = await routing.GetDefaultRouteAsync(projectId, cancellationToken);
        if (selector is null || defaultRoute is null)
        {
            return Result(new DispatchDecision(
                null,
                DispatchDecisionKind.ConfigurationMissing,
                "Issue取得条件または既定の推論設定がありません。"));
        }

        var search = await gitHub.SearchIssuesAsync(project, selector, cancellationToken);
        if (!search.Success)
        {
            return Result(new DispatchDecision(
                null,
                DispatchDecisionKind.GitHubUnavailable,
                search.Error ?? "GitHubからIssueを取得できませんでした。"));
        }

        var rules = await routing.ListRulesAsync(projectId, cancellationToken);
        var queued = new List<ExecutionRecord>();
        var decisions = new List<DispatchDecision>();

        foreach (var issue in search.Issues)
        {
            var decision = RoutingResolver.Resolve(defaultRoute, rules, issue.Labels);
            var create = await executions.TryCreateQueuedAsync(
                project,
                issue,
                decision,
                trigger,
                cancellationToken);

            if (create.Success)
            {
                queued.Add(create.Execution!);
                decisions.Add(new DispatchDecision(
                    issue.Number,
                    DispatchDecisionKind.Queued,
                    "実行待ちへ追加しました。",
                    create.Execution!.Id));
                continue;
            }

            decisions.Add(FromCreateFailure(issue.Number, create.Failure));
            if (create.Failure == ExecutionCreateFailure.ConcurrencyLimitReached)
            {
                break;
            }
        }

        return new DispatchResult(queued, decisions);
    }

    public async Task<DispatchDecision> QueueIssueAsync(
        Guid projectId,
        int issueNumber,
        ExecutionTrigger trigger,
        CancellationToken cancellationToken = default)
    {
        if (issueNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(issueNumber));
        }

        var project = await projects.GetAsync(projectId, cancellationToken);
        if (project is null)
        {
            return new DispatchDecision(
                issueNumber,
                DispatchDecisionKind.ConfigurationMissing,
                "プロジェクトが見つかりません。");
        }

        var guard = await CheckGuardsAsync(project, cancellationToken);
        if (guard is not null)
        {
            return guard with { IssueNumber = issueNumber };
        }

        var defaultRoute = await routing.GetDefaultRouteAsync(projectId, cancellationToken);
        if (defaultRoute is null)
        {
            return new DispatchDecision(
                issueNumber,
                DispatchDecisionKind.ConfigurationMissing,
                "既定の推論設定がありません。");
        }

        var lookup = await gitHub.GetIssueAsync(project, issueNumber, cancellationToken);
        if (!lookup.Success || lookup.Issue is null)
        {
            return new DispatchDecision(
                issueNumber,
                DispatchDecisionKind.IssueNotFound,
                lookup.Error ?? "対象Issueを取得できませんでした。");
        }

        var rules = await routing.ListRulesAsync(projectId, cancellationToken);
        var route = RoutingResolver.Resolve(defaultRoute, rules, lookup.Issue.Labels);
        var create = await executions.TryCreateQueuedAsync(
            project,
            lookup.Issue,
            route,
            trigger,
            cancellationToken);

        return create.Success
            ? new DispatchDecision(
                issueNumber,
                DispatchDecisionKind.Queued,
                "実行待ちへ追加しました。",
                create.Execution!.Id)
            : FromCreateFailure(issueNumber, create.Failure);
    }

    public async Task ProcessQueuedAsync(
        CancellationToken cancellationToken = default)
    {
        var queued = await executionQueries.ListQueuedAsync(
            limit: 100,
            cancellationToken);

        await Task.WhenAll(queued.Select(item =>
            ProcessOneAsync(item, cancellationToken)));
    }

    public async Task<bool> CancelAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        var execution = await executions.GetAsync(executionId, cancellationToken);
        if (execution is null || ExecutionStateMachine.IsTerminal(execution.Status))
        {
            return false;
        }

        if (execution.Status == ExecutionStatus.Running)
        {
            await codex.CancelAsync(executionId, cancellationToken);
        }

        await executions.TransitionAsync(
            executionId,
            ExecutionStatus.Canceled,
            new ExecutionTransitionData(Detail: "利用者が実行を取り消しました。"),
            cancellationToken);
        return true;
    }

    private async Task ProcessOneAsync(
        ExecutionRecord queued,
        CancellationToken cancellationToken)
    {
        try
        {
            var project = await projects.GetAsync(queued.ProjectId, cancellationToken)
                ?? throw new InvalidOperationException("対象プロジェクトが見つかりません。");

            var current = await executions.GetAsync(queued.Id, cancellationToken);
            if (current?.Status != ExecutionStatus.Queued)
            {
                return;
            }

            await executions.TransitionAsync(
                queued.Id,
                ExecutionStatus.Preparing,
                new ExecutionTransitionData(Detail: "実行準備を開始しました。"),
                cancellationToken);

            var preparation = await workspaces.PrepareAsync(
                project,
                queued.IssueNumber,
                cancellationToken);

            current = await executions.GetAsync(queued.Id, cancellationToken);
            if (current?.Status == ExecutionStatus.Canceled)
            {
                return;
            }

            var running = await executions.TransitionAsync(
                queued.Id,
                ExecutionStatus.Running,
                new ExecutionTransitionData(
                    BaseRevision: preparation.BaseRevision,
                    WorktreePath: preparation.WorktreePath,
                    ProcessIdentifier: codex.GetProcessIdentifier(queued.Id),
                    Detail: "Codex実行を開始しました。"),
                cancellationToken);

            var result = await codex.RunAsync(
                running,
                project,
                preparation.WorktreePath,
                cancellationToken);

            current = await executions.GetAsync(queued.Id, cancellationToken);
            if (current?.Status == ExecutionStatus.Canceled)
            {
                return;
            }

            if (result.ExitCode == 0)
            {
                await executions.TransitionAsync(
                    queued.Id,
                    ExecutionStatus.Succeeded,
                    new ExecutionTransitionData(
                        ExitCode: result.ExitCode,
                        FinalResult: result.FinalResult,
                        StandardOutputLogPath: result.StandardOutputLogPath,
                        StandardErrorLogPath: result.StandardErrorLogPath,
                        Detail: "Codex実行が正常終了しました。"),
                    cancellationToken);

                await workspaces.RemoveAsync(
                    project,
                    queued.IssueNumber,
                    cancellationToken);
            }
            else
            {
                await executions.TransitionAsync(
                    queued.Id,
                    ExecutionStatus.Failed,
                    new ExecutionTransitionData(
                        ExitCode: result.ExitCode,
                        FinalResult: result.FinalResult,
                        StandardOutputLogPath: result.StandardOutputLogPath,
                        StandardErrorLogPath: result.StandardErrorLogPath,
                        FailureSummary: $"Codexが終了コード {result.ExitCode} で終了しました。",
                        Detail: "Codex実行が失敗しました。"),
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var current = await executions.GetAsync(queued.Id, CancellationToken.None);
            if (current is not null && ExecutionStateMachine.IsActive(current.Status))
            {
                await executions.TransitionAsync(
                    queued.Id,
                    ExecutionStatus.Failed,
                    new ExecutionTransitionData(
                        FailureSummary: exception.Message,
                        Detail: "実行処理中に例外が発生しました。"),
                    CancellationToken.None);
            }
        }
    }

    private async Task<DispatchDecision?> CheckGuardsAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        if (!project.Enabled)
        {
            return new DispatchDecision(
                null,
                DispatchDecisionKind.ProjectDisabled,
                "プロジェクトが無効です。");
        }

        var workerHealth = await health.CheckAsync(cancellationToken);
        if (!workerHealth.Available)
        {
            return new DispatchDecision(
                null,
                DispatchDecisionKind.HealthUnavailable,
                workerHealth.Detail ?? "実行環境を利用できません。");
        }

        var repository = await gitHub.CheckRepositoryAsync(project, cancellationToken);
        if (!repository.Success)
        {
            return new DispatchDecision(
                null,
                DispatchDecisionKind.GitHubUnavailable,
                repository.Error ?? "GitHubリポジトリを利用できません。");
        }

        return null;
    }

    private static DispatchDecision FromCreateFailure(
        int issueNumber,
        ExecutionCreateFailure failure) =>
        failure switch
        {
            ExecutionCreateFailure.DuplicateActiveIssue => new DispatchDecision(
                issueNumber,
                DispatchDecisionKind.DuplicateActiveIssue,
                "同一Issueの実行が既に進行中です。"),
            ExecutionCreateFailure.ConcurrencyLimitReached => new DispatchDecision(
                issueNumber,
                DispatchDecisionKind.ConcurrencyLimitReached,
                "プロジェクトの最大同時実行数に達しています。"),
            _ => new DispatchDecision(
                issueNumber,
                DispatchDecisionKind.ConfigurationMissing,
                "実行待ちを作成できませんでした。")
        };

    private static DispatchResult Result(DispatchDecision decision) =>
        new([], [decision]);
}
