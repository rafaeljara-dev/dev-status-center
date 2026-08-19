using DevStatusCenter.Application.Abstractions;

namespace DevStatusCenter.Infrastructure.Persistence;

public sealed class SqliteSettingsStore(SqliteConnectionFactory connectionFactory) : ISettingsStore
{

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM app_settings WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        await connectionFactory.EnterWriteAsync(cancellationToken);
        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO app_settings(key, value, updated_at_ms)
                VALUES ($key, $value, $now)
                ON CONFLICT(key) DO UPDATE SET
                    value = excluded.value,
                    updated_at_ms = excluded.updated_at_ms;
                """;
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", value);
            command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            connectionFactory.ExitWrite();
        }
    }
}

