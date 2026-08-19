using DevStatusCenter.Domain.Common;
using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Domain.ValueObjects;

namespace DevStatusCenter.Domain.Models;

public sealed record Subscription
{
    public Subscription(
        string id,
        string name,
        Money price,
        BillingCadence cadence,
        DateTimeOffset nextRenewalAt,
        DataSourceKind source,
        string? serviceId = null,
        bool isActive = true)
    {
        Id = Guard.NotBlank(id, nameof(id));
        Name = Guard.NotBlank(name, nameof(name));
        Price = price;
        Cadence = cadence;
        NextRenewalAt = Guard.Utc(nextRenewalAt);
        Source = source;
        ServiceId = serviceId?.Trim();
        IsActive = isActive;
    }

    public string Id { get; }

    public string Name { get; }

    public Money Price { get; }

    public BillingCadence Cadence { get; }

    public DateTimeOffset NextRenewalAt { get; }

    public DataSourceKind Source { get; }

    public string? ServiceId { get; }

    public bool IsActive { get; }
}

