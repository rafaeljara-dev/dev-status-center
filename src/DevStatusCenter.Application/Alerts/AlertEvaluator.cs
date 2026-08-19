using System.Globalization;
using DevStatusCenter.Application.Dashboard;
using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Domain.Models;

namespace DevStatusCenter.Application.Alerts;

/// <summary>
/// Deriva alertas del estado ya calculado. Es una función pura: no consulta nada, no guarda nada
/// y no decide si notificar -- eso lo resuelve el enfriamiento en <c>ILocalStore</c>. Aquí sólo
/// se responde "¿qué es cierto ahora mismo?".
/// </summary>
public static class AlertEvaluator
{
    /// <summary>
    /// Cuánto callar sobre una alerta ya notificada. Doce horas dejan como mucho dos avisos al
    /// día del mismo presupuesto: suficiente para no olvidarlo y poco para no volverse ruido.
    /// </summary>
    public static readonly TimeSpan DefaultCooldown = TimeSpan.FromHours(12);

    /// <summary>Ventana para avisar de un pago próximo.</summary>
    public static readonly TimeSpan PaymentHorizon = TimeSpan.FromDays(3);

    /// <summary>
    /// Fallos consecutivos antes de molestar por un provider. Uno solo suele ser un hipo de red y
    /// el backoff ya lo reintenta; avisar al primero convertiría la app en ruido.
    /// </summary>
    private const int ProviderFailuresBeforeAlerting = 3;

    public static IReadOnlyList<Alert> Evaluate(DashboardSnapshot snapshot, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // Sin un sync exitoso todavía no hay nada que afirmar. Alertar sobre caché vacío diría
        // "gastaste 0" en vez de "aún no sé".
        if (snapshot.LastSuccessfulSync is null)
        {
            return [];
        }

        var alerts = new List<Alert>();
        AddBudgetAlerts(alerts, snapshot);
        AddPaymentAlerts(alerts, snapshot, now);
        AddProviderAlerts(alerts, snapshot);
        return alerts;
    }

    private static void AddBudgetAlerts(List<Alert> alerts, DashboardSnapshot snapshot)
    {
        foreach (var budget in snapshot.Budgets)
        {
            if (budget.Limit.Amount <= 0m)
            {
                continue;
            }

            var current = SpendFor(snapshot, budget);
            var percent = current / budget.Limit.Amount * 100m;

            // Sólo el umbral más alto alcanzado. Cruzar el crítico no debe disparar además el
            // importante y el de aviso: sería la misma noticia contada tres veces.
            var crossed = Highest(percent, budget);
            if (crossed is { } level)
            {
                alerts.Add(new Alert(
                    $"alert:budget:{budget.Id}:{level.Name}",
                    level.Severity,
                    "budget",
                    $"{budget.Name} al {Format(percent)}%",
                    $"{Money(current, budget.Limit.Currency)} de {Money(budget.Limit.Amount, budget.Limit.Currency)}.",
                    level.Percent,
                    budget.ServiceId));
            }

            // La proyección avisa antes de que ocurra: es la diferencia entre enterarse a tiempo
            // y enterarse cuando ya te pasaste.
            var projected = ProjectedFor(snapshot, budget);
            if (crossed?.Severity != AlertSeverity.Critical &&
                projected > budget.Limit.Amount &&
                current <= budget.Limit.Amount)
            {
                alerts.Add(new Alert(
                    $"alert:forecast:{budget.Id}",
                    AlertSeverity.Warning,
                    "forecast",
                    $"{budget.Name} va camino de excederse",
                    $"Proyección {Money(projected, budget.Limit.Currency)} contra un límite de " +
                    $"{Money(budget.Limit.Amount, budget.Limit.Currency)}.",
                    budget.Limit.Amount,
                    budget.ServiceId));
            }
        }
    }

    private static void AddPaymentAlerts(
        List<Alert> alerts,
        DashboardSnapshot snapshot,
        DateTimeOffset now)
    {
        foreach (var payment in snapshot.UpcomingPayments)
        {
            if (payment.Status != PaymentStatus.Scheduled)
            {
                continue;
            }

            var remaining = payment.DueAt - now;
            if (remaining < TimeSpan.Zero || remaining > PaymentHorizon)
            {
                continue;
            }

            var days = (int)Math.Ceiling(remaining.TotalDays);
            alerts.Add(new Alert(
                $"alert:payment:{payment.Id}",
                AlertSeverity.Info,
                "payment",
                days <= 1 ? $"{payment.Name} se cobra mañana" : $"{payment.Name} se cobra en {days} días",
                Money(payment.Amount.Amount, payment.Amount.Currency),
                payment.Amount.Amount));
        }
    }

    private static void AddProviderAlerts(List<Alert> alerts, DashboardSnapshot snapshot)
    {
        foreach (var provider in snapshot.ProviderStates)
        {
            switch (provider.Status)
            {
                // La autenticación no se arregla sola por más que se reintente: hay que avisar ya.
                case ProviderStatus.AuthenticationRequired:
                    alerts.Add(new Alert(
                        $"alert:provider:{provider.ProviderId}:auth",
                        AlertSeverity.Important,
                        "provider",
                        $"{provider.ProviderId} necesita credenciales",
                        "El token fue rechazado. Revísalo en Providers & credentials.",
                        0m));
                    break;

                case ProviderStatus.Error when provider.ConsecutiveFailures >= ProviderFailuresBeforeAlerting:
                    alerts.Add(new Alert(
                        $"alert:provider:{provider.ProviderId}:error",
                        AlertSeverity.Warning,
                        "provider",
                        $"{provider.ProviderId} lleva {provider.ConsecutiveFailures} fallos seguidos",
                        provider.ErrorMessage ?? "Los datos que ves son el último valor bueno.",
                        provider.ConsecutiveFailures));
                    break;

                default:
                    break;
            }
        }
    }

    private static decimal SpendFor(DashboardSnapshot snapshot, Budget budget) => budget switch
    {
        { ServiceId: { } serviceId } => snapshot.Services
            .Where(x => string.Equals(x.Id, serviceId, StringComparison.Ordinal))
            .Sum(x => x.Current.Amount),
        { Category: { } category } => snapshot.Categories
            .Where(x => x.Category == category)
            .Sum(x => x.Current.Amount),
        _ => snapshot.CurrentSpend.Amount
    };

    private static decimal ProjectedFor(DashboardSnapshot snapshot, Budget budget) => budget switch
    {
        { ServiceId: { } serviceId } => snapshot.Services
            .Where(x => string.Equals(x.Id, serviceId, StringComparison.Ordinal))
            .Sum(x => x.Projected.Amount),
        { Category: { } category } => snapshot.Categories
            .Where(x => x.Category == category)
            .Sum(x => x.Projected.Amount),
        _ => snapshot.ProjectedSpend.Amount
    };

    private static (string Name, AlertSeverity Severity, decimal Percent)? Highest(decimal percent, Budget budget)
    {
        if (percent >= budget.CriticalPercent)
        {
            return ("critical", AlertSeverity.Critical, budget.CriticalPercent);
        }

        if (percent >= budget.ImportantPercent)
        {
            return ("important", AlertSeverity.Important, budget.ImportantPercent);
        }

        return percent >= budget.WarningPercent
            ? ("warning", AlertSeverity.Warning, budget.WarningPercent)
            : null;
    }

    private static string Format(decimal value) =>
        decimal.Round(value, 1).ToString("0.#", CultureInfo.InvariantCulture);

    private static string Money(decimal amount, string currency) =>
        string.Format(CultureInfo.InvariantCulture, "{0} {1:N2}", currency, amount);
}
