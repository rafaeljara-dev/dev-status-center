using DevStatusCenter.Domain.Common;
using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Domain.ValueObjects;

namespace DevStatusCenter.Domain.Models;

public sealed record BillingRecord
{
    public BillingRecord(
        string id,
        string serviceId,
        Money amount,
        BillingPeriod period,
        DateTimeOffset capturedAt,
        DataSourceKind source,
        DataAccuracy accuracy,
        string? externalInvoiceId = null)
    {
        Id = Guard.NotBlank(id, nameof(id));
        ServiceId = Guard.NotBlank(serviceId, nameof(serviceId));
        Amount = amount;
        Period = period ?? throw new ArgumentNullException(nameof(period));
        CapturedAt = Guard.Utc(capturedAt);
        Source = source;
        Accuracy = accuracy;
        ExternalInvoiceId = externalInvoiceId?.Trim();
    }

    public string Id { get; }

    public string ServiceId { get; }

    public Money Amount { get; }

    public BillingPeriod Period { get; }

    public DateTimeOffset CapturedAt { get; }

    public DataSourceKind Source { get; }

    public DataAccuracy Accuracy { get; }

    public string? ExternalInvoiceId { get; }
}

