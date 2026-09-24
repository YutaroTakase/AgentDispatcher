using AgentDispatcher.Infrastructure.Processes;

namespace AgentDispatcher.Tests;

public sealed class SudoWorkerProcessRunnerTests
{
    [Fact]
    public async Task WorkerCommandRunsAsDedicatedUser()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var system = new RecordingProcessRunner();
        var runner = new SudoWorkerProcessRunner(system, "agent-dispatcher-worker");

        await runner.RunAsync(
            "gh",
            ["auth", "status"],
            "/tmp/work",
            cancellationToken);

        Assert.Equal("sudo", system.FileName);
        Assert.Equal("/tmp/work", system.WorkingDirectory);
        Assert.Equal(
            [
                "--non-interactive",
                "--set-home",
                "--user",
                "agent-dispatcher-worker",
                "--",
                "gh",
                "auth",
                "status"
            ],
            system.Arguments);
    }

    private sealed class RecordingProcessRunner : IProcessRunner
    {
        public string? FileName { get; private set; }
        public string? WorkingDirectory { get; private set; }
        public IReadOnlyList<string> Arguments { get; private set; } = [];

        public Task<ProcessResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string? workingDirectory = null,
            CancellationToken cancellationToken = default)
        {
            FileName = fileName;
            WorkingDirectory = workingDirectory;
            Arguments = arguments.ToArray();
            return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
        }
    }
}
