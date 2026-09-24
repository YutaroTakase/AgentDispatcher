using AgentDispatcher.Domain.Projects;
using AgentDispatcher.Infrastructure.Processes;
using AgentDispatcher.Infrastructure.Workspaces;

namespace AgentDispatcher.Tests;

public sealed class GitWorktreeManagerTests : IAsyncLifetime
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
    public async Task PrepareCreatesManagedRepositoryAndIssueWorktree()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var runner = new QueueProcessRunner(
            new ProcessResult(0, string.Empty, string.Empty),
            new ProcessResult(0, string.Empty, string.Empty),
            new ProcessResult(0, "abc123\n", string.Empty),
            new ProcessResult(0, string.Empty, string.Empty),
            new ProcessResult(0, string.Empty, string.Empty));
        var manager = new GitWorktreeManager(runner, _directory);
        var project = Project.Create(
            "テスト",
            "owner/repository",
            true,
            "main",
            5,
            2,
            30,
            7);

        var result = await manager.PrepareAsync(
            project,
            42,
            cancellationToken);

        Assert.Equal("abc123", result.BaseRevision);
        Assert.EndsWith(
            Path.Combine(project.Id.ToString("N"), "42"),
            result.WorktreePath);

        Assert.Collection(
            runner.Commands,
            command =>
            {
                Assert.Equal("gh", command.FileName);
                Assert.Contains("clone", command.Arguments);
                Assert.Contains("owner/repository", command.Arguments);
            },
            command =>
            {
                Assert.Equal("git", command.FileName);
                Assert.Equal("fetch", command.Arguments[2]);
            },
            command =>
            {
                Assert.Equal("git", command.FileName);
                Assert.Equal("rev-parse", command.Arguments[2]);
            },
            command => Assert.Equal("prune", command.Arguments[^1]),
            command =>
            {
                Assert.Equal("git", command.FileName);
                Assert.Equal("add", command.Arguments[3]);
                Assert.Contains("agent-dispatcher/issue-42", command.Arguments);
                Assert.Contains("abc123", command.Arguments);
            });
    }

    private sealed class QueueProcessRunner(params ProcessResult[] results) : IProcessRunner
    {
        private readonly Queue<ProcessResult> _results = new(results);

        public List<RecordedCommand> Commands { get; } = [];

        public Task<ProcessResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string? workingDirectory = null,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(new RecordedCommand(fileName, arguments.ToArray()));
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed record RecordedCommand(
        string FileName,
        IReadOnlyList<string> Arguments);
}
