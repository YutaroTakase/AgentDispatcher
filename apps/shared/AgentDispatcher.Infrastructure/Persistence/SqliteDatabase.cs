using Microsoft.Data.Sqlite;

namespace AgentDispatcher.Infrastructure.Persistence;

public sealed class SqliteDatabase
{
    private static readonly Migration[] Migrations =
    [
        new(
            1,
            """
            CREATE TABLE projects (
                id TEXT NOT NULL PRIMARY KEY,
                display_name TEXT NOT NULL,
                repository TEXT NOT NULL,
                enabled INTEGER NOT NULL,
                default_branch TEXT NOT NULL,
                issue_scan_interval_minutes INTEGER NOT NULL,
                max_concurrent_executions INTEGER NOT NULL,
                execution_retention_days INTEGER NOT NULL,
                failure_worktree_retention_days INTEGER NOT NULL,
                created_at_utc TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
            );

            CREATE INDEX ix_projects_enabled ON projects(enabled);
            """),
        new(
            2,
            """
            CREATE TABLE project_issue_selectors (
                project_id TEXT NOT NULL PRIMARY KEY,
                query_fragment TEXT NOT NULL,
                sort_field TEXT NOT NULL,
                sort_order TEXT NOT NULL,
                max_candidates_per_scan INTEGER NOT NULL,
                FOREIGN KEY(project_id) REFERENCES projects(id) ON DELETE CASCADE
            );

            CREATE TABLE project_default_routes (
                project_id TEXT NOT NULL PRIMARY KEY,
                model_identifier TEXT NOT NULL,
                reasoning_effort TEXT NOT NULL,
                FOREIGN KEY(project_id) REFERENCES projects(id) ON DELETE CASCADE
            );

            CREATE TABLE routing_rules (
                id TEXT NOT NULL PRIMARY KEY,
                project_id TEXT NOT NULL,
                rule_order INTEGER NOT NULL,
                enabled INTEGER NOT NULL,
                required_labels_json TEXT NOT NULL,
                excluded_labels_json TEXT NOT NULL,
                model_identifier TEXT NOT NULL,
                reasoning_effort TEXT NOT NULL,
                FOREIGN KEY(project_id) REFERENCES projects(id) ON DELETE CASCADE
            );

            CREATE INDEX ix_routing_rules_project_order
                ON routing_rules(project_id, rule_order, id);
            """)
    ];

    public SqliteDatabase(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public static SqliteDatabase FromFile(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        }.ToString();

        return new SqliteDatabase(connectionString);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version INTEGER NOT NULL PRIMARY KEY,
                applied_at_utc TEXT NOT NULL
            );
            """,
            cancellationToken);

        foreach (var migration in Migrations)
        {
            if (await IsAppliedAsync(connection, migration.Version, cancellationToken))
            {
                continue;
            }

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = migration.Sql;
            await command.ExecuteNonQueryAsync(cancellationToken);

            await using var recordCommand = connection.CreateCommand();
            recordCommand.Transaction = (SqliteTransaction)transaction;
            recordCommand.CommandText =
                "INSERT INTO schema_migrations(version, applied_at_utc) VALUES ($version, $appliedAtUtc);";
            recordCommand.Parameters.AddWithValue("$version", migration.Version);
            recordCommand.Parameters.AddWithValue("$appliedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
            await recordCommand.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> IsAppliedAsync(
        SqliteConnection connection,
        int version,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM schema_migrations WHERE version = $version LIMIT 1;";
        command.Parameters.AddWithValue("$version", version);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null;
    }

    private sealed record Migration(int Version, string Sql);
}
