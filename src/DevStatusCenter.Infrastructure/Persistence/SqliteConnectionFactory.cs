using Microsoft.Data.Sqlite;

namespace DevStatusCenter.Infrastructure.Persistence;

public sealed class SqliteConnectionFactory : IDisposable
{
    /// <summary>
    /// Applied on every connection, not only on the one that runs migrations.
    /// <list type="bullet">
    /// <item><c>journal_mode</c> is persisted in the file, so it is set once by the migration runner.</item>
    /// <item><c>synchronous</c>, <c>temp_store</c>, <c>foreign_keys</c> and <c>busy_timeout</c> are
    /// per-connection and reset to their defaults on every open. Leaving <c>synchronous</c> at its
    /// FULL default would force an fsync on every commit, which contradicts the idle-disk budget.</item>
    /// </list>
    /// </summary>
    private const string SessionPragmas =
        "PRAGMA busy_timeout = 5000;" +
        "PRAGMA foreign_keys = ON;" +
        "PRAGMA synchronous = NORMAL;" +
        "PRAGMA temp_store = MEMORY;";

    /// <summary>
    /// Un único escritor por base de datos. Antes cada store tenía su propio semáforo, así
    /// que una escritura de settings podía competir con un refresh de providers sobre el
    /// mismo archivo y degenerar en SQLITE_BUSY. Serializar aquí lo elimina de raíz y deja
    /// las lecturas totalmente libres (WAL permite lectores concurrentes con el escritor).
    /// </summary>
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    private readonly string _connectionString;

    public SqliteConnectionFactory(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(databasePath));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,

            // Private cache on purpose. Shared cache degrades WAL concurrency to table-level
            // SQLITE_LOCKED between connections of the same process, which would make dashboard
            // reads collide with a provider refresh instead of running side by side.
            Cache = SqliteCacheMode.Private,
            Pooling = true,
            DefaultTimeout = 5
        }.ToString();
    }

    /// <summary>Toma el turno de escritura. No asigna cuando no hay contención.</summary>
    public Task EnterWriteAsync(CancellationToken cancellationToken) =>
        _writeGate.WaitAsync(cancellationToken);

    public void ExitWrite() => _writeGate.Release();

    public void Dispose() => _writeGate.Dispose();

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = SessionPragmas;
        await command.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }
}
