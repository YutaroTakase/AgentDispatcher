using AgentDispatcher.Domain.Projects;
using Microsoft.Data.Sqlite;

namespace AgentDispatcher.Infrastructure.Persistence;

public sealed class SqliteProjectRepository(SqliteDatabase database) : IProjectRepository
{
    public async Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default)
    {
        var projects = new List<Project>();

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id,
                display_name,
                repository,
                enabled,
                default_branch,
                issue_scan_interval_minutes,
                max_concurrent_executions,
                execution_retention_days,
                failure_worktree_retention_days
            FROM projects
            ORDER BY display_name COLLATE NOCASE, id;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            projects.Add(ReadProject(reader));
        }

        return projects;
    }

    public async Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id,
                display_name,
                repository,
                enabled,
                default_branch,
                issue_scan_interval_minutes,
                max_concurrent_executions,
                execution_retention_days,
                failure_worktree_retention_days
            FROM projects
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadProject(reader) : null;
    }

    public async Task AddAsync(Project project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        var now = DateTimeOffset.UtcNow.ToString("O");

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO projects (
                id,
                display_name,
                repository,
                enabled,
                default_branch,
                issue_scan_interval_minutes,
                max_concurrent_executions,
                execution_retention_days,
                failure_worktree_retention_days,
                created_at_utc,
                updated_at_utc)
            VALUES (
                $id,
                $displayName,
                $repository,
                $enabled,
                $defaultBranch,
                $issueScanIntervalMinutes,
                $maxConcurrentExecutions,
                $executionRetentionDays,
                $failureWorktreeRetentionDays,
                $createdAtUtc,
                $updatedAtUtc);
            """;

        AddParameters(command, project);
        command.Parameters.AddWithValue("$createdAtUtc", now);
        command.Parameters.AddWithValue("$updatedAtUtc", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(Project project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE projects
            SET
                display_name = $displayName,
                repository = $repository,
                enabled = $enabled,
                default_branch = $defaultBranch,
                issue_scan_interval_minutes = $issueScanIntervalMinutes,
                max_concurrent_executions = $maxConcurrentExecutions,
                execution_retention_days = $executionRetentionDays,
                failure_worktree_retention_days = $failureWorktreeRetentionDays,
                updated_at_utc = $updatedAtUtc
            WHERE id = $id;
            """;

        AddParameters(command, project);
        command.Parameters.AddWithValue("$updatedAtUtc", DateTimeOffset.UtcNow.ToString("O"));

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM projects WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString());
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static void AddParameters(SqliteCommand command, Project project)
    {
        command.Parameters.AddWithValue("$id", project.Id.ToString());
        command.Parameters.AddWithValue("$displayName", project.DisplayName);
        command.Parameters.AddWithValue("$repository", project.Repository);
        command.Parameters.AddWithValue("$enabled", project.Enabled ? 1 : 0);
        command.Parameters.AddWithValue("$defaultBranch", project.DefaultBranch);
        command.Parameters.AddWithValue("$issueScanIntervalMinutes", project.IssueScanIntervalMinutes);
        command.Parameters.AddWithValue("$maxConcurrentExecutions", project.MaxConcurrentExecutions);
        command.Parameters.AddWithValue("$executionRetentionDays", project.ExecutionRetentionDays);
        command.Parameters.AddWithValue("$failureWorktreeRetentionDays", project.FailureWorktreeRetentionDays);
    }

    private static Project ReadProject(SqliteDataReader reader)
    {
        return Project.Restore(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt64(3) != 0,
            reader.GetString(4),
            reader.GetInt32(5),
            reader.GetInt32(6),
            reader.GetInt32(7),
            reader.GetInt32(8));
    }
}
