using AgentDispatcher.Domain.GitHub;
using AgentDispatcher.Domain.Health;
using AgentDispatcher.Domain.Projects;
using AgentDispatcher.Infrastructure.Processes;

namespace AgentDispatcher.Infrastructure.Health;

public sealed class HealthService(
    IProjectRepository projects,
    IGitHubIssueSource gitHub,
    IProcessRunner systemProcessRunner,
    IWorkerProcessRunner workerProcessRunner,
    string dataDirectory) : IHealthService
{
    public async Task<HostHealthReport> CheckHostAsync(
        CancellationToken cancellationToken = default)
    {
        var checks = new List<HealthCheck>();

        checks.Add(await CommandCheckAsync(
            "git",
            "Git",
            workerProcessRunner,
            "git",
            ["--version"],
            "Gitを利用できます。",
            "Gitを利用できません。",
            cancellationToken));

        checks.Add(await CommandCheckAsync(
            "github-cli",
            "GitHub CLI",
            workerProcessRunner,
            "gh",
            ["--version"],
            "GitHub CLIを利用できます。",
            "GitHub CLIを利用できません。",
            cancellationToken));

        checks.Add(await CommandCheckAsync(
            "github-auth",
            "GitHub認証",
            workerProcessRunner,
            "gh",
            ["auth", "status"],
            "GitHubへ認証済みです。",
            "GitHubへ認証されていません。",
            cancellationToken));

        checks.Add(await CommandCheckAsync(
            "codex-cli",
            "Codex CLI",
            workerProcessRunner,
            "codex",
            ["--version"],
            "Codex CLIを利用できます。",
            "Codex CLIを利用できません。",
            cancellationToken));

        checks.Add(await CommandCheckAsync(
            "codex-auth",
            "Codex認証",
            workerProcessRunner,
            "codex",
            ["login", "status"],
            "Codexへ認証済みです。",
            "Codexへ認証されていません。",
            cancellationToken));

        checks.Add(await CommandCheckAsync(
            "systemd",
            "systemd",
            systemProcessRunner,
            "systemctl",
            ["--version"],
            "systemdを利用できます。",
            "systemdを利用できません。",
            cancellationToken));

        checks.Add(CheckDataDirectory());

        long? freeDiskBytes = null;
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(dataDirectory));
            if (!string.IsNullOrWhiteSpace(root))
            {
                freeDiskBytes = new DriveInfo(root).AvailableFreeSpace;
                checks.Add(new HealthCheck(
                    "disk",
                    "空きディスク容量",
                    freeDiskBytes < 1L * 1024 * 1024 * 1024
                        ? HealthState.Warning
                        : HealthState.Healthy,
                    $"{freeDiskBytes / 1024 / 1024:N0} MiB 利用可能です。"));
            }
        }
        catch (Exception exception)
        {
            checks.Add(new HealthCheck(
                "disk",
                "空きディスク容量",
                HealthState.Unhealthy,
                $"空き容量を確認できません: {exception.Message}"));
        }

        return new HostHealthReport(
            DateTimeOffset.UtcNow,
            checks,
            freeDiskBytes);
    }

    public async Task<ProjectHealthReport?> CheckProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await projects.GetAsync(projectId, cancellationToken);
        if (project is null)
        {
            return null;
        }

        return await CheckProjectInternalAsync(project, cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectHealthReport>> CheckProjectsAsync(
        CancellationToken cancellationToken = default)
    {
        var items = await projects.ListAsync(cancellationToken);
        var reports = new List<ProjectHealthReport>(items.Count);

        foreach (var project in items)
        {
            reports.Add(await CheckProjectInternalAsync(project, cancellationToken));
        }

        return reports;
    }

    private async Task<ProjectHealthReport> CheckProjectInternalAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        var checks = new List<HealthCheck>();
        var repositoryCheck = await gitHub.CheckRepositoryAsync(
            project,
            cancellationToken);

        checks.Add(new HealthCheck(
            "repository",
            "GitHubリポジトリ",
            repositoryCheck.Success ? HealthState.Healthy : HealthState.Unhealthy,
            repositoryCheck.Success
                ? $"{project.Repository} / {project.DefaultBranch} へ到達できます。"
                : repositoryCheck.Error ?? "GitHubリポジトリへ到達できません。"));

        var repositoryPath = Path.Combine(
            Path.GetFullPath(dataDirectory),
            "repositories",
            project.Id.ToString("N"),
            "repository");

        if (!Directory.Exists(Path.Combine(repositoryPath, ".git")))
        {
            checks.Add(new HealthCheck(
                "managed-repository",
                "管理用リポジトリ",
                HealthState.Warning,
                "まだ作成されていません。最初の実行時に作成します。"));
        }
        else
        {
            checks.Add(await CheckRepositorySyncAsync(
                project,
                repositoryPath,
                cancellationToken));
        }

        var worktreeRoot = Path.Combine(
            Path.GetFullPath(dataDirectory),
            "worktrees",
            project.Id.ToString("N"));

        var worktreeState = CheckWritableDirectory(
            "worktree-root",
            "作業ツリー保存先",
            worktreeRoot);
        checks.Add(worktreeState);

        var worktreeCount = Directory.Exists(worktreeRoot)
            ? Directory.EnumerateDirectories(worktreeRoot).Count()
            : 0;

        checks.Add(new HealthCheck(
            "worktrees",
            "残存作業ツリー",
            HealthState.Healthy,
            $"{worktreeCount}件の作業ツリーがあります。"));

        return new ProjectHealthReport(
            project.Id,
            project.DisplayName,
            project.Repository,
            DateTimeOffset.UtcNow,
            checks,
            worktreeCount);
    }

    private async Task<HealthCheck> CheckRepositorySyncAsync(
        Project project,
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var local = await workerProcessRunner.RunAsync(
            "git",
            ["-C", repositoryPath, "rev-parse", $"origin/{project.DefaultBranch}"],
            cancellationToken: cancellationToken);

        if (local.ExitCode != 0)
        {
            return new HealthCheck(
                "managed-repository",
                "管理用リポジトリ",
                HealthState.Unhealthy,
                Detail("ローカルの既定ブランチ参照を確認できません。", local));
        }

        var remote = await workerProcessRunner.RunAsync(
            "gh",
            [
                "api",
                $"repos/{project.Repository}/commits/{Uri.EscapeDataString(project.DefaultBranch)}",
                "--jq",
                ".sha"
            ],
            cancellationToken: cancellationToken);

        if (remote.ExitCode != 0)
        {
            return new HealthCheck(
                "managed-repository",
                "管理用リポジトリ",
                HealthState.Unhealthy,
                Detail("GitHub上の最新リビジョンを確認できません。", remote));
        }

        var localRevision = local.StandardOutput.Trim();
        var remoteRevision = remote.StandardOutput.Trim();
        var synced = string.Equals(
            localRevision,
            remoteRevision,
            StringComparison.OrdinalIgnoreCase);

        return new HealthCheck(
            "managed-repository",
            "管理用リポジトリ",
            synced ? HealthState.Healthy : HealthState.Warning,
            synced
                ? $"GitHubと同期しています ({ShortRevision(localRevision)})。"
                : $"未同期です。ローカル {ShortRevision(localRevision)} / GitHub {ShortRevision(remoteRevision)}。次回実行時に更新します。");
    }

    private HealthCheck CheckDataDirectory() =>
        CheckWritableDirectory(
            "data-directory",
            "データ保存先",
            Path.GetFullPath(dataDirectory));

    private static HealthCheck CheckWritableDirectory(
        string key,
        string name,
        string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var testFile = Path.Combine(
                directory,
                $".agent-dispatcher-write-test-{Guid.NewGuid():N}");
            File.WriteAllText(testFile, "ok");
            File.Delete(testFile);

            return new HealthCheck(
                key,
                name,
                HealthState.Healthy,
                $"{directory} へ書き込みできます。");
        }
        catch (Exception exception)
        {
            return new HealthCheck(
                key,
                name,
                HealthState.Unhealthy,
                $"{directory} へ書き込みできません: {exception.Message}");
        }
    }

    private static async Task<HealthCheck> CommandCheckAsync(
        string key,
        string name,
        IProcessRunner runner,
        string fileName,
        IReadOnlyList<string> arguments,
        string successMessage,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await runner.RunAsync(
                fileName,
                arguments,
                cancellationToken: cancellationToken);

            return new HealthCheck(
                key,
                name,
                result.ExitCode == 0 ? HealthState.Healthy : HealthState.Unhealthy,
                result.ExitCode == 0
                    ? successMessage
                    : Detail(failureMessage, result));
        }
        catch (Exception exception)
        {
            return new HealthCheck(
                key,
                name,
                HealthState.Unhealthy,
                $"{failureMessage} {exception.Message}");
        }
    }

    private static string Detail(
        string message,
        ProcessResult result)
    {
        var detail = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();

        return string.IsNullOrWhiteSpace(detail)
            ? message
            : $"{message} {detail}";
    }

    private static string ShortRevision(string revision) =>
        string.IsNullOrWhiteSpace(revision)
            ? "(不明)"
            : revision[..Math.Min(8, revision.Length)];
}
