using DevStatusCenter.Application.Abstractions;
using DevStatusCenter.Application.Forecast;
using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Domain.ValueObjects;

namespace DevStatusCenter.Application.Dashboard;

public sealed class DashboardService(
    ILocalStore store,
    ForecastEngine forecastEngine,
    TimeProvider timeProvider,
    string displayCurrency)
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(2);

    public async Task<DashboardSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var data = await store.ReadDashboardDataAsync(displayCurrency, now, cancellationToken);
        var forecast = forecastEngine.Calculate(data, now);
        var forecastByService = forecast.Lines.ToDictionary(x => x.ServiceId, StringComparer.Ordinal);

        var services = data.Services.Select(item =>
        {
            var line = forecastByService[item.Service.Id];
            return new DashboardServiceRow(
                item.Service.Id,
                item.Service.Name,
                item.Service.Category,
                item.CurrentCost,
                line.Projected,
                item.Source,
                item.Accuracy,
                item.CapturedAt,
                item.LatestUsage);
        }).ToArray();

        var categories = services
            .GroupBy(x => x.Category)
            .Select(group => new DashboardCategoryRow(
                group.Key,
                new Money(group.Sum(x => x.Current.Amount), displayCurrency),
                new Money(group.Sum(x => x.Projected.Amount), displayCurrency)))
            .OrderByDescending(x => x.Current.Amount)
            .ToArray();

        var current = new Money(services.Sum(x => x.Current.Amount), displayCurrency);
        var budget = data.Budgets.FirstOrDefault(x => x.ServiceId is null && x.Category is null)?.Limit;
        var percent = budget is { Amount: > 0m }
            ? decimal.Round(current.Amount / budget.Value.Amount * 100m, 1)
            : null;
        var stale = data.LastSuccessfulSync is null || now - data.LastSuccessfulSync > StaleAfter;

        return new DashboardSnapshot(
            displayCurrency,
            current,
            forecast.ProjectedTotal,
            budget,
            percent,
            categories,
            services,
            data.UpcomingPayments,
            data.QuickAccess,
            data.ProviderStates,
            data.LastSuccessfulSync,
            stale);
    }
}
