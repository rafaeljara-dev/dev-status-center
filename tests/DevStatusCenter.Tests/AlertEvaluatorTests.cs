using DevStatusCenter.Application.Alerts;
using DevStatusCenter.Application.Dashboard;
using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Domain.Models;
using DevStatusCenter.Domain.ValueObjects;

namespace DevStatusCenter.Tests;

public sealed class AlertEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Evaluate_StaysSilentBeforeTheFirstSuccessfulSync()
    {
        var snapshot = Snapshot(current: 400m, projected: 400m, hasSynced: false);

        Assert.Empty(AlertEvaluator.Evaluate(snapshot, Now));
    }

    [Theory]
    [InlineData(60, null)]
    [InlineData(72, AlertSeverity.Warning)]
    [InlineData(88, AlertSeverity.Important)]
    [InlineData(97, AlertSeverity.Critical)]
    public void Evaluate_RaisesOnlyTheHighestBudgetThresholdCrossed(int spent, AlertSeverity? expected)
    {
        var snapshot = Snapshot(current: spent, projected: spent);

        var budget = AlertEvaluator.Evaluate(snapshot, Now)
            .Where(x => x.RuleType == "budget")
            .ToArray();

        if (expected is null)
        {
            Assert.Empty(budget);
            return;
        }

        // Cruzar el crítico no debe además disparar el importante y el de aviso.
        Assert.Equal(expected, Assert.Single(budget).Severity);
    }

    [Fact]
    public void Evaluate_WarnsWhenTheForecastOverrunsWhileSpendIsStillUnder()
    {
        var snapshot = Snapshot(current: 40m, projected: 130m);

        var forecast = Assert.Single(AlertEvaluator.Evaluate(snapshot, Now), x => x.RuleType == "forecast");

        Assert.Equal(AlertSeverity.Warning, forecast.Severity);
        Assert.Contains("130", forecast.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_DoesNotAddAForecastWarningOnTopOfACriticalBudget()
    {
        var snapshot = Snapshot(current: 98m, projected: 140m);

        var alerts = AlertEvaluator.Evaluate(snapshot, Now);

        Assert.Contains(alerts, x => x.Severity == AlertSeverity.Critical);
        Assert.DoesNotContain(alerts, x => x.RuleType == "forecast");
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(5, false)]
    public void Evaluate_AnnouncesOnlyPaymentsInsideTheHorizon(int daysAway, bool expected)
    {
        var snapshot = Snapshot(
            current: 10m,
            projected: 10m,
            payments: [new Payment("p1", "Claude", new Money(20m, "USD"), Now.AddDays(daysAway), PaymentStatus.Scheduled)]);

        var payments = AlertEvaluator.Evaluate(snapshot, Now).Where(x => x.RuleType == "payment");

        Assert.Equal(expected, payments.Any());
    }

    [Fact]
    public void Evaluate_IgnoresAPaymentThatIsAlreadyPaid()
    {
        var snapshot = Snapshot(
            current: 10m,
            projected: 10m,
            payments: [new Payment("p1", "Claude", new Money(20m, "USD"), Now.AddDays(1), PaymentStatus.Paid)]);

        Assert.DoesNotContain(AlertEvaluator.Evaluate(snapshot, Now), x => x.RuleType == "payment");
    }

    [Fact]
    public void Evaluate_FlagsAProviderThatNeedsCredentialsImmediately()
    {
        var snapshot = Snapshot(
            current: 10m,
            projected: 10m,
            providers: [State("neon", ProviderStatus.AuthenticationRequired, failures: 1)]);

        var alert = Assert.Single(AlertEvaluator.Evaluate(snapshot, Now), x => x.RuleType == "provider");

        Assert.Equal(AlertSeverity.Important, alert.Severity);
        Assert.Contains("neon", alert.Title, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public void Evaluate_WaitsForRepeatedFailuresBeforeComplainingAboutAProvider(int failures, bool expected)
    {
        var snapshot = Snapshot(
            current: 10m,
            projected: 10m,
            providers: [State("vercel", ProviderStatus.Error, failures)]);

        // Un fallo suelto suele ser un hipo de red y el backoff ya lo reintenta.
        Assert.Equal(expected, AlertEvaluator.Evaluate(snapshot, Now).Any(x => x.RuleType == "provider"));
    }

    [Fact]
    public void Evaluate_KeepsAlertIdsStableAcrossCycles()
    {
        var first = AlertEvaluator.Evaluate(Snapshot(current: 97m, projected: 97m), Now);
        var second = AlertEvaluator.Evaluate(Snapshot(current: 98m, projected: 98m), Now.AddMinutes(30));

        // Los ids no llevan el instante: es lo que permite reconocer la misma alerta y callar.
        Assert.Equal(first.Select(x => x.Id), second.Select(x => x.Id));
    }

    private static ProviderState State(string id, ProviderStatus status, int failures) =>
        new(id, status, Now, Now, Now, failures, null, "boom");

    private static DashboardSnapshot Snapshot(
        decimal current,
        decimal projected,
        bool hasSynced = true,
        IReadOnlyList<Payment>? payments = null,
        IReadOnlyList<ProviderState>? providers = null)
    {
        var budget = new Budget("budget:monthly", "Monthly total", new Money(100m, "USD"));
        return new DashboardSnapshot(
            "USD",
            new Money(current, "USD"),
            new Money(projected, "USD"),
            budget.Limit,
            current,
            [budget],
            [],
            [],
            payments ?? [],
            [],
            providers ?? [],
            hasSynced ? Now.AddMinutes(-5) : null,
            IsStale: false);
    }
}
