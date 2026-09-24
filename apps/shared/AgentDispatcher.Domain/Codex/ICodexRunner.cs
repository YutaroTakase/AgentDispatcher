using AgentDispatcher.Domain.Executions;
using AgentDispatcher.Domain.Projects;

namespace AgentDispatcher.Domain.Codex;

public sealed record CodexRunResult(
    int ExitCode,
    string FinalResult,
    string StandardOutputLogPath,
    string StandardErrorLogPath,
    string ProcessIdentifier);

public interface ICodexRunner
{
    string GetProcessIdentifier(Guid executionId);

    Task<CodexRunResult> RunAsync(
        ExecutionRecord execution,
        Project project,
        string worktreePath,
        CancellationToken cancellationToken = default);

    Task<bool> IsRunningAsync(
        Guid executionId,
        CancellationToken cancellationToken = default);

    Task CancelAsync(
        Guid executionId,
        CancellationToken cancellationToken = default);
}
