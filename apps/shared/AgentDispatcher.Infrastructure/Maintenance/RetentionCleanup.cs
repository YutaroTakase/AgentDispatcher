using AgentDispatcher.Domain.Maintenance;
using AgentDispatcher.Domain.Projects;
using AgentDispatcher.Domain.Workspaces;

namespace AgentDispatcher.Infrastructure.Maintenance;

public sealed class RetentionCleanup(
    IRetentionRepository retention,
    IProjectRepository projects,
    IGitWorkspace workspaces,
    string dataDirectory) : IRetentionCleanup
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);

    public async Task<CleanupRunRecord?> RunIfDueAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var lastRun = await retention.GetLastCleanupAtAsync(cancellationToken);
        if (lastRun is { } previous && now - previous < CleanupInterval)
        {
            return null;
        }

        return await RunAsync(now, cancellationToken);
    }

    public async Task<CleanupRunRecord> RunAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var startedAt = now;
        var worktreesRemoved = 0;
        var executionsDeleted = 0;
        var errors = new List<string>();
        var candidates = await retention.ListCandidatesAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            var project = await projects.GetAsync(candidate.ProjectId, cancellationToken);
            if (project is null)
            {
                errors.Add($"実行 {candidate.ExecutionId}: 対象プロジェクトが見つかりません。");
                continue;
            }

            var worktreeExpired = candidate.Status == Domain.Executions.ExecutionStatus.Succeeded ||
                candidate.FinishedAt <= now.AddDays(-candidate.FailureWorktreeRetentionDays);

            if (worktreeExpired)
            {
                try
                {
                    await workspaces.RemoveAsync(
                        project,
                        candidate.IssueNumber,
                        cancellationToken);
                    worktreesRemoved++;
                }
                catch (Exception exception)
                {
                    errors.Add($"実行 {candidate.ExecutionId}: 作業ツリー整理失敗: {exception.Message}");
                    continue;
                }
            }

            var historyExpired =
                candidate.FinishedAt <= now.AddDays(-candidate.ExecutionRetentionDays);

            if (!historyExpired || !worktreeExpired)
            {
                continue;
            }

            try
            {
                DeleteExecutionLogDirectory(candidate.ExecutionId);
                if (await retention.DeleteExecutionAsync(
                        candidate.ExecutionId,
                        cancellationToken))
                {
                    executionsDeleted++;
                }
            }
            catch (Exception exception)
            {
                errors.Add($"実行 {candidate.ExecutionId}: 履歴・ログ整理失敗: {exception.Message}");
            }
        }

        return await retention.RecordCleanupAsync(
            startedAt,
            DateTimeOffset.UtcNow,
            worktreesRemoved,
            executionsDeleted,
            errors,
            cancellationToken);
    }

    private void DeleteExecutionLogDirectory(Guid executionId)
    {
        var root = Path.GetFullPath(dataDirectory);
        var executionDirectory = Path.GetFullPath(Path.Combine(
            root,
            "executions",
            executionId.ToString("N")));

        var expectedPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!executionDirectory.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("ログ保存先がデータ領域外です。");
        }

        if (Directory.Exists(executionDirectory))
        {
            Directory.Delete(executionDirectory, recursive: true);
        }
    }
}
