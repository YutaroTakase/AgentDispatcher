using System.Text.Json;
using AgentDispatcher.Domain.Executions;
using Microsoft.Data.Sqlite;

namespace AgentDispatcher.Infrastructure.Persistence;

public sealed class SqliteExecutionQueryRepository(SqliteDatabase database)
    : IExecutionQueryRepository
{
    public async Task<IReadOnlyList<ExecutionRecord>> ListAsync(
        ExecutionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Limit is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "取得件数は1件以上1000件以下で指定してください。");
        }

        var items = new List<ExecutionRecord>();

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var conditions = new List<string>();
        if (query.ProjectId is { } projectId)
        {
            conditions.Add("project_id = $projectId");
            command.Parameters.AddWithValue("$projectId", projectId.ToString());
        }

        if (query.Status is { } status)
        {
            conditions.Add("status = $status");
            command.Parameters.AddWithValue("$status", status.ToString());
        }

        if (!string.IsNullOrWhiteSpace(query.ModelIdentifier))
        {
            conditions.Add("model_identifier = $modelIdentifier");
            command.Parameters.AddWithValue("$modelIdentifier", query.ModelIdentifier.Trim());
        }

        if (query.Trigger is { } trigger)
        {
            conditions.Add("trigger = $trigger");
            command.Parameters.AddWithValue("$trigger", trigger.ToString());
        }

        if (query.From is { } from)
        {
            conditions.Add("created_at_utc >= $from");
            command.Parameters.AddWithValue("$from", from.ToString("O"));
        }

        if (query.To is { } to)
        {
            conditions.Add("created_at_utc <= $to");
            command.Parameters.AddWithValue("$to", to.ToString("O"));
        }

        command.CommandText = SelectColumns +
            (conditions.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", conditions)) +
            " ORDER BY created_at_utc DESC, id DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", query.Limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadExecution(reader));
        }

        return items;
    }

    public async Task<IReadOnlyList<ExecutionRecord>> ListQueuedAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var items = new List<ExecutionRecord>();

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectColumns +
            " WHERE status = 'Queued' ORDER BY created_at_utc, id LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadExecution(reader));
        }

        return items;
    }

    private const string SelectColumns =
        """
        SELECT
            id,
            project_id,
            issue_number,
            issue_title,
            issue_labels_json,
            trigger,
            model_identifier,
            reasoning_effort,
            matched_rule_id,
            base_revision,
            worktree_path,
            status,
            process_identifier,
            created_at_utc,
            started_at_utc,
            finished_at_utc,
            exit_code,
            final_result,
            stdout_log_path,
            stderr_log_path,
            failure_summary
        FROM executions
        """;

    private static ExecutionRecord ReadExecution(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetInt32(2),
            reader.GetString(3),
            JsonSerializer.Deserialize<string[]>(reader.GetString(4)) ?? [],
            Enum.Parse<ExecutionTrigger>(reader.GetString(5)),
            reader.GetString(6),
            reader.GetString(7),
            reader.IsDBNull(8) ? null : Guid.Parse(reader.GetString(8)),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            Enum.Parse<ExecutionStatus>(reader.GetString(11)),
            reader.IsDBNull(12) ? null : reader.GetString(12),
            DateTimeOffset.Parse(reader.GetString(13)),
            reader.IsDBNull(14) ? null : DateTimeOffset.Parse(reader.GetString(14)),
            reader.IsDBNull(15) ? null : DateTimeOffset.Parse(reader.GetString(15)),
            reader.IsDBNull(16) ? null : reader.GetInt32(16),
            reader.IsDBNull(17) ? null : reader.GetString(17),
            reader.IsDBNull(18) ? null : reader.GetString(18),
            reader.IsDBNull(19) ? null : reader.GetString(19),
            reader.IsDBNull(20) ? null : reader.GetString(20));
}
