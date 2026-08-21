using DevStatusCenter.Application.Providers;
using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Providers.Mock;

namespace DevStatusCenter.Tests;

public sealed class MockProviderTests
{
    [Fact]
    public async Task RefreshAsync_ReturnsNormalizedAiAndInfrastructureData()
    {
        var provider = new MockProvider();
        var now = new DateTimeOffset(2026, 8, 19, 0, 0, 0, TimeSpan.Zero);

        var result = await provider.RefreshAsync(
            new ProviderRefreshContext(now, false, "USD"),
            CancellationToken.None);

        Assert.Single(result.Accounts);
        Assert.Contains(result.Observations, x => x.Service.Category == ServiceCategory.Ai);
        Assert.Contains(result.Observations, x => x.Service.Category == ServiceCategory.Infrastructure);
        Assert.All(result.Observations, x => Assert.Single(x.Billing));
        Assert.Contains(
            result.Observations.SelectMany(x => x.Usage),
            x => x.Metric.Kind == MetricKind.QuotaConsumed);
    }

    /// <summary>
    /// Con filtro, el provider de demostracion solo rellena los huecos que se le piden: ni una
    /// fila mas, ni los pagos, que pertenecen a los servicios que quedaron fuera.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_WithAServiceFilter_ReturnsOnlyThoseServicesAndNoPayments()
    {
        var provider = new MockProvider(["vercel", "cloudflare"]);
        var now = new DateTimeOffset(2026, 8, 19, 0, 0, 0, TimeSpan.Zero);

        var result = await provider.RefreshAsync(
            new ProviderRefreshContext(now, false, "USD"),
            CancellationToken.None);

        Assert.Equal(
            ["cloudflare", "vercel"],
            result.Observations.Select(x => x.Service.ExternalId).OrderBy(x => x, StringComparer.Ordinal));
        Assert.Empty(result.Payments);
        Assert.Empty(result.Subscriptions);
    }
}

