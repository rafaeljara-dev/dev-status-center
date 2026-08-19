using DevStatusCenter.Domain.Common;

namespace DevStatusCenter.Domain.ValueObjects;

public sealed record BillingPeriod
{
    public BillingPeriod(DateTimeOffset startsAt, DateTimeOffset endsAt, string timeZoneId)
    {
        StartsAt = Guard.Utc(startsAt);
        EndsAt = Guard.Utc(endsAt);
        TimeZoneId = Guard.NotBlank(timeZoneId, nameof(timeZoneId));

        if (EndsAt <= StartsAt)
        {
            throw new ArgumentException("Billing period end must be later than its start.", nameof(endsAt));
        }
    }

    public DateTimeOffset StartsAt { get; }

    public DateTimeOffset EndsAt { get; }

    public string TimeZoneId { get; }
}

