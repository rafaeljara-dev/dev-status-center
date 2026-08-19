using Microsoft.Data.Sqlite;

namespace DevStatusCenter.Infrastructure.Persistence;

public sealed class SqliteMigrationRunner(SqliteConnectionFactory connectionFactory)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await ConfigureDatabaseAsync(connection, cancellationToken);
        await EnsureMigrationTableAsync(connection, cancellationToken);

        var assembly = typeof(SqliteMigrationRunner).Assembly;
        var migrations = assembly.GetManifestResourceNames()
            .Where(x => x.Contains(".Persistence.Migrations.", StringComparison.Ordinal) &&
                        x.EndsWith(".sql", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        foreach (var resourceName in migrations)
        {
            if (await IsAppliedAsync(connection, resourceName, cancellationToken))
            {
                continue;
            }

            await using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded migration not found: {resourceName}");
            using var reader = new StreamReader(stream);
            var sql = await reader.ReadToEndAsync(cancellationToken);

            await using var transaction = connection.BeginTransaction();
            await using (var migration = connection.CreateCommand())
            {
                migration.Transaction = transaction;
                migration.CommandText = sql;
                await migration.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var markApplied = connection.CreateCommand())
            {
                markApplied.Transaction = transaction;
                markApplied.CommandText = "INSERT INTO schema_migrations(name, applied_at_ms) VALUES ($name, $now);";
                markApplied.Parameters.AddWithValue("$name", resourceName);
                markApplied.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                await markApplied.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static async Task ConfigureDatabaseAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        // journal_mode is the only pragma that persists in the database file; the per-connection
        // ones live in SqliteConnectionFactory so every connection gets them, not just this one.
        command.CommandText = "PRAGMA journal_mode = WAL;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureMigrationTableAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                name TEXT PRIMARY KEY,
                applied_at_ms INTEGER NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> IsAppliedAsync(
        SqliteConnection connection,
        string name,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM schema_migrations WHERE name = $name);";
        command.Parameters.AddWithValue("$name", name);
        return await command.ExecuteScalarAsync(cancellationToken) is 1L;
    }
}
