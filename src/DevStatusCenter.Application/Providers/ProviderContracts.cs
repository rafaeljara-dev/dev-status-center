using DevStatusCenter.Domain.Models;

namespace DevStatusCenter.Application.Providers;

[Flags]
public enum ProviderCapabilities
{
    None = 0,
    Usage = 1 << 0,
    Billing = 1 << 1,
    Quota = 1 << 2,
    Subscriptions = 1 << 3
}

public sealed record ProviderDescriptor(
    string Id,
    string DisplayName,
    ProviderCapabilities Capabilities,
    RefreshPolicy RefreshPolicy);

public sealed record ProviderRefreshContext(
    DateTimeOffset RequestedAt,
    bool IsManual,
    string DisplayCurrency);

public sealed record ServiceObservation(
    Service Service,
    IReadOnlyList<UsageSnapshot> Usage,
    IReadOnlyList<BillingRecord> Billing);

public sealed record ProviderRefreshResult(
    string ProviderId,
    DateTimeOffset CompletedAt,
    IReadOnlyList<ProviderAccount> Accounts,
    IReadOnlyList<ServiceObservation> Observations,
    IReadOnlyList<Subscription> Subscriptions,
    IReadOnlyList<Payment> Payments)
{
    public static ProviderRefreshResult Empty(string providerId, DateTimeOffset completedAt) =>
        new(providerId, completedAt, [], [], [], []);
}

public interface IProvider
{
    ProviderDescriptor Descriptor { get; }

    Task<ProviderRefreshResult> RefreshAsync(
        ProviderRefreshContext context,
        CancellationToken cancellationToken);
}

public interface IUsageProvider
{
    Task<IReadOnlyList<ServiceObservation>> GetUsageAsync(
        ProviderRefreshContext context,
        CancellationToken cancellationToken);
}

public interface IBillingProvider
{
    Task<IReadOnlyList<ServiceObservation>> GetBillingAsync(
        ProviderRefreshContext context,
        CancellationToken cancellationToken);
}

public interface IQuotaProvider
{
    Task<IReadOnlyList<UsageSnapshot>> GetQuotasAsync(
        ProviderRefreshContext context,
        CancellationToken cancellationToken);
}

public interface ISubscriptionProvider
{
    Task<IReadOnlyList<Subscription>> GetSubscriptionsAsync(
        ProviderRefreshContext context,
        CancellationToken cancellationToken);
}

public enum ProviderFailureKind
{
    Authentication,
    RateLimited,
    Timeout,
    InvalidResponse,
    Transient,
    Permanent
}

public sealed class ProviderRefreshException : Exception
{
    public ProviderRefreshException(
        ProviderFailureKind kind,
        string providerId,
        string errorCode,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
        ProviderId = providerId;
        ErrorCode = errorCode;
    }

    public ProviderFailureKind Kind { get; }

    public string ProviderId { get; }

    public string ErrorCode { get; }
}

