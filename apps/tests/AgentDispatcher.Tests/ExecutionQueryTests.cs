using AgentDispatcher.Domain.Executions;
using AgentDispatcher.Domain.GitHub;
using AgentDispatcher.Domain.Projects;
using AgentDispatcher.Domain.Routing;
using AgentDispatcher.Infrastructure.Persistence;

namespace AgentDispatcher.Tests;

public sealed class ExecutionQueryTests : IAsyncLifetime
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
    public async Task ExecutionListCanBeFilteredAndQueuedItemsCanBeRead()
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
            2,
            30,
            7);
        await projects.AddAsync(project, cancellationToken);

        var route = new RoutingDecision(
            ExecutionRoute.Create("model-a", "medium"),
            null);

        var created = await executions.TryCreateQueuedAsync(
            project,
            new IssueCandidate(10, "Issue 10", ["agent"], "https://example.test/10"),
            route,
            ExecutionTrigger.Manual,
            cancellationToken);

        Assert.True(created.Success);

        var queued = await queries.ListQueuedAsync(10, cancellationToken);
        Assert.Single(queued);

        var filtered = await queries.ListAsync(
            new ExecutionQuery(
                ProjectId: project.Id,
                Status: ExecutionStatus.Queued,
                ModelIdentifier: "model-a",
                Trigger: ExecutionTrigger.Manual,
                Limit: 10),
            cancellationToken);

        var item = Assert.Single(filtered);
        Assert.Equal(10, item.IssueNumber);
    }

    [Fact]
    public async Task ProjectScanTimestampPersists()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var database = SqliteDatabase.FromFile(Path.Combine(_directory, "scan.db"));
        await database.InitializeAsync(cancellationToken);

        var projects = new SqliteProjectRepository(database);
        var scanStates = new SqliteProjectScanStateRepository(database);
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

        var timestamp = DateTimeOffset.UtcNow;
        await scanStates.SetLastScanAsync(project.Id, timestamp, cancellationToken);

        var restored = await scanStates.GetLastScanAsync(project.Id, cancellationToken);
        Assert.NotNull(restored);
        Assert.Equal(timestamp.ToUnixTimeMilliseconds(), restored.Value.ToUnixTimeMilliseconds());
    }
}
