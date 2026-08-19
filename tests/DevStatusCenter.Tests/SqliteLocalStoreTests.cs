using DevStatusCenter.Application.Providers;
using DevStatusCenter.Domain.Models;
using DevStatusCenter.Domain.ValueObjects;
using DevStatusCenter.Infrastructure.Persistence;
using DevStatusCenter.Providers.Mock;
using Microsoft.Data.Sqlite;

namespace DevStatusCenter.Tests;

public sealed class SqliteLocalStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dev-status-center-tests", Guid.NewGuid().ToString("N"));

    public SqliteLocalStoreTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyProviderRefresh_PersistsDashboardAndQuickAccess()
    {
        var factory = new SqliteConnectionFactory(Path.Combine(_root, "cache.db"));
        var store = new SqliteLocalStore(factory, new SqliteMigrationRunner(factory));
        await store.InitializeAsync(CancellationToken.None);
        var now = new DateTimeOffset(2026, 8, 19, 0, 0, 0, TimeSpan.Zero);
        var result = await new MockProvider().RefreshAsync(
            new ProviderRefreshContext(now, false, "USD"),
            CancellationToken.None);
        await store.ApplyProviderRefreshAsync(result, CancellationToken.None);
        await store.UpsertQuickAccessAsync(new QuickAccessEntry(
            "project:test",
            "Test project",
            QuickAccessKind.Project,
            _root), CancellationToken.None);

        var dashboard = await store.ReadDashboardDataAsync("USD", now, CancellationToken.None);

        Assert.Equal(5, dashboard.Services.Count);
        Assert.Single(dashboard.QuickAccess);
        Assert.All(dashboard.Services, x => Assert.Equal("USD", x.CurrentCost.Currency));
    }

    [Fact]
    public async Task SeedReferenceData_DoesNotOverwriteExistingUserBudget()
    {
        var factory = new SqliteConnectionFactory(Path.Combine(_root, "seed.db"));
        var store = new SqliteLocalStore(factory, new SqliteMigrationRunner(factory));
        await store.InitializeAsync(CancellationToken.None);
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
}
