using System.Text.Json;
using AgentDispatcher.Domain.Routing;
using Microsoft.Data.Sqlite;

namespace AgentDispatcher.Infrastructure.Persistence;

public sealed class SqliteRoutingConfigurationRepository(SqliteDatabase database)
    : IRoutingConfigurationRepository
{
    public async Task<ExecutionRoute?> GetDefaultRouteAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT model_identifier, reasoning_effort
            FROM project_default_routes
            WHERE project_id = $projectId;
            """;
        command.Parameters.AddWithValue("$projectId", projectId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ExecutionRoute.Create(reader.GetString(0), reader.GetString(1))
            : null;
    }

    public async Task UpsertDefaultRouteAsync(
        Guid projectId,
        ExecutionRoute route,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(route);

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO project_default_routes (
                project_id,
                model_identifier,
                reasoning_effort)
            VALUES (
                $projectId,
                $modelIdentifier,
                $reasoningEffort)
            ON CONFLICT(project_id) DO UPDATE SET
                model_identifier = excluded.model_identifier,
                reasoning_effort = excluded.reasoning_effort;
            """;
        command.Parameters.AddWithValue("$projectId", projectId.ToString());
        command.Parameters.AddWithValue("$modelIdentifier", route.ModelIdentifier);
        command.Parameters.AddWithValue("$reasoningEffort", route.ReasoningEffort);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RoutingRule>> ListRulesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var rules = new List<RoutingRule>();

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id,
                rule_order,
                enabled,
                required_labels_json,
                excluded_labels_json,
                model_identifier,
                reasoning_effort
            FROM routing_rules
            WHERE project_id = $projectId
            ORDER BY rule_order, id;
            """;
        command.Parameters.AddWithValue("$projectId", projectId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rules.Add(RoutingRule.Restore(
                Guid.Parse(reader.GetString(0)),
                projectId,
                reader.GetInt32(1),
                reader.GetInt64(2) != 0,
                DeserializeLabels(reader.GetString(3)),
                DeserializeLabels(reader.GetString(4)),
                ExecutionRoute.Create(reader.GetString(5), reader.GetString(6))));
        }

        return rules;
    }

    public async Task ReplaceRulesAsync(
        Guid projectId,
        IReadOnlyList<RoutingRule> rules,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rules);

        if (rules.Any(rule => rule.ProjectId != projectId))
        {
            throw new ArgumentException("異なるプロジェクトの規則を保存できません。", nameof(rules));
        }

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = (SqliteTransaction)transaction;
            deleteCommand.CommandText = "DELETE FROM routing_rules WHERE project_id = $projectId;";
            deleteCommand.Parameters.AddWithValue("$projectId", projectId.ToString());
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var rule in rules)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText =
                """
                INSERT INTO routing_rules (
                    id,
                    project_id,
                    rule_order,
                    enabled,
                    required_labels_json,
                    excluded_labels_json,
                    model_identifier,
                    reasoning_effort)
                VALUES (
                    $id,
                    $projectId,
                    $order,
                    $enabled,
                    $requiredLabels,
                    $excludedLabels,
                    $modelIdentifier,
                    $reasoningEffort);
                """;
            command.Parameters.AddWithValue("$id", rule.Id.ToString());
            command.Parameters.AddWithValue("$projectId", projectId.ToString());
            command.Parameters.AddWithValue("$order", rule.Order);
            command.Parameters.AddWithValue("$enabled", rule.Enabled ? 1 : 0);
            command.Parameters.AddWithValue("$requiredLabels", JsonSerializer.Serialize(rule.RequiredLabels));
            command.Parameters.AddWithValue("$excludedLabels", JsonSerializer.Serialize(rule.ExcludedLabels));
            command.Parameters.AddWithValue("$modelIdentifier", rule.Route.ModelIdentifier);
            command.Parameters.AddWithValue("$reasoningEffort", rule.Route.ReasoningEffort);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static string[] DeserializeLabels(string json)
    {
        return JsonSerializer.Deserialize<string[]>(json) ?? [];
    }
}
