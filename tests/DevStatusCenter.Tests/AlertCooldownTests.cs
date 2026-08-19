using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Domain.Models;
using DevStatusCenter.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace DevStatusCenter.Tests;

/// <summary>
/// El enfriamiento es lo que separa "te aviso" de "te acoso". Sin él, una alerta de presupuesto
/// se repetiría en cada ciclo de refresh y el usuario apagaría las notificaciones.
/// </summary>
public sealed class AlertCooldownTests : IDisposable
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromHours(12);

    private static readonly DateTimeOffset Now = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "dev-status-center-alerts",
        Guid.NewGuid().ToString("N"));

    private readonly List<SqliteConnectionFactory> _factories = [];

    public AlertCooldownTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        foreach (var factory in _factories)
        {
            factory.Dispose();
        }

        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task ANewAlertIsAlwaysDelivered()
    {
        var store = await CreateStoreAsync();

        var due = await store.RecordAndFilterAlertsAsync([Budget()], Cooldown, Now, CancellationToken.None);

        Assert.Equal("alert:budget:monthly:critical", Assert.Single(due).Id);
    }

    [Fact]
    public async Task TheSameAlertIsSuppressedUntilTheCooldownExpires()
    {
        var store = await CreateStoreAsync();

        await store.RecordAndFilterAlertsAsync([Budget()], Cooldown, Now, CancellationToken.None);

        var tooSoon = await store.RecordAndFilterAlertsAsync(
            [Budget()], Cooldown, Now.Add(Cooldown).AddMinutes(-1), CancellationToken.None);
        Assert.Empty(tooSoon);

        var afterwards = await store.RecordAndFilterAlertsAsync(
            [Budget()], Cooldown, Now.Add(Cooldown).AddMinutes(1), CancellationToken.None);
        Assert.Single(afterwards);
    }

    [Fact]
    public async Task ADifferentAlertIsNotBlockedByAnotherOnesCooldown()
    {
        var store = await CreateStoreAsync();
        await store.RecordAndFilterAlertsAsync([Budget()], Cooldown, Now, CancellationToken.None);

        var due = await store.RecordAndFilterAlertsAsync(
            [Budget(), Payment()], Cooldown, Now.AddMinutes(15), CancellationToken.None);

        Assert.Equal("alert:payment:vps", Assert.Single(due).Id);
    }

    [Fact]
    public async Task AMutedAlertIsNeverDelivered()
    {
        var store = await CreateStoreAsync();
        var factory = _factories[^1];
        await store.RecordAndFilterAlertsAsync([Budget()], Cooldown, Now, CancellationToken.None);
        await MuteAsync(factory, "alert:budget:monthly:critical");

        var due = await store.RecordAndFilterAlertsAsync(
            [Budget()], Cooldown, Now.AddDays(30), CancellationToken.None);

        Assert.Empty(due);
    }

    [Fact]
    public async Task AnEmptyCandidateListTouchesNothing()
    {
        var store = await CreateStoreAsync();

        Assert.Empty(await store.RecordAndFilterAlertsAsync([], Cooldown, Now, CancellationToken.None));
    }

    private static Alert Budget() => new(
        "alert:budget:monthly:critical",
        AlertSeverity.Critical,
        "budget",
        "Monthly total al 97%",
        "USD 97.00 de USD 100.00.",
        95m);

    private static Alert Payment() => new(
        "alert:payment:vps",
        AlertSeverity.Info,
        "payment",
        "VPS se cobra mañana",
        "USD 12.00",
        12m);

    private async Task<SqliteLocalStore> CreateStoreAsync()
    {
        var factory = new SqliteConnectionFactory(Path.Combine(_root, $"{Guid.NewGuid():N}.db"));
        _factories.Add(factory);
        var store = new SqliteLocalStore(factory, new SqliteMigrationRunner(factory));
        await store.InitializeAsync(CancellationToken.None);
        return store;
    }

    private static async Task MuteAsync(SqliteConnectionFactory factory, string alertId)
    {
        await using var connection = await factory.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE alerts SET is_enabled = 0 WHERE id = $id;";
        command.Parameters.AddWithValue("$id", alertId);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }
}
