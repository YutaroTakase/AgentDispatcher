using AgentDispatcher.Domain.Projects;
using Microsoft.Data.Sqlite;

namespace AgentDispatcher.Infrastructure.Persistence;

public sealed class SqliteIssueSelectorRepository(SqliteDatabase database) : IIssueSelectorRepository
{
    public async Task<IssueSelectorSettings?> GetAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                query_fragment,
                sort_field,
                sort_order,
                max_candidates_per_scan
            FROM project_issue_selectors
            WHERE project_id = $projectId;
            """;
        command.Parameters.AddWithValue("$projectId", projectId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return IssueSelectorSettings.Create(
            projectId,
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3));
    }

    public async Task UpsertAsync(
        IssueSelectorSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO project_issue_selectors (
                project_id,
                query_fragment,
                sort_field,
                sort_order,
                max_candidates_per_scan)
            VALUES (
                $projectId,
                $queryFragment,
                $sortField,
                $sortOrder,
                $maxCandidatesPerScan)
            ON CONFLICT(project_id) DO UPDATE SET
                query_fragment = excluded.query_fragment,
                sort_field = excluded.sort_field,
                sort_order = excluded.sort_order,
                max_candidates_per_scan = excluded.max_candidates_per_scan;
            """;
        command.Parameters.AddWithValue("$projectId", settings.ProjectId.ToString());
        command.Parameters.AddWithValue("$queryFragment", settings.QueryFragment);
        command.Parameters.AddWithValue("$sortField", settings.SortField);
        command.Parameters.AddWithValue("$sortOrder", settings.SortOrder);
        command.Parameters.AddWithValue("$maxCandidatesPerScan", settings.MaxCandidatesPerScan);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
