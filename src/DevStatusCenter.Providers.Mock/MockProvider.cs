using DevStatusCenter.Application.Providers;
using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Domain.Models;
using DevStatusCenter.Domain.ValueObjects;

namespace DevStatusCenter.Providers.Mock;

public sealed class MockProvider : IProvider, IUsageProvider, IBillingProvider, IQuotaProvider
{
    private const string ProviderId = "mock";
    private const string AccountId = "mock-personal";

    public ProviderDescriptor Descriptor { get; } = new(
        ProviderId,
        "Demo data",
        ProviderCapabilities.Usage | ProviderCapabilities.Billing | ProviderCapabilities.Quota,
        new RefreshPolicy(
            minimumInterval: TimeSpan.FromSeconds(10),
            normalInterval: TimeSpan.FromMinutes(15),
            ecoInterval: TimeSpan.FromHours(1)));

    public async Task<ProviderRefreshResult> RefreshAsync(
        ProviderRefreshContext context,
        CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(80), cancellationToken);
        var observations = BuildObservations(context);
        return new ProviderRefreshResult(
            ProviderId,
            context.RequestedAt,
            [Account()],
            observations,
            BuildSubscriptions(context.RequestedAt),
            BuildPayments(context.RequestedAt));
    }

    public Task<IReadOnlyList<ServiceObservation>> GetUsageAsync(
        ProviderRefreshContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<ServiceObservation> result = BuildObservations(context)
            .Select(x => x with { Billing = [] })
            .ToArray();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<ServiceObservation>> GetBillingAsync(
        ProviderRefreshContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<ServiceObservation> result = BuildObservations(context)
            .Select(x => x with { Usage = [] })
            .ToArray();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<UsageSnapshot>> GetQuotasAsync(
        ProviderRefreshContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<UsageSnapshot> result = BuildObservations(context)
            .SelectMany(x => x.Usage)
            .Where(x => x.Metric.Kind == MetricKind.QuotaConsumed)
            .ToArray();
        return Task.FromResult(result);
    }

    private static ProviderAccount Account() => new(
        AccountId,
        ProviderId,
        "Personal (demo)",
        "demo-account",
        credentialReference: null);

    private static IReadOnlyList<ServiceObservation> BuildObservations(ProviderRefreshContext context)
    {
        var period = CurrentMonth(context.RequestedAt);
        var elapsedDays = Math.Max(1d, (context.RequestedAt - period.StartsAt).TotalDays);
        var capturedAt = context.RequestedAt;

        return
        [
            CreateAiObservation(
                "openai",
                "OpenAI",
                1.85m + (decimal)elapsedDays * 0.82m,
                4_200_000m + (decimal)elapsedDays * 18_000m,
                812_000m + (decimal)elapsedDays * 4_100m,
                1_800_000m,
                37m,
                capturedAt,
                period),
            CreateAiObservation(
                "anthropic",
                "Anthropic",
                0.75m + (decimal)elapsedDays * 0.57m,
                2_100_000m + (decimal)elapsedDays * 11_000m,
                490_000m + (decimal)elapsedDays * 2_600m,
                0m,
                29m,
                capturedAt,
                period),
            CreateInfrastructureObservation(
                "vercel",
                "Vercel",
                0.40m + (decimal)elapsedDays * 0.36m,
                [("bandwidth", "Bandwidth", MetricKind.DataTransfer, "GB", 38.4m),
                 ("requests", "Edge requests", MetricKind.Requests, "requests", 780_000m)],
                capturedAt,
                period),
            CreateInfrastructureObservation(
                "neon",
                "Neon",
                0.22m + (decimal)elapsedDays * 0.16m,
                [("compute", "Compute", MetricKind.Compute, "CU-hours", 21.8m),
                 ("storage", "Storage", MetricKind.Storage, "GB-month", 4.2m)],
                capturedAt,
                period),
            CreateInfrastructureObservation(
                "cloudflare",
                "Cloudflare",
                (decimal)elapsedDays * 0.05m,
                [("r2-storage", "R2 storage", MetricKind.Storage, "GB-month", 1.7m),
                 ("workers", "Workers requests", MetricKind.Requests, "requests", 240_000m)],
                capturedAt,
                period)
        ];
    }

    private static ServiceObservation CreateAiObservation(
        string externalId,
        string name,
        decimal cost,
        decimal inputTokens,
        decimal outputTokens,
        decimal cachedTokens,
        decimal quotaPercent,
        DateTimeOffset capturedAt,
        BillingPeriod period)
    {
        var service = Service(externalId, name, ServiceCategory.Ai);
        var metrics = new List<UsageSnapshot>
        {
            Usage(service.Id, "input-tokens", "Input", MetricKind.TokensInput, "tokens", inputTokens, capturedAt, period),
            Usage(service.Id, "output-tokens", "Output", MetricKind.TokensOutput, "tokens", outputTokens, capturedAt, period),
            Usage(service.Id, "quota", "Quota consumed", MetricKind.QuotaConsumed, "percent", quotaPercent, capturedAt, period)
        };
        if (cachedTokens > 0m)
        {
            metrics.Add(Usage(
                service.Id,
                "cached-tokens",
                "Cached",
                MetricKind.TokensCached,
                "tokens",
                cachedTokens,
                capturedAt,
                period));
        }

        return Observation(service, decimal.Round(cost, 2), metrics, capturedAt, period);
    }

    private static ServiceObservation CreateInfrastructureObservation(
        string externalId,
        string name,
        decimal cost,
        IReadOnlyList<(string Code, string Name, MetricKind Kind, string Unit, decimal Value)> rawMetrics,
        DateTimeOffset capturedAt,
        BillingPeriod period)
    {
        var service = Service(externalId, name, ServiceCategory.Infrastructure);
        var metrics = rawMetrics
            .Select(metric => Usage(
                service.Id,
                metric.Code,
                metric.Name,
                metric.Kind,
                metric.Unit,
                metric.Value,
                capturedAt,
                period))
            .ToArray();
        return Observation(service, decimal.Round(cost, 2), metrics, capturedAt, period);
    }

    private static ServiceObservation Observation(
        Service service,
        decimal cost,
        IReadOnlyList<UsageSnapshot> usage,
        DateTimeOffset capturedAt,
        BillingPeriod period) =>
        new(
            service,
            usage,
            [new BillingRecord(
                SnapshotId(service.Id, "billing", capturedAt),
                service.Id,
                new Money(cost, "USD"),
                period,
                capturedAt,
                DataSourceKind.Mock,
                DataAccuracy.Estimated)]);

    private static Service Service(string externalId, string name, ServiceCategory category) =>
        new(
            $"{ProviderId}:{AccountId}:{externalId}",
            ProviderId,
            AccountId,
            externalId,
            name,
            category,
            CostBehavior.Variable);

    private static UsageSnapshot Usage(
        string serviceId,
        string code,
        string name,
        MetricKind kind,
        string unit,
        decimal value,
        DateTimeOffset capturedAt,
        BillingPeriod period) =>
        new(
            SnapshotId(serviceId, code, capturedAt),
            serviceId,
            new UsageMetric(code, name, kind, unit),
            value,
            capturedAt,
            period,
            DataSourceKind.Mock,
            DataAccuracy.Estimated);

    private static string SnapshotId(string serviceId, string metric, DateTimeOffset capturedAt) =>
        $"{serviceId}:{metric}:{capturedAt.ToUnixTimeMilliseconds()}";

    private static BillingPeriod CurrentMonth(DateTimeOffset now)
    {
        var start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        return new BillingPeriod(start, start.AddMonths(1), "UTC");
    }

    private static IReadOnlyList<Subscription> BuildSubscriptions(DateTimeOffset now) =>
    [
        new(
            "mock:subscription:claude",
            "Claude",
            new Money(20m, "USD"),
            BillingCadence.Monthly,
            now.AddDays(2),
            DataSourceKind.Mock),
        new(
            "mock:subscription:chatgpt",
            "ChatGPT",
            new Money(20m, "USD"),
            BillingCadence.Monthly,
            now.AddDays(5),
            DataSourceKind.Mock)
    ];

    private static IReadOnlyList<Payment> BuildPayments(DateTimeOffset now) =>
    [
        new(
            "mock:payment:claude",
            "Claude",
            new Money(20m, "USD"),
            now.AddDays(2),
            PaymentStatus.Scheduled,
            "mock:subscription:claude"),
        new(
            "mock:payment:chatgpt",
            "ChatGPT",
            new Money(20m, "USD"),
            now.AddDays(5),
            PaymentStatus.Scheduled,
            "mock:subscription:chatgpt"),
        new(
            "mock:payment:vps",
            "VPS",
            new Money(12m, "USD"),
            now.AddDays(9),
            PaymentStatus.Scheduled)
    ];
}
