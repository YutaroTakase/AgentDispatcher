using System.Text.Json;
using AgentDispatcher.Domain.Executions;
using AgentDispatcher.Domain.GitHub;
using AgentDispatcher.Domain.Projects;
using AgentDispatcher.Domain.Routing;
using Microsoft.Data.Sqlite;

namespace AgentDispatcher.Infrastructure.Persistence;

public sealed class SqliteExecutionRepository(SqliteDatabase database) : IExecutionRepository
{
    public async Task<ExecutionCreateResult> TryCreateQueuedAsync(
        Project project,
        IssueCandidate issue,
        RoutingDecision routing,
        ExecutionTrigger trigger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentNullException.ThrowIfNull(routing);

        var now = DateTimeOffset.UtcNow;
        var execution = new ExecutionRecord(
            Guid.NewGuid(),
            project.Id,
            issue.Number,
            issue.Title,
            issue.Labels.ToArray(),
            trigger,
            routing.Route.ModelIdentifier,
            routing.Route.ReasoningEffort,
            routing.MatchedRuleId,
            null,
            null,
            ExecutionStatus.Queued,
            null,
            now,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var lockCommand = connection.CreateCommand())
        {
            lockCommand.Transaction = transaction;
            lockCommand.CommandText =
                "UPDATE projects SET updated_at_utc = updated_at_utc WHERE id = $projectId;";
            lockCommand.Parameters.AddWithValue("$projectId", project.Id.ToString());

            if (await lockCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new InvalidOperationException("対象プロジェクトが存在しません。");
            }
        }

        if (await HasActiveIssueAsync(
                connection,
                transaction,
                project.Id,
                issue.Number,
                cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return ExecutionCreateResult.Rejected(
                ExecutionCreateFailure.DuplicateActiveIssue);
        }

        var activeCount = await CountActiveAsync(
            connection,
            transaction,
            project.Id,
            cancellationToken);

        if (activeCount >= project.MaxConcurrentExecutions)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ExecutionCreateResult.Rejected(
                ExecutionCreateFailure.ConcurrencyLimitReached);
        }

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO executions (
                    id,
                    project_id,
                    issue_number,
                    issue_title,
                    issue_labels_json,
                    trigger,
                    model_identifier,
                    reasoning_effort,
                    matched_rule_id,
                    status,
                    created_at_utc)
                VALUES (
                    $id,
                    $projectId,
                    $issueNumber,
                    $issueTitle,
                    $issueLabelsJson,
                    $trigger,
                    $modelIdentifier,
                    $reasoningEffort,
                    $matchedRuleId,
                    $status,
                    $createdAtUtc);
                """;
            insert.Parameters.AddWithValue("$id", execution.Id.ToString());
            insert.Parameters.AddWithValue("$projectId", execution.ProjectId.ToString());
            insert.Parameters.AddWithValue("$issueNumber", execution.IssueNumber);
            insert.Parameters.AddWithValue("$issueTitle", execution.IssueTitle);
            insert.Parameters.AddWithValue(
                "$issueLabelsJson",
                JsonSerializer.Serialize(execution.IssueLabels));
            insert.Parameters.AddWithValue("$trigger", execution.Trigger.ToString());
            insert.Parameters.AddWithValue("$modelIdentifier", execution.ModelIdentifier);
            insert.Parameters.AddWithValue("$reasoningEffort", execution.ReasoningEffort);
            insert.Parameters.AddWithValue(
                "$matchedRuleId",
                execution.MatchedRuleId?.ToString() ?? (object)DBNull.Value);
            insert.Parameters.AddWithValue("$status", execution.Status.ToString());
            insert.Parameters.AddWithValue("$createdAtUtc", execution.CreatedAt.ToString("O"));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertEventAsync(
            connection,
            transaction,
            execution.Id,
            ExecutionStatus.Queued,
            now,
            "実行を作成しました。",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return ExecutionCreateResult.Created(execution);
    }

    public async Task<ExecutionRecord?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateSelectCommand(connection);
        command.CommandText += " WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadExecution(reader)
            : null;
    }

    public async Task<IReadOnlyList<ExecutionRecord>> ListActiveByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var items = new List<ExecutionRecord>();

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateSelectCommand(connection);
        command.CommandText +=
            " WHERE project_id = $projectId AND status IN ('Queued','Preparing','Running') ORDER BY created_at_utc;";
        command.Parameters.AddWithValue("$projectId", projectId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadExecution(reader));
        }

        return items;
    }

    public async Task<IReadOnlyList<ExecutionRecord>> ListActiveAsync(
        CancellationToken cancellationToken = default)
    {
        var items = new List<ExecutionRecord>();

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateSelectCommand(connection);
        command.CommandText +=
            " WHERE status IN ('Queued','Preparing','Running') ORDER BY created_at_utc;";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadExecution(reader));
        }

        return items;
    }

    public async Task<IReadOnlyList<ExecutionRecord>> ListQueuedAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var items = new List<ExecutionRecord>();

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateSelectCommand(connection);
        command.CommandText +=
            " WHERE status = 'Queued' ORDER BY created_at_utc LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadExecution(reader));
        }

        return items;
    }

    public async Task<IReadOnlyList<ExecutionRecord>> QueryAsync(
        ExecutionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Limit is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "取得件数は1件以上500件以下で指定してください。");
        }

        var items = new List<ExecutionRecord>();
        var conditions = new List<string>();

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateSelectCommand(connection);

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
            command.Parameters.AddWithValue("$from", from.ToUniversalTime().ToString("O"));
        }

        if (query.To is { } to)
        {
            conditions.Add("created_at_utc <= $to");
            command.Parameters.AddWithValue("$to", to.ToUniversalTime().ToString("O"));
        }

        if (conditions.Count > 0)
        {
            command.CommandText += " WHERE " + string.Join(" AND ", conditions);
        }

        command.CommandText += " ORDER BY created_at_utc DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", query.Limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadExecution(reader));
        }

        return items;
    }

    public async Task<IReadOnlyList<ExecutionRecord>> ListFinishedBeforeAsync(
        Guid projectId,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default)
    {
        var items = new List<ExecutionRecord>();

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateSelectCommand(connection);
        command.CommandText +=
            """
             WHERE project_id = $projectId
               AND status IN ('Succeeded','Failed','Canceled')
               AND finished_at_utc IS NOT NULL
               AND finished_at_utc <= $cutoff
             ORDER BY finished_at_utc;
            """;
        command.Parameters.AddWithValue("$projectId", projectId.ToString());
        command.Parameters.AddWithValue("$cutoff", cutoff.ToUniversalTime().ToString("O"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadExecution(reader));
        }

        return items;
    }

    public async Task<ExecutionRecord> TransitionAsync(
        Guid id,
        ExecutionStatus targetStatus,
        ExecutionTransitionData? data = null,
        CancellationToken cancellationToken = default)
    {
        data ??= new ExecutionTransitionData();

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var current = await GetWithinTransactionAsync(
            connection,
            transaction,
            id,
            cancellationToken)
            ?? throw new KeyNotFoundException($"実行 {id} が見つかりません。");

        ExecutionStateMachine.EnsureTransition(current.Status, targetStatus);

        if (targetStatus == ExecutionStatus.Running &&
            (string.IsNullOrWhiteSpace(data.BaseRevision) ||
             string.IsNullOrWhiteSpace(data.WorktreePath) ||
             string.IsNullOrWhiteSpace(data.ProcessIdentifier)))
        {
            throw new ArgumentException(
                "実行中へ遷移するには基準リビジョン、作業ツリー、プロセス識別子が必要です。",
                nameof(data));
        }

        var now = DateTimeOffset.UtcNow;
        var startedAt = targetStatus == ExecutionStatus.Running
            ? now.ToString("O")
            : null;
        var finishedAt = ExecutionStateMachine.IsTerminal(targetStatus)
            ? now.ToString("O")
            : null;

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText =
                """
                UPDATE executions
                SET
                    status = $status,
                    base_revision = COALESCE($baseRevision, base_revision),
                    worktree_path = COALESCE($worktreePath, worktree_path),
                    process_identifier = COALESCE($processIdentifier, process_identifier),
                    started_at_utc = COALESCE($startedAtUtc, started_at_utc),
                    finished_at_utc = COALESCE($finishedAtUtc, finished_at_utc),
                    exit_code = COALESCE($exitCode, exit_code),
                    final_result = COALESCE($finalResult, final_result),
                    stdout_log_path = COALESCE($stdoutLogPath, stdout_log_path),
                    stderr_log_path = COALESCE($stderrLogPath, stderr_log_path),
                    failure_summary = COALESCE($failureSummary, failure_summary)
                WHERE id = $id;
                """;
            update.Parameters.AddWithValue("$status", targetStatus.ToString());
            AddNullable(update, "$baseRevision", data.BaseRevision);
            AddNullable(update, "$worktreePath", data.WorktreePath);
            AddNullable(update, "$processIdentifier", data.ProcessIdentifier);
            AddNullable(update, "$startedAtUtc", startedAt);
            AddNullable(update, "$finishedAtUtc", finishedAt);
            AddNullable(update, "$exitCode", data.ExitCode);
            AddNullable(update, "$finalResult", data.FinalResult);
            AddNullable(update, "$stdoutLogPath", data.StandardOutputLogPath);
            AddNullable(update, "$stderrLogPath", data.StandardErrorLogPath);
            AddNullable(update, "$failureSummary", data.FailureSummary);
            update.Parameters.AddWithValue("$id", id.ToString());
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertEventAsync(
            connection,
            transaction,
            id,
            targetStatus,
            now,
            data.Detail,
            cancellationToken);

        var updated = await GetWithinTransactionAsync(
            connection,
            transaction,
            id,
            cancellationToken)
            ?? throw new InvalidOperationException("更新後の実行を取得できませんでした。");

        await transaction.CommitAsync(cancellationToken);
        return updated;
    }

    public async Task ClearWorktreePathAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "UPDATE executions SET worktree_path = NULL WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM executions WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExecutionEvent>> ListEventsAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        var events = new List<ExecutionEvent>();

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, execution_id, status, occurred_at_utc, detail
            FROM execution_events
            WHERE execution_id = $executionId
            ORDER BY id;
            """;
        command.Parameters.AddWithValue("$executionId", executionId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new ExecutionEvent(
                reader.GetInt64(0),
                Guid.Parse(reader.GetString(1)),
                Enum.Parse<ExecutionStatus>(reader.GetString(2)),
                DateTimeOffset.Parse(reader.GetString(3)),
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return events;
    }

    private static SqliteCommand CreateSelectCommand(SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText =
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
        return command;
    }

    private static async Task<ExecutionRecord?> GetWithinTransactionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var command = CreateSelectCommand(connection);
        command.Transaction = transaction;
        command.CommandText += " WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadExecution(reader)
            : null;
    }

    private static ExecutionRecord ReadExecution(SqliteDataReader reader)
    {
        return new ExecutionRecord(
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

    private static async Task<bool> HasActiveIssueAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid projectId,
        int issueNumber,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT 1
            FROM executions
            WHERE project_id = $projectId
              AND issue_number = $issueNumber
              AND status IN ('Queued','Preparing','Running')
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$projectId", projectId.ToString());
        command.Parameters.AddWithValue("$issueNumber", issueNumber);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<int> CountActiveAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM executions
            WHERE project_id = $projectId
              AND status IN ('Queued','Preparing','Running');
            """;
        command.Parameters.AddWithValue("$projectId", projectId.ToString());
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task InsertEventAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid executionId,
        ExecutionStatus status,
        DateTimeOffset occurredAt,
        string? detail,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO execution_events (
                execution_id,
                status,
                occurred_at_utc,
                detail)
            VALUES (
                $executionId,
                $status,
                $occurredAtUtc,
                $detail);
            """;
        command.Parameters.AddWithValue("$executionId", executionId.ToString());
        command.Parameters.AddWithValue("$status", status.ToString());
        command.Parameters.AddWithValue("$occurredAtUtc", occurredAt.ToString("O"));
        AddNullable(command, "$detail", detail);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddNullable(
        SqliteCommand command,
        string name,
        object? value)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }
}
