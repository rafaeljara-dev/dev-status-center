using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Domain.Models;
using DevStatusCenter.Domain.ValueObjects;

namespace DevStatusCenter.Application.Dashboard;

public sealed record CachedServiceCost(
    Service Service,
    Money CurrentCost,
    BillingPeriod Period,
    DateTimeOffset CapturedAt,
    DataSourceKind Source,
    DataAccuracy Accuracy,
    IReadOnlyList<UsageSnapshot> LatestUsage);

public sealed record DashboardCacheData(
    string Currency,
    IReadOnlyList<CachedServiceCost> Services,
    IReadOnlyList<Budget> Budgets,
    IReadOnlyList<Subscription> Subscriptions,
    IReadOnlyList<Payment> UpcomingPayments,
    IReadOnlyList<QuickAccessEntry> QuickAccess,
    IReadOnlyList<ProviderState> ProviderStates,
    DateTimeOffset? LastSuccessfulSync);

public sealed record ForecastLine(
    string ServiceId,
    Money Current,
    Money Projected,
    DataAccuracy Accuracy);

public sealed record ForecastResult(
    Money CurrentVariable,
    Money ProjectedVariable,
    Money KnownFixed,
    Money ProjectedTotal,
    IReadOnlyList<ForecastLine> Lines);

public sealed record DashboardServiceRow(
    string Id,
    string Name,
    // La UI resuelve la marca por ProviderId y, si no la conoce, por ExternalId. Sin estos dos
    // campos el dashboard solo tiene un nombre para mostrar, que no basta para elegir un logo.
    string ProviderId,
    string ExternalId,
    ServiceCategory Category,
    Money Current,
    Money Projected,
    DataSourceKind Source,
    DataAccuracy Accuracy,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<UsageSnapshot> Usage,
    /// <summary>Falso para los planes de tarifa plana: se muestra la cuota, nunca un importe.</summary>
    bool TracksCost);

public sealed record DashboardCategoryRow(
    ServiceCategory Category,
    Money Current,
    Money Projected);

public sealed record DashboardSnapshot(
    string Currency,
    Money CurrentSpend,
    Money ProjectedSpend,
    Money? MonthlyBudget,
    decimal? BudgetPercent,
    IReadOnlyList<Budget> Budgets,
    IReadOnlyList<DashboardCategoryRow> Categories,
    IReadOnlyList<DashboardServiceRow> Services,
    IReadOnlyList<Payment> UpcomingPayments,
    IReadOnlyList<QuickAccessEntry> QuickAccess,
    IReadOnlyList<ProviderState> ProviderStates,
    IReadOnlyList<ServiceHealth> Health,
    DateTimeOffset? LastSuccessfulSync,
    bool IsStale);
