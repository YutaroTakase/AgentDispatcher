using AgentDispatcher.Domain.Executions;
using AgentDispatcher.Domain.Projects;
using AgentDispatcher.Infrastructure.Codex;
using AgentDispatcher.Infrastructure.Processes;

namespace AgentDispatcher.Tests;

public sealed class CodexCliRunnerTests : IAsyncLifetime
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
    public async Task RunUsesDedicatedUserModelReasoningAndNetworkAccess()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var runner = new RecordingProcessRunner(
            new ProcessResult(0, "実装完了\n", "progress\n"));
        var codex = new CodexCliRunner(
            runner,
            _directory,
            "agent-worker",
            "/home/agent-worker");
        var project = Project.Create(
            "テスト",
            "owner/repository",
            true,
            "main",
            5,
            2,
            30,
            7);
        var execution = new ExecutionRecord(
            Guid.NewGuid(),
            project.Id,
            50,
            "Issue 50",
            ["agent"],
            ExecutionTrigger.Manual,
            "selected-model",
            "high",
            null,
            null,
            null,
            ExecutionStatus.Preparing,
            null,
            DateTimeOffset.UtcNow,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        var worktree = Path.Combine(_directory, "worktree");
        Directory.CreateDirectory(worktree);

        var result = await codex.RunAsync(
            execution,
            project,
            worktree,
            cancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("実装完了", result.FinalResult);
        Assert.True(File.Exists(result.StandardOutputLogPath));
        Assert.True(File.Exists(result.StandardErrorLogPath));
        Assert.Equal("sudo", runner.FileName);
        Assert.Contains("systemd-run", runner.Arguments);
        Assert.Contains("--uid=agent-worker", runner.Arguments);
        Assert.Contains("--setenv=HOME=/home/agent-worker", runner.Arguments);
        Assert.Contains("--full-auto", runner.Arguments);
        Assert.Contains("--model", runner.Arguments);
        Assert.Contains("selected-model", runner.Arguments);
        Assert.Contains(
            "model_reasoning_effort=\"high\"",
            runner.Arguments);
        Assert.Contains(
            "sandbox_workspace_write.network_access=true",
            runner.Arguments);
        Assert.Contains(
            runner.Arguments,
            value => value.Contains("Issue #50", StringComparison.Ordinal));
    }

    [Fact]
    public void UnsupportedReasoningEffortIsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            AgentDispatcher.Domain.Routing.ExecutionRoute.Create(
                "model",
                "invalid"));
    }

    private sealed class RecordingProcessRunner(ProcessResult result) : IProcessRunner
    {
        public string? FileName { get; private set; }

        public IReadOnlyList<string> Arguments { get; private set; } = [];

        public Task<ProcessResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string? workingDirectory = null,
            CancellationToken cancellationToken = default)
        {
            FileName = fileName;
            Arguments = arguments.ToArray();
            return Task.FromResult(result);
        }
    }
}
