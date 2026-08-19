namespace DevStatusCenter.Domain.Enums;

public enum ServiceCategory
{
    Ai,
    Infrastructure,
    Development,
    Subscription,
    Domain,
    Other
}

public enum CostBehavior
{
    Variable,
    Fixed,
    Mixed
}

public enum BillingCadence
{
    OneTime,
    Weekly,
    Monthly,
    Quarterly,
    Yearly,
    Custom
}

public enum DataSourceKind
{
    OfficialBillingApi,
    OfficialUsageApi,
    Invoice,
    Manual,
    Mock
}

public enum DataAccuracy
{
    Exact,
    ProviderReported,
    Calculated,
    Estimated,
    Manual,
    Stale
}

public enum ProviderStatus
{
    Healthy,
    Refreshing,
    Stale,
    RateLimited,
    AuthenticationRequired,
    Error,
    Disabled
}

public enum PowerMode
{
    Normal,
    Eco,
    Paused,
    Gaming
}

public enum MetricKind
{
    TokensInput,
    TokensOutput,
    TokensCached,
    Compute,
    Storage,
    DataTransfer,
    Requests,
    QuotaConsumed,
    Custom
}

public enum PaymentStatus
{
    Scheduled,
    Paid,
    Skipped,
    Failed
}

