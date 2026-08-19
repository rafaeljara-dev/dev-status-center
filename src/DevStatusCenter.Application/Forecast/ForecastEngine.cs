using DevStatusCenter.Application.Dashboard;
using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Domain.ValueObjects;

namespace DevStatusCenter.Application.Forecast;

/// <summary>
/// Modelo inicial: proyección lineal del consumo variable sobre el periodo de facturación,
/// más las obligaciones fijas que caen dentro del mismo horizonte. Es una función pura; se
/// volverá una instancia con opciones cuando llegue el modelo de media móvil (MVP 5).
/// </summary>
public static class ForecastEngine
{
    public static ForecastResult Calculate(DashboardCacheData data, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(data);

        var currentVariable = 0m;
        var projectedVariable = 0m;
        var knownFixed = 0m;
        var fixedServiceCost = 0m;
        var currentTotal = 0m;
        var lines = new List<ForecastLine>(data.Services.Count);

        // El horizonte se calculaba dentro del bucle, una vez por suscripción y por pago.
        // Es el mismo valor para todos: se resuelve una sola vez.
        var horizonEnd = data.Services.Count > 0
            ? data.Services[0].Period.EndsAt
            : new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1);

        foreach (var item in data.Services)
        {
            EnsureCurrency(data.Currency, item.CurrentCost.Currency);
            currentTotal += item.CurrentCost.Amount;

            var projected = item.Service.CostBehavior switch
            {
                CostBehavior.Variable or CostBehavior.Mixed =>
                    ProjectVariable(item.CurrentCost.Amount, item.Period, now),
                _ => item.CurrentCost.Amount
            };

            if (item.Service.CostBehavior is CostBehavior.Variable or CostBehavior.Mixed)
            {
                currentVariable += item.CurrentCost.Amount;
                projectedVariable += projected;
            }
            else if (item.Service.CostBehavior == CostBehavior.Fixed)
            {
                fixedServiceCost += item.CurrentCost.Amount;
            }

            lines.Add(new ForecastLine(
                item.Service.Id,
                item.CurrentCost,
                new Money(projected, data.Currency),
                projected == item.CurrentCost.Amount ? item.Accuracy : DataAccuracy.Estimated));
        }

        var linkedSubscriptionIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var subscription in data.Subscriptions)
        {
            linkedSubscriptionIds.Add(subscription.Id);
            if (!subscription.IsActive)
            {
                continue;
            }

            EnsureCurrency(data.Currency, subscription.Price.Currency);
            if (IsInsideHorizon(subscription.NextRenewalAt, now, horizonEnd))
            {
                knownFixed += subscription.Price.Amount;
            }
        }

        foreach (var payment in data.UpcomingPayments)
        {
            if (payment.Status != PaymentStatus.Scheduled)
            {
                continue;
            }

            // Un pago enlazado a una suscripción ya se contó arriba: no se duplica.
            if (payment.SubscriptionId is not null && linkedSubscriptionIds.Contains(payment.SubscriptionId))
            {
                continue;
            }

            EnsureCurrency(data.Currency, payment.Amount.Currency);
            if (IsInsideHorizon(payment.DueAt, now, horizonEnd))
            {
                knownFixed += payment.Amount.Amount;
            }
        }

        var total = Math.Max(currentTotal, projectedVariable + knownFixed + fixedServiceCost);

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

        var cutoff = now < period.EndsAt ? now : period.EndsAt;
        var elapsedDays = Math.Max(1d, (cutoff - period.StartsAt).TotalDays);
        var totalDays = (period.EndsAt - period.StartsAt).TotalDays;
        var projected = amount / (decimal)elapsedDays * (decimal)totalDays;

        // La proyección nunca puede quedar por debajo de lo ya gastado (FR-032).
        return decimal.Round(Math.Max(amount, projected), 2, MidpointRounding.AwayFromZero);
    }

    private static bool IsInsideHorizon(DateTimeOffset date, DateTimeOffset now, DateTimeOffset horizonEnd) =>
        date >= now && date < horizonEnd;

    private static void EnsureCurrency(string expected, string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Forecast requires normalized {expected} amounts; found {actual}. Currency conversion belongs upstream.");
        }
    }
}
