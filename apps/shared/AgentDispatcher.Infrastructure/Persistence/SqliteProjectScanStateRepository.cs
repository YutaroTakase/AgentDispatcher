using AgentDispatcher.Domain.Dispatching;
using Microsoft.Data.Sqlite;

namespace AgentDispatcher.Infrastructure.Persistence;

public sealed class SqliteProjectScanStateRepository(SqliteDatabase database)
    : IProjectScanStateRepository
{
    public async Task<DateTimeOffset?> GetLastScanAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT last_scan_at_utc FROM project_scan_states WHERE project_id = $projectId;";
        command.Parameters.AddWithValue("$projectId", projectId.ToString());

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string text ? DateTimeOffset.Parse(text) : null;
    }

    public async Task SetLastScanAsync(
        Guid projectId,
        DateTimeOffset scannedAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO project_scan_states(project_id, last_scan_at_utc)
            VALUES ($projectId, $lastScanAtUtc)
            ON CONFLICT(project_id) DO UPDATE SET
                last_scan_at_utc = excluded.last_scan_at_utc;
            """;
        command.Parameters.AddWithValue("$projectId", projectId.ToString());
        command.Parameters.AddWithValue("$lastScanAtUtc", scannedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
