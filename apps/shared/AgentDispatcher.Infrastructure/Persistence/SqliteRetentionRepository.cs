using System.Text.Json;
using AgentDispatcher.Domain.Executions;
using AgentDispatcher.Domain.Maintenance;
using Microsoft.Data.Sqlite;

namespace AgentDispatcher.Infrastructure.Persistence;

public sealed class SqliteRetentionRepository(SqliteDatabase database)
    : IRetentionRepository
{
    public async Task<IReadOnlyList<RetentionCandidate>> ListCandidatesAsync(
        CancellationToken cancellationToken = default)
    {
        var items = new List<RetentionCandidate>();

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                e.id,
                e.project_id,
                e.issue_number,
                e.status,
                e.finished_at_utc,
                p.execution_retention_days,
                p.failure_worktree_retention_days
            FROM executions e
            INNER JOIN projects p ON p.id = e.project_id
            WHERE e.status IN ('Succeeded', 'Failed', 'Canceled')
              AND e.finished_at_utc IS NOT NULL
            ORDER BY e.finished_at_utc;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new RetentionCandidate(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                reader.GetInt32(2),
                Enum.Parse<ExecutionStatus>(reader.GetString(3)),
                DateTimeOffset.Parse(reader.GetString(4)),
                reader.GetInt32(5),
                reader.GetInt32(6)));
        }

        return items;
    }

    public async Task<bool> DeleteExecutionAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            DELETE FROM executions
            WHERE id = $id
              AND status IN ('Succeeded', 'Failed', 'Canceled');
            """;
        command.Parameters.AddWithValue("$id", executionId.ToString());
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<DateTimeOffset?> GetLastCleanupAtAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT finished_at_utc FROM cleanup_runs ORDER BY id DESC LIMIT 1;";

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string text ? DateTimeOffset.Parse(text) : null;
    }

    public async Task<CleanupRunRecord> RecordCleanupAsync(
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        int worktreesRemoved,
        int executionsDeleted,
        IReadOnlyList<string> errors,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO cleanup_runs(
                started_at_utc,
                finished_at_utc,
                worktrees_removed,
                executions_deleted,
                error_count,
                errors_json)
            VALUES(
                $startedAt,
                $finishedAt,
                $worktreesRemoved,
                $executionsDeleted,
                $errorCount,
                $errorsJson);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$startedAt", startedAt.ToString("O"));
        command.Parameters.AddWithValue("$finishedAt", finishedAt.ToString("O"));
        command.Parameters.AddWithValue("$worktreesRemoved", worktreesRemoved);
        command.Parameters.AddWithValue("$executionsDeleted", executionsDeleted);
        command.Parameters.AddWithValue("$errorCount", errors.Count);
        command.Parameters.AddWithValue("$errorsJson", JsonSerializer.Serialize(errors));

        var id = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        return new CleanupRunRecord(
            id,
            startedAt,
            finishedAt,
            worktreesRemoved,
            executionsDeleted,
            errors.Count,
            errors.ToArray());
    }

    public async Task<IReadOnlyList<CleanupRunRecord>> ListRecentCleanupRunsAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var items = new List<CleanupRunRecord>();
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id,
                started_at_utc,
                finished_at_utc,
                worktrees_removed,
                executions_deleted,
                error_count,
                errors_json
            FROM cleanup_runs
            ORDER BY id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new CleanupRunRecord(
                reader.GetInt64(0),
                DateTimeOffset.Parse(reader.GetString(1)),
                DateTimeOffset.Parse(reader.GetString(2)),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                JsonSerializer.Deserialize<string[]>(reader.GetString(6)) ?? []));
        }

        return items;
    }
}
