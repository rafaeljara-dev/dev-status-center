using DevStatusCenter.Application.Dashboard;
using DevStatusCenter.Application.Forecast;
using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Domain.Models;
using DevStatusCenter.Domain.ValueObjects;

namespace DevStatusCenter.Tests;

public sealed class ForecastEngineTests
{
    [Fact]
    public void Calculate_CombinesVariableUsageSubscriptionsAndUnlinkedPayments()
    {
        var now = new DateTimeOffset(2026, 8, 19, 0, 0, 0, TimeSpan.Zero);
        var period = new BillingPeriod(
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            "UTC");
        var service = new Service(
            "openai",
            "mock",
            "personal",
            "openai",
            "OpenAI",
            ServiceCategory.Ai,
            CostBehavior.Variable);
        var cache = new DashboardCacheData(
            "USD",
            [new CachedServiceCost(
                service,
                new Money(18m, "USD"),
                period,
                now,
                DataSourceKind.OfficialBillingApi,
                DataAccuracy.ProviderReported,
                [])],
            [],
            [new Subscription(
                "claude",
                "Claude",
                new Money(20m, "USD"),
                BillingCadence.Monthly,
                now.AddDays(2),
                DataSourceKind.Manual)],
            [
                new Payment(
                    "claude-payment",
                    "Claude",
                    new Money(20m, "USD"),
                    now.AddDays(2),
                    PaymentStatus.Scheduled,
                    "claude"),
                new Payment(
                    "vps-payment",
                    "VPS",
                    new Money(12m, "USD"),
                    now.AddDays(3),
                    PaymentStatus.Scheduled)
            ],
            [],
            [],
            now);

        var result = ForecastEngine.Calculate(cache, now);

        Assert.Equal(31m, result.ProjectedVariable.Amount);
        Assert.Equal(32m, result.KnownFixed.Amount);
        Assert.Equal(63m, result.ProjectedTotal.Amount);
    }

    [Fact]
    public void Calculate_RejectsUnconvertedCurrency()
    {
        var now = new DateTimeOffset(2026, 8, 19, 0, 0, 0, TimeSpan.Zero);
        var period = new BillingPeriod(now.AddDays(-1), now.AddDays(10), "UTC");
        var service = new Service(
            "service",
            "provider",
            "account",
            "external",
            "Service",
            ServiceCategory.Other,
            CostBehavior.Variable);
        var cache = new DashboardCacheData(
            "USD",
            [new CachedServiceCost(
                service,
                new Money(100m, "MXN"),
                period,
                now,
                DataSourceKind.Manual,
                DataAccuracy.Manual,
                [])],
            [], [], [], [], [], null);

        Assert.Throws<InvalidOperationException>(() => ForecastEngine.Calculate(cache, now));
    }
}

