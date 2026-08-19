using DevStatusCenter.Application.Providers;
using DevStatusCenter.Domain.Models;
using DevStatusCenter.Domain.ValueObjects;
using DevStatusCenter.Infrastructure.Persistence;
using DevStatusCenter.Providers.Mock;
using Microsoft.Data.Sqlite;

namespace DevStatusCenter.Tests;

public sealed class SqliteLocalStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 0, 0, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "dev-status-center-tests",
        Guid.NewGuid().ToString("N"));

    private readonly List<SqliteConnectionFactory> _factories = [];

    public SqliteLocalStoreTests()
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
    public async Task ApplyProviderRefresh_PersistsDashboardAndQuickAccess()
    {
        var store = await CreateStoreAsync("cache.db");
        await store.ApplyProviderRefreshAsync(await MockRefreshAsync(Now), CancellationToken.None);
        await store.UpsertQuickAccessAsync(
            new QuickAccessEntry("project:test", "Test project", QuickAccessKind.Project, _root),
            CancellationToken.None);

        var dashboard = await store.ReadDashboardDataAsync("USD", Now, CancellationToken.None);

        Assert.Equal(5, dashboard.Services.Count);
        Assert.Single(dashboard.QuickAccess);
        Assert.All(dashboard.Services, x => Assert.Equal("USD", x.CurrentCost.Currency));
    }

    [Fact]
    public async Task SeedReferenceData_DoesNotOverwriteExistingUserBudget()
    {
        var store = await CreateStoreAsync("seed.db");
        var original = new Budget("monthly", "Monthly", new Money(200m, "USD"));
        var replacement = new Budget("monthly", "Monthly", new Money(999m, "USD"));

        await store.SeedReferenceDataAsync([original], [], [], CancellationToken.None);
        await store.SeedReferenceDataAsync([replacement], [], [], CancellationToken.None);
        var dashboard = await store.ReadDashboardDataAsync(
            "USD",
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.Equal(200m, Assert.Single(dashboard.Budgets).Limit.Amount);
    }

    [Fact]
    public async Task ReadDashboardData_ReturnsTheNewestSnapshotPerService()
    {
        var store = await CreateStoreAsync("latest.db");
        var later = Now.AddDays(6);

        await store.ApplyProviderRefreshAsync(await MockRefreshAsync(Now), CancellationToken.None);
        await store.ApplyProviderRefreshAsync(await MockRefreshAsync(later), CancellationToken.None);

        var dashboard = await store.ReadDashboardDataAsync("USD", later, CancellationToken.None);

        // Una fila por servicio pese a los dos refrescos, y la que corresponde al mas reciente.
        Assert.Equal(5, dashboard.Services.Count);
        Assert.All(dashboard.Services, x => Assert.Equal(later, x.CapturedAt));
    }

    [Fact]
    public async Task ApplyProviderRefresh_IgnoresASnapshotOlderThanTheCurrentOne()
    {
        var store = await CreateStoreAsync("out-of-order.db");
        var earlier = Now.AddDays(-3);

        await store.ApplyProviderRefreshAsync(await MockRefreshAsync(Now), CancellationToken.None);
        await store.ApplyProviderRefreshAsync(await MockRefreshAsync(earlier), CancellationToken.None);

        var dashboard = await store.ReadDashboardDataAsync("USD", Now, CancellationToken.None);

        // Una respuesta que llega tarde no puede hacer retroceder el valor vigente.
        Assert.All(dashboard.Services, x => Assert.Equal(Now, x.CapturedAt));
    }

    [Fact]
    public async Task PruneHistory_DropsOldSnapshotsButKeepsTheDashboardIntact()
    {
        var store = await CreateStoreAsync("prune.db");
        var factory = _factories[^1];

        await store.ApplyProviderRefreshAsync(await MockRefreshAsync(Now), CancellationToken.None);
        var before = await store.ReadDashboardDataAsync("USD", Now, CancellationToken.None);
        Assert.Equal(5, before.Services.Count);

        // Retención mínima: todo el histórico queda fuera de ventana.
        var removed = await store.PruneHistoryAsync(TimeSpan.FromTicks(1), CancellationToken.None);
        Assert.True(removed > 0);
        Assert.Equal(0, await CountAsync(factory, "usage_snapshots"));
        Assert.Equal(0, await CountAsync(factory, "billing_records"));

        var after = await store.ReadDashboardDataAsync("USD", Now, CancellationToken.None);

        // La proyección vigente sobrevive a la poda: el popup nunca queda en blanco.
        Assert.Equal(before.Services.Count, after.Services.Count);
        Assert.All(after.Services, x => Assert.True(x.CurrentCost.Amount >= 0m));
        Assert.NotEmpty(after.Services.SelectMany(x => x.LatestUsage));
    }

    private static Task<ProviderRefreshResult> MockRefreshAsync(DateTimeOffset at) =>
        new MockProvider().RefreshAsync(
            new ProviderRefreshContext(at, false, "USD"),
            CancellationToken.None);

    private async Task<SqliteLocalStore> CreateStoreAsync(string fileName)
    {
        var factory = new SqliteConnectionFactory(Path.Combine(_root, fileName));
        _factories.Add(factory);
        var store = new SqliteLocalStore(factory, new SqliteMigrationRunner(factory));
        await store.InitializeAsync(CancellationToken.None);
        return store;
    }

    private static async Task<long> CountAsync(SqliteConnectionFactory factory, string table)
    {
        await using var connection = await factory.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        return (long)(await command.ExecuteScalarAsync(CancellationToken.None))!;
    }
}
