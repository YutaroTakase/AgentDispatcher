using AgentDispatcher.Domain.Executions;
using AgentDispatcher.Domain.GitHub;
using AgentDispatcher.Domain.Projects;
using AgentDispatcher.Domain.Routing;
using AgentDispatcher.Infrastructure.Persistence;

namespace AgentDispatcher.Tests;

public sealed class ExecutionPersistenceTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "AgentDispatcher.Tests",
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
    public async Task DuplicateIssueAndConcurrencyLimitAreEnforced()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var database = SqliteDatabase.FromFile(
            Path.Combine(_directory, "agent-dispatcher.db"));
        await database.InitializeAsync(cancellationToken);

        var projects = new SqliteProjectRepository(database);
        var executions = new SqliteExecutionRepository(database);
        var project = Project.Create(
            "テスト",
            "owner/repository",
            true,
            "main",
            5,
            1,
            30,
            7);
        await projects.AddAsync(project, cancellationToken);

        var route = new RoutingDecision(
            ExecutionRoute.Create("test-model", "medium"),
            null);
        var issue1 = new IssueCandidate(
            10,
            "Issue 10",
            ["agent"],
            "https://github.com/owner/repository/issues/10");
        var issue2 = new IssueCandidate(
            11,
            "Issue 11",
            ["agent"],
            "https://github.com/owner/repository/issues/11");

        var first = await executions.TryCreateQueuedAsync(
            project,
            issue1,
            route,
            ExecutionTrigger.Scheduled,
            cancellationToken);
        var duplicate = await executions.TryCreateQueuedAsync(
            project,
            issue1,
            route,
            ExecutionTrigger.Manual,
            cancellationToken);
        var overLimit = await executions.TryCreateQueuedAsync(
            project,
            issue2,
            route,
            ExecutionTrigger.Scheduled,
            cancellationToken);

        Assert.True(first.Success);
        Assert.Equal(
            ExecutionCreateFailure.DuplicateActiveIssue,
            duplicate.Failure);
        Assert.Equal(
            ExecutionCreateFailure.ConcurrencyLimitReached,
            overLimit.Failure);
    }

    [Fact]
    public async Task StateTransitionsAndEventsArePersisted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var database = SqliteDatabase.FromFile(
            Path.Combine(_directory, "state.db"));
        await database.InitializeAsync(cancellationToken);

        var projects = new SqliteProjectRepository(database);
        var executions = new SqliteExecutionRepository(database);
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

        var created = await executions.TryCreateQueuedAsync(
            project,
            new IssueCandidate(20, "Issue 20", [], "https://example.test/20"),
            new RoutingDecision(
                ExecutionRoute.Create("test-model", "high"),
                Guid.NewGuid()),
            ExecutionTrigger.Manual,
            cancellationToken);

        var execution = Assert.IsType<ExecutionRecord>(created.Execution);

        await executions.TransitionAsync(
            execution.Id,
            ExecutionStatus.Preparing,
            new ExecutionTransitionData(Detail: "準備開始"),
            cancellationToken);

        var running = await executions.TransitionAsync(
            execution.Id,
            ExecutionStatus.Running,
            new ExecutionTransitionData(
                BaseRevision: "abc123",
                WorktreePath: "/tmp/worktree",
                ProcessIdentifier: "unit-1"),
            cancellationToken);

        var succeeded = await executions.TransitionAsync(
            execution.Id,
            ExecutionStatus.Succeeded,
            new ExecutionTransitionData(
                ExitCode: 0,
                FinalResult: "完了",
                StandardOutputLogPath: "/tmp/stdout.log",
                StandardErrorLogPath: "/tmp/stderr.log"),
            cancellationToken);

        Assert.Equal(ExecutionStatus.Running, running.Status);
        Assert.NotNull(running.StartedAt);
        Assert.Equal("abc123", running.BaseRevision);
        Assert.Equal(ExecutionStatus.Succeeded, succeeded.Status);
        Assert.NotNull(succeeded.FinishedAt);
        Assert.Equal(0, succeeded.ExitCode);

        var events = await executions.ListEventsAsync(
            execution.Id,
            cancellationToken);
        Assert.Equal(
            [
                ExecutionStatus.Queued,
                ExecutionStatus.Preparing,
                ExecutionStatus.Running,
                ExecutionStatus.Succeeded
            ],
            events.Select(item => item.Status));

        var retry = await executions.TryCreateQueuedAsync(
            project,
            new IssueCandidate(20, "Issue 20 retry", [], "https://example.test/20"),
            new RoutingDecision(
                ExecutionRoute.Create("test-model", "medium"),
                null),
            ExecutionTrigger.Manual,
            cancellationToken);
        Assert.True(retry.Success);
    }

    [Fact]
    public void TerminalStateCannotTransitionAgain()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ExecutionStateMachine.EnsureTransition(
                ExecutionStatus.Succeeded,
                ExecutionStatus.Running));
    }
}
