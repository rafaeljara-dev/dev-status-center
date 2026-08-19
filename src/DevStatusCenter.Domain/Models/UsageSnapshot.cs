using DevStatusCenter.Domain.Common;
using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Domain.ValueObjects;

namespace DevStatusCenter.Domain.Models;

public sealed record UsageSnapshot
{
    public UsageSnapshot(
        string id,
        string serviceId,
        UsageMetric metric,
        decimal value,
        DateTimeOffset capturedAt,
        BillingPeriod period,
        DataSourceKind source,
        DataAccuracy accuracy)
    {
        Id = Guard.NotBlank(id, nameof(id));
        ServiceId = Guard.NotBlank(serviceId, nameof(serviceId));
        Metric = metric ?? throw new ArgumentNullException(nameof(metric));
        Value = Guard.NonNegative(value, nameof(value));
        CapturedAt = Guard.Utc(capturedAt);
        Period = period ?? throw new ArgumentNullException(nameof(period));
        Source = source;
        Accuracy = accuracy;
    }

    public string Id { get; }

    public string ServiceId { get; }

    public UsageMetric Metric { get; }

    public decimal Value { get; }

    public DateTimeOffset CapturedAt { get; }

    public BillingPeriod Period { get; }

    public DataSourceKind Source { get; }

    public DataAccuracy Accuracy { get; }
}

