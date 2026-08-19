using DevStatusCenter.Domain.Common;
using DevStatusCenter.Domain.Enums;

namespace DevStatusCenter.Domain.Models;

public sealed record ProviderState
{
    public ProviderState(
        string providerId,
        ProviderStatus status,
        DateTimeOffset? lastAttemptAt,
        DateTimeOffset? lastSuccessAt,
        DateTimeOffset? nextRefreshAt,
        int consecutiveFailures,
        string? errorCode,
        string? errorMessage)
    {
        ProviderId = Guard.NotBlank(providerId, nameof(providerId));
        Status = status;
        LastAttemptAt = lastAttemptAt?.ToUniversalTime();
        LastSuccessAt = lastSuccessAt?.ToUniversalTime();
        NextRefreshAt = nextRefreshAt?.ToUniversalTime();
        ConsecutiveFailures = Math.Max(0, consecutiveFailures);
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public string ProviderId { get; }

    public ProviderStatus Status { get; }

    public DateTimeOffset? LastAttemptAt { get; }

    public DateTimeOffset? LastSuccessAt { get; }

    public DateTimeOffset? NextRefreshAt { get; }

    public int ConsecutiveFailures { get; }

    public string? ErrorCode { get; }

    public string? ErrorMessage { get; }
}
