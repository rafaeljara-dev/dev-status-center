using DevStatusCenter.Application.Dashboard;
using DevStatusCenter.Application.Providers;
using DevStatusCenter.Domain.Models;

namespace DevStatusCenter.Application.Abstractions;

public interface ILocalStore
{
    Task InitializeAsync(CancellationToken cancellationToken);

    Task ApplyProviderRefreshAsync(
        ProviderRefreshResult result,
        CancellationToken cancellationToken);

    Task<DashboardCacheData> ReadDashboardDataAsync(
        string displayCurrency,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<ProviderState?> ReadProviderStateAsync(
        string providerId,
        CancellationToken cancellationToken);

    Task WriteProviderStateAsync(
        ProviderState state,
        CancellationToken cancellationToken);

    Task SeedReferenceDataAsync(
        IReadOnlyCollection<Budget> budgets,
        IReadOnlyCollection<Subscription> subscriptions,
        IReadOnlyCollection<Payment> payments,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<QuickAccessEntry>> ReadQuickAccessAsync(
        CancellationToken cancellationToken);

    Task UpsertQuickAccessAsync(
        QuickAccessEntry entry,
        CancellationToken cancellationToken);

    Task DeleteQuickAccessAsync(
        string id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Borra snapshots de usage/billing e historial de refresh mas viejos que
    /// <paramref name="retention"/>. La proyeccion vigente que alimenta el dashboard no se
    /// toca, asi que podar nunca deja la UI en blanco. Devuelve las filas eliminadas.
    /// </summary>
    Task<int> PruneHistoryAsync(
        TimeSpan retention,
        CancellationToken cancellationToken);
}
