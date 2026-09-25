using AgentDispatcher.Domain.Codex;
using AgentDispatcher.Domain.Executions;
using AgentDispatcher.Domain.GitHub;
using AgentDispatcher.Domain.Projects;
using AgentDispatcher.Domain.Recovery;
using AgentDispatcher.Domain.Routing;
using AgentDispatcher.Infrastructure.Persistence;
using AgentDispatcher.Infrastructure.Recovery;

namespace AgentDispatcher.Tests;

public sealed class ExecutionRecoveryTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "AgentDispatcher.Recovery.Tests",
        Guid.NewGuid().ToString("N"));

    public ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task InterruptedPreparingAndMissingRunningProcessBecomeFailed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var database = SqliteDatabase.FromFile(Path.Combine(_directory, "dispatcher.db"));
        await database.InitializeAsync(cancellationToken);

        var projects = new SqliteProjectRepository(database);
        var executions = new SqliteExecutionRepository(database);
        var queries = new SqliteExecutionQueryRepository(database);
        var project = Project.Create(
            "テスト",
            "owner/repository",
            true,
            "main",
            5,
            3,
            30,
            7);
        await projects.AddAsync(project, cancellationToken);

        var preparing = await CreateAsync(
            executions,
            project,
            10,
            cancellationToken);
        await executions.TransitionAsync(
            preparing.Id,
            ExecutionStatus.Preparing,
            cancellationToken: cancellationToken);

        var running = await CreateAsync(
            executions,
            project,
            11,
            cancellationToken);
        await executions.TransitionAsync(
            running.Id,
            ExecutionStatus.Preparing,
            cancellationToken: cancellationToken);
        await executions.TransitionAsync(
            running.Id,
            ExecutionStatus.Running,
            new ExecutionTransitionData(
                BaseRevision: "abc",
                WorktreePath: "/tmp/worktree",
                ProcessIdentifier: "unit"),
            cancellationToken);

        var recovery = new ExecutionRecovery(
            executions,
            queries,
            new FakeCodexRunner(isRunning: false));

        var result = await recovery.ReconcileAsync(cancellationToken);

        Assert.Equal(1, result.PreparingFailed);
        Assert.Equal(1, result.RunningFailed);
        Assert.Equal(
            ExecutionStatus.Failed,
            (await executions.GetAsync(preparing.Id, cancellationToken))!.Status);
        Assert.Equal(
            ExecutionStatus.Failed,
            (await executions.GetAsync(running.Id, cancellationToken))!.Status);
    }

    [Fact]
    public async Task ExistingRunningProcessRemainsRunning()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var database = SqliteDatabase.FromFile(Path.Combine(_directory, "active.db"));
        await database.InitializeAsync(cancellationToken);

        var projects = new SqliteProjectRepository(database);
        var executions = new SqliteExecutionRepository(database);
        var queries = new SqliteExecutionQueryRepository(database);
        var project = Project.Create(
            "テスト",
            "owner/repository",
            true,
            "main",
            5,
            2,
            30,
            7);
        await projects.AddAsync(project, cancellationToken);

        var running = await CreateAsync(executions, project, 20, cancellationToken);
        await executions.TransitionAsync(
            running.Id,
            ExecutionStatus.Preparing,
            cancellationToken: cancellationToken);
        await executions.TransitionAsync(
            running.Id,
            ExecutionStatus.Running,
            new ExecutionTransitionData(
                BaseRevision: "abc",
                WorktreePath: "/tmp/worktree",
                ProcessIdentifier: "unit"),
            cancellationToken);

        var recovery = new ExecutionRecovery(
            executions,
            queries,
            new FakeCodexRunner(isRunning: true));

        var result = await recovery.ReconcileAsync(cancellationToken);

        Assert.Equal(1, result.RunningStillActive);
        Assert.Equal(
            ExecutionStatus.Running,
            (await executions.GetAsync(running.Id, cancellationToken))!.Status);
    }

    private static async Task<ExecutionRecord> CreateAsync(
        IExecutionRepository executions,
        Project project,
        int issueNumber,
        CancellationToken cancellationToken)
    {
        var result = await executions.TryCreateQueuedAsync(
            project,
            new IssueCandidate(
                issueNumber,
                $"Issue {issueNumber}",
                [],
                $"https://example.test/{issueNumber}"),
            new RoutingDecision(
                ExecutionRoute.Create("model", "medium"),
                null),
            ExecutionTrigger.Manual,
            cancellationToken);

        return Assert.IsType<ExecutionRecord>(result.Execution);
    }

    private sealed class FakeCodexRunner(bool isRunning) : ICodexRunner
    {
        public string GetProcessIdentifier(Guid executionId) =>
            $"unit-{executionId:N}";

        public Task<bool> IsRunningAsync(
            Guid executionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(isRunning);

        public Task<CodexRunResult> RunAsync(
            ExecutionRecord execution,
            Project project,
            string worktreePath,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task CancelAsync(
            Guid executionId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
