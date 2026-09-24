using AgentDispatcher.Domain.GitHub;
using AgentDispatcher.Domain.Health;
using AgentDispatcher.Domain.Projects;
using AgentDispatcher.Infrastructure.Health;
using AgentDispatcher.Infrastructure.Persistence;
using AgentDispatcher.Infrastructure.Processes;

namespace AgentDispatcher.Tests;

public sealed class HealthServiceTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "AgentDispatcher.Health.Tests",
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
    public async Task HostAndProjectHealthAreReported()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var database = SqliteDatabase.FromFile(Path.Combine(_directory, "dispatcher.db"));
        await database.InitializeAsync(cancellationToken);
        var projects = new SqliteProjectRepository(database);

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

        var runner = new SuccessfulProcessRunner();
        var service = new HealthService(
            projects,
            new SuccessfulGitHubSource(),
            runner,
            runner,
            _directory);

        var host = await service.CheckHostAsync(cancellationToken);
        var projectHealth = await service.CheckProjectAsync(
            project.Id,
            cancellationToken);

        Assert.True(host.Available);
        Assert.Contains(host.Checks, check =>
            check.Key == "codex-auth" &&
            check.State == HealthState.Healthy);

        Assert.NotNull(projectHealth);
        Assert.True(projectHealth.Available);
        Assert.Contains(projectHealth.Checks, check =>
            check.Key == "managed-repository" &&
            check.State == HealthState.Warning);
        Assert.Contains(projectHealth.Checks, check =>
            check.Key == "worktree-root" &&
            check.State == HealthState.Healthy);
    }

    private sealed class SuccessfulProcessRunner :
        IProcessRunner,
        IWorkerProcessRunner
    {
        public Task<ProcessResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string? workingDirectory = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProcessResult(0, "ok\n", string.Empty));
    }

    private sealed class SuccessfulGitHubSource : IGitHubIssueSource
    {
        public Task<RepositoryCheckResult> CheckRepositoryAsync(
            Project project,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RepositoryCheckResult(
                true,
                project.DefaultBranch,
                null));

        public Task<IssueSearchResult> SearchIssuesAsync(
            Project project,
            IssueSelectorSettings selector,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(IssueSearchResult.Succeeded([]));

        public Task<IssueLookupResult> GetIssueAsync(
            Project project,
            int issueNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(IssueLookupResult.Failed("未使用"));
    }
}
