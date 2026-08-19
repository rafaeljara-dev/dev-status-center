using DevStatusCenter.Domain.Common;
using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Domain.ValueObjects;

namespace DevStatusCenter.Domain.Models;

public sealed record Payment
{
    public Payment(
        string id,
        string name,
        Money amount,
        DateTimeOffset dueAt,
        PaymentStatus status,
        string? subscriptionId = null)
    {
        Id = Guard.NotBlank(id, nameof(id));
        Name = Guard.NotBlank(name, nameof(name));
        Amount = amount;
        DueAt = Guard.Utc(dueAt);
        Status = status;
        SubscriptionId = subscriptionId?.Trim();
    }

    public string Id { get; }

    public string Name { get; }

    public Money Amount { get; }

    public DateTimeOffset DueAt { get; }

    public PaymentStatus Status { get; }

    public string? SubscriptionId { get; }
}

