using AgentDispatcher.Domain.Executions;
using AgentDispatcher.Domain.GitHub;
using AgentDispatcher.Domain.Maintenance;
using AgentDispatcher.Domain.Projects;
using AgentDispatcher.Domain.Routing;
using AgentDispatcher.Domain.Workspaces;
using AgentDispatcher.Infrastructure.Maintenance;
using AgentDispatcher.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace AgentDispatcher.Tests;

public sealed class RetentionCleanupTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "AgentDispatcher.Retention.Tests",
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
    public async Task ExpiredFailedExecutionIsCleanedAndRunIsRecorded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var database = SqliteDatabase.FromFile(Path.Combine(_directory, "dispatcher.db"));
        await database.InitializeAsync(cancellationToken);

        var projects = new SqliteProjectRepository(database);
        var executions = new SqliteExecutionRepository(database);
        var retention = new SqliteRetentionRepository(database);

        var project = Project.Create(
            "テスト",
            "owner/repository",
            true,
            "main",
            5,
            1,
            1,
            0);
        await projects.AddAsync(project, cancellationToken);

        var create = await executions.TryCreateQueuedAsync(
            project,
            new IssueCandidate(10, "Issue", [], "https://example.test/10"),
            new RoutingDecision(ExecutionRoute.Create("model", "medium"), null),
            ExecutionTrigger.Manual,
            cancellationToken);
        var execution = Assert.IsType<ExecutionRecord>(create.Execution);

        await executions.TransitionAsync(
            execution.Id,
            ExecutionStatus.Preparing,
            cancellationToken: cancellationToken);
        await executions.TransitionAsync(
            execution.Id,
            ExecutionStatus.Running,
            new ExecutionTransitionData(
                BaseRevision: "abc",
                WorktreePath: "/tmp/worktree",
                ProcessIdentifier: "unit"),
            cancellationToken);
        await executions.TransitionAsync(
            execution.Id,
            ExecutionStatus.Failed,
            new ExecutionTransitionData(FailureSummary: "failure"),
            cancellationToken);

        var old = DateTimeOffset.UtcNow.AddDays(-2);
        await using (var connection = new SqliteConnection(database.ConnectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                "UPDATE executions SET finished_at_utc = $finishedAt WHERE id = $id;";
            command.Parameters.AddWithValue("$finishedAt", old.ToString("O"));
            command.Parameters.AddWithValue("$id", execution.Id.ToString());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var logDirectory = Path.Combine(
            _directory,
            "executions",
            execution.Id.ToString("N"));
        Directory.CreateDirectory(logDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(logDirectory, "stdout.log"),
            "log",
            cancellationToken);

        var workspace = new RecordingWorkspace();
        var cleanup = new RetentionCleanup(
            retention,
            projects,
            workspace,
            _directory);

        var run = await cleanup.RunAsync(DateTimeOffset.UtcNow, cancellationToken);

        Assert.Equal(1, run.WorktreesRemoved);
        Assert.Equal(1, run.ExecutionsDeleted);
        Assert.Equal(0, run.ErrorCount);
        Assert.False(Directory.Exists(logDirectory));
        Assert.Null(await executions.GetAsync(execution.Id, cancellationToken));
        Assert.Contains(10, workspace.RemovedIssues);

        var recorded = await retention.ListRecentCleanupRunsAsync(10, cancellationToken);
        Assert.Single(recorded);
    }

    [Fact]
    public async Task CleanupFailureKeepsExecutionForRetry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var database = SqliteDatabase.FromFile(Path.Combine(_directory, "retry.db"));
        await database.InitializeAsync(cancellationToken);

        var projects = new SqliteProjectRepository(database);
        var executions = new SqliteExecutionRepository(database);
        var retention = new SqliteRetentionRepository(database);
        var project = Project.Create(
            "テスト",
            "owner/repository",
            true,
            "main",
            5,
            1,
            1,
            0);
        await projects.AddAsync(project, cancellationToken);

        var create = await executions.TryCreateQueuedAsync(
            project,
            new IssueCandidate(11, "Issue", [], "https://example.test/11"),
            new RoutingDecision(ExecutionRoute.Create("model", "medium"), null),
            ExecutionTrigger.Manual,
            cancellationToken);
        var execution = Assert.IsType<ExecutionRecord>(create.Execution);
        await executions.TransitionAsync(execution.Id, ExecutionStatus.Preparing, cancellationToken: cancellationToken);
        await executions.TransitionAsync(
            execution.Id,
            ExecutionStatus.Running,
            new ExecutionTransitionData(BaseRevision: "abc", WorktreePath: "/tmp/worktree", ProcessIdentifier: "unit"),
            cancellationToken);
        await executions.TransitionAsync(execution.Id, ExecutionStatus.Failed, cancellationToken: cancellationToken);

        await using (var connection = new SqliteConnection(database.ConnectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                "UPDATE executions SET finished_at_utc = $finishedAt WHERE id = $id;";
            command.Parameters.AddWithValue("$finishedAt", DateTimeOffset.UtcNow.AddDays(-2).ToString("O"));
            command.Parameters.AddWithValue("$id", execution.Id.ToString());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var cleanup = new RetentionCleanup(
            retention,
            projects,
            new FailingWorkspace(),
            _directory);

        var run = await cleanup.RunAsync(DateTimeOffset.UtcNow, cancellationToken);

        Assert.Equal(1, run.ErrorCount);
        Assert.NotNull(await executions.GetAsync(execution.Id, cancellationToken));
    }

    private sealed class RecordingWorkspace : IGitWorkspace
    {
        public List<int> RemovedIssues { get; } = [];

        public Task<WorktreePreparation> PrepareAsync(
            Project project,
            int issueNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RemoveAsync(
            Project project,
            int issueNumber,
            CancellationToken cancellationToken = default)
        {
            RemovedIssues.Add(issueNumber);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingWorkspace : IGitWorkspace
    {
        public Task<WorktreePreparation> PrepareAsync(
            Project project,
            int issueNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RemoveAsync(
            Project project,
            int issueNumber,
            CancellationToken cancellationToken = default) =>
            throw new IOException("削除できません。");
    }
}
