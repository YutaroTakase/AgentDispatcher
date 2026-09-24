using AgentDispatcher.Domain.Codex;
using AgentDispatcher.Domain.Executions;
using AgentDispatcher.Domain.Projects;
using AgentDispatcher.Infrastructure.Processes;

namespace AgentDispatcher.Infrastructure.Codex;

public sealed class CodexCliRunner(
    IProcessRunner processRunner,
    string dataDirectory,
    string workerUser,
    string workerHomeDirectory) : ICodexRunner
{
    public string GetProcessIdentifier(Guid executionId) =>
        $"agent-dispatcher-codex-{executionId:N}";

    public async Task<CodexRunResult> RunAsync(
        ExecutionRecord execution,
        Project project,
        string worktreePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(worktreePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workerUser);
        ArgumentException.ThrowIfNullOrWhiteSpace(workerHomeDirectory);

        var unitName = GetProcessIdentifier(execution.Id);
        var prompt =
            $"GitHubリポジトリ {project.Repository} の Issue #{execution.IssueNumber} を処理してください。" +
            "まずリポジトリ内の指示文書とGitHub上のIssue内容を確認し、それらを正本として作業してください。";

        var reasoningConfiguration =
            $"model_reasoning_effort=\"{execution.ReasoningEffort}\"";

        var arguments = new List<string>
        {
            "--quiet",
            "--wait",
            "--pipe",
            "--collect",
            $"--unit={unitName}",
            $"--uid={workerUser}",
            $"--setenv=HOME={Path.GetFullPath(workerHomeDirectory)}",
            $"--setenv=USER={workerUser}",
            $"--setenv=LOGNAME={workerUser}",
            $"--working-directory={Path.GetFullPath(worktreePath)}",
            "codex",
            "exec",
            "--full-auto",
            "--model",
            execution.ModelIdentifier,
            "--config",
            reasoningConfiguration,
            "--config",
            "sandbox_workspace_write.network_access=true",
            prompt
        };

        ProcessResult result;
        try
        {
            result = await processRunner.RunAsync(
                "sudo",
                ["--non-interactive", "systemd-run", .. arguments],
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await CancelAsync(execution.Id, CancellationToken.None);
            throw;
        }

        var logDirectory = Path.Combine(
            Path.GetFullPath(dataDirectory),
            "executions",
            execution.Id.ToString("N"));
        Directory.CreateDirectory(logDirectory);

        var stdoutPath = Path.Combine(logDirectory, "stdout.log");
        var stderrPath = Path.Combine(logDirectory, "stderr.log");

        await File.WriteAllTextAsync(
            stdoutPath,
            result.StandardOutput,
            cancellationToken);
        await File.WriteAllTextAsync(
            stderrPath,
            result.StandardError,
            cancellationToken);

        return new CodexRunResult(
            result.ExitCode,
            result.StandardOutput.Trim(),
            stdoutPath,
            stderrPath,
            unitName);
    }

    public async Task CancelAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        var result = await processRunner.RunAsync(
            "sudo",
            ["--non-interactive", "systemctl", "stop", GetProcessIdentifier(executionId)],
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0 &&
            !result.StandardError.Contains(
                "not loaded",
                StringComparison.OrdinalIgnoreCase) &&
            !result.StandardError.Contains(
                "not found",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Codex実行を停止できませんでした。 {result.StandardError.Trim()}");
        }
    }
}
