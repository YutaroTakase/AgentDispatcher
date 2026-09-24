using AgentDispatcher.Domain.Dispatching;
using AgentDispatcher.Infrastructure.Processes;

namespace AgentDispatcher.Infrastructure.Dispatching;

public sealed class SystemWorkerHealthProbe(
    IProcessRunner systemProcessRunner,
    IWorkerProcessRunner workerProcessRunner,
    string workerUser) : IWorkerHealthProbe
{
    public async Task<WorkerHealthResult> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workerUser))
        {
            return new WorkerHealthResult(false, "Codex実行用Unix利用者が設定されていません。");
        }

        var git = await workerProcessRunner.RunAsync(
            "git",
            ["--version"],
            cancellationToken: cancellationToken);
        if (git.ExitCode != 0)
        {
            return Unavailable("Gitを利用できません。", git);
        }

        var github = await workerProcessRunner.RunAsync(
            "gh",
            ["auth", "status"],
            cancellationToken: cancellationToken);
        if (github.ExitCode != 0)
        {
            return Unavailable("GitHub CLIが未認証です。", github);
        }

        var systemd = await systemProcessRunner.RunAsync(
            "systemctl",
            ["--version"],
            cancellationToken: cancellationToken);
        if (systemd.ExitCode != 0)
        {
            return Unavailable("systemdを利用できません。", systemd);
        }

        var codex = await workerProcessRunner.RunAsync(
            "codex",
            ["--version"],
            cancellationToken: cancellationToken);
        if (codex.ExitCode != 0)
        {
            return Unavailable("Codex CLIを実行できません。", codex);
        }

        return new WorkerHealthResult(true, null);
    }

    private static WorkerHealthResult Unavailable(
        string message,
        ProcessResult result)
    {
        var detail = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();

        return new WorkerHealthResult(
            false,
            string.IsNullOrWhiteSpace(detail)
                ? message
                : $"{message} {detail}");
    }
}
