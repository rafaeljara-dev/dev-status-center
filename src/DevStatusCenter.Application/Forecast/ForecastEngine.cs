using DevStatusCenter.Application.Dashboard;
using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Domain.ValueObjects;

namespace DevStatusCenter.Application.Forecast;

public sealed class ForecastEngine
{
    public ForecastResult Calculate(DashboardCacheData data, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(data);

        var currentVariable = 0m;
        var projectedVariable = 0m;
        var knownFixed = 0m;
        var lines = new List<ForecastLine>(data.Services.Count);

        foreach (var item in data.Services)
        {
            EnsureCurrency(data.Currency, item.CurrentCost.Currency);

            var projected = item.Service.CostBehavior switch
            {
                CostBehavior.Variable => ProjectVariable(item.CurrentCost.Amount, item.Period, now),
                CostBehavior.Fixed => item.CurrentCost.Amount,
                CostBehavior.Mixed => ProjectVariable(item.CurrentCost.Amount, item.Period, now),
                _ => item.CurrentCost.Amount
            };

            if (item.Service.CostBehavior is CostBehavior.Variable or CostBehavior.Mixed)
            {
                currentVariable += item.CurrentCost.Amount;
                projectedVariable += projected;
            }

            lines.Add(new ForecastLine(
                item.Service.Id,
                item.CurrentCost,
                new Money(projected, data.Currency),
                projected == item.CurrentCost.Amount ? item.Accuracy : DataAccuracy.Estimated));
        }

        foreach (var subscription in data.Subscriptions.Where(x => x.IsActive))
        {
            EnsureCurrency(data.Currency, subscription.Price.Currency);
            if (IsInsideCurrentServicePeriod(subscription.NextRenewalAt, data.Services, now))
            {
                knownFixed += subscription.Price.Amount;
            }
        }

        var linkedSubscriptionIds = data.Subscriptions
            .Select(x => x.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var payment in data.UpcomingPayments.Where(x => x.Status == PaymentStatus.Scheduled))
        {
            if (payment.SubscriptionId is not null && linkedSubscriptionIds.Contains(payment.SubscriptionId))
            {
                continue;
            }

            EnsureCurrency(data.Currency, payment.Amount.Currency);
            if (IsInsideCurrentServicePeriod(payment.DueAt, data.Services, now))
            {
                knownFixed += payment.Amount.Amount;
            }
        }

        var currentTotal = data.Services.Sum(x => x.CurrentCost.Amount);
        var total = Math.Max(currentTotal, projectedVariable + knownFixed +
            data.Services.Where(x => x.Service.CostBehavior == CostBehavior.Fixed).Sum(x => x.CurrentCost.Amount));

        return new ForecastResult(
            new Money(currentVariable, data.Currency),
            new Money(projectedVariable, data.Currency),
            new Money(knownFixed, data.Currency),
            new Money(decimal.Round(total, 2, MidpointRounding.AwayFromZero), data.Currency),
            lines);
    }

    private static decimal ProjectVariable(decimal amount, BillingPeriod period, DateTimeOffset now)
    {
        if (now <= period.StartsAt)
        {
            return amount;
        }

        var elapsedDays = Math.Max(1d, (Math.Min(now, period.EndsAt) - period.StartsAt).TotalDays);
        var totalDays = (period.EndsAt - period.StartsAt).TotalDays;
        var projected = amount / (decimal)elapsedDays * (decimal)totalDays;
        return decimal.Round(Math.Max(amount, projected), 2, MidpointRounding.AwayFromZero);
    }

    private static bool IsInsideCurrentServicePeriod(
        DateTimeOffset date,
        IReadOnlyList<CachedServiceCost> services,
        DateTimeOffset now)
    {
        var period = services.FirstOrDefault()?.Period;
        if (period is null)
        {
            var monthEnd = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1);
            return date >= now && date < monthEnd;
        }

        return date >= now && date < period.EndsAt;
    }

    private static void EnsureCurrency(string expected, string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Forecast requires normalized {expected} amounts; found {actual}. Currency conversion belongs upstream.");
        }
    }
}

