using AgentDispatcher.Domain.Projects;
using AgentDispatcher.Domain.Workspaces;
using AgentDispatcher.Infrastructure.Processes;

namespace AgentDispatcher.Infrastructure.Workspaces;

public sealed class GitWorktreeManager(
    IProcessRunner processRunner,
    string dataDirectory) : IGitWorkspace
{
    public async Task<WorktreePreparation> PrepareAsync(
        Project project,
        int issueNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (issueNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(issueNumber));
        }

        var repositoryPath = GetRepositoryPath(project.Id);
        var worktreePath = GetWorktreePath(project.Id, issueNumber);
        Directory.CreateDirectory(Path.GetDirectoryName(repositoryPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(worktreePath)!);

        if (!Directory.Exists(Path.Combine(repositoryPath, ".git")))
        {
            var clone = await processRunner.RunAsync(
                "gh",
                [
                    "repo",
                    "clone",
                    project.Repository,
                    repositoryPath,
                    "--",
                    "--filter=blob:none"
                ],
                cancellationToken: cancellationToken);
            EnsureSuccess(clone, "管理用リポジトリを取得できませんでした。");
        }

        var fetch = await processRunner.RunAsync(
            "git",
            ["-C", repositoryPath, "fetch", "--prune", "origin"],
            cancellationToken: cancellationToken);
        EnsureSuccess(fetch, "リポジトリを更新できませんでした。");

        var revision = await processRunner.RunAsync(
            "git",
            ["-C", repositoryPath, "rev-parse", $"origin/{project.DefaultBranch}"],
            cancellationToken: cancellationToken);
        EnsureSuccess(revision, "既定ブランチの最新リビジョンを取得できませんでした.");

        var baseRevision = revision.StandardOutput.Trim();
        if (string.IsNullOrWhiteSpace(baseRevision))
        {
            throw new InvalidOperationException("基準リビジョンが空です。");
        }

        await RemoveExistingWorktreeAsync(
            repositoryPath,
            worktreePath,
            cancellationToken);

        var branchName = $"agent-dispatcher/issue-{issueNumber}";
        var add = await processRunner.RunAsync(
            "git",
            [
                "-C",
                repositoryPath,
                "worktree",
                "add",
                "-B",
                branchName,
                worktreePath,
                baseRevision
            ],
            cancellationToken: cancellationToken);
        EnsureSuccess(add, "Issue専用作業ツリーを作成できませんでした。");

        return new WorktreePreparation(
            repositoryPath,
            worktreePath,
            baseRevision);
    }

    public async Task RemoveAsync(
        Project project,
        int issueNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        var repositoryPath = GetRepositoryPath(project.Id);
        var worktreePath = GetWorktreePath(project.Id, issueNumber);

        if (!Directory.Exists(repositoryPath))
        {
            if (Directory.Exists(worktreePath))
            {
                Directory.Delete(worktreePath, recursive: true);
            }

            return;
        }

        await RemoveExistingWorktreeAsync(
            repositoryPath,
            worktreePath,
            cancellationToken);
    }

    private async Task RemoveExistingWorktreeAsync(
        string repositoryPath,
        string worktreePath,
        CancellationToken cancellationToken)
    {
        if (Directory.Exists(worktreePath))
        {
            var remove = await processRunner.RunAsync(
                "git",
                [
                    "-C",
                    repositoryPath,
                    "worktree",
                    "remove",
                    "--force",
                    worktreePath
                ],
                cancellationToken: cancellationToken);

            if (remove.ExitCode != 0 && Directory.Exists(worktreePath))
            {
                Directory.Delete(worktreePath, recursive: true);
            }
        }

        var prune = await processRunner.RunAsync(
            "git",
            ["-C", repositoryPath, "worktree", "prune"],
            cancellationToken: cancellationToken);
        EnsureSuccess(prune, "作業ツリー情報を整理できませんでした。");
    }

    private string GetRepositoryPath(Guid projectId) =>
        Path.Combine(
            Path.GetFullPath(dataDirectory),
            "repositories",
            projectId.ToString("N"),
            "repository");

    private string GetWorktreePath(Guid projectId, int issueNumber) =>
        Path.Combine(
            Path.GetFullPath(dataDirectory),
            "worktrees",
            projectId.ToString("N"),
            issueNumber.ToString());

    private static void EnsureSuccess(ProcessResult result, string message)
    {
        if (result.ExitCode == 0)
        {
            return;
        }

        var detail = string.IsNullOrWhiteSpace(result.StandardError)
            ? string.Empty
            : $" {result.StandardError.Trim()}";
        throw new InvalidOperationException(message + detail);
    }
}
