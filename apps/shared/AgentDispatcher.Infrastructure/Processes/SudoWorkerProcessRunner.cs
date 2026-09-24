namespace AgentDispatcher.Infrastructure.Processes;

public sealed class SudoWorkerProcessRunner(
    IProcessRunner systemProcessRunner,
    string workerUser) : IWorkerProcessRunner
{
    public Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(workerUser);
        ArgumentNullException.ThrowIfNull(arguments);

        var sudoArguments = new List<string>
        {
            "--non-interactive",
            "--set-home",
            "--user",
            workerUser,
            "--",
            fileName
        };
        sudoArguments.AddRange(arguments);

        return systemProcessRunner.RunAsync(
            "sudo",
            sudoArguments,
            workingDirectory,
            cancellationToken);
    }
}
