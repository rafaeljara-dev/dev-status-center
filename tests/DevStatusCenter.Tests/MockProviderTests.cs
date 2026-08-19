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
}

