namespace DevStatusCenter.Domain.Models;

public sealed record RefreshPolicy
{
    public RefreshPolicy(
        TimeSpan minimumInterval,
        TimeSpan normalInterval,
        TimeSpan ecoInterval,
        bool supportsManualRefresh = true)
    {
        if (minimumInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumInterval));
        }

        if (normalInterval < minimumInterval)
        {
            throw new ArgumentOutOfRangeException(nameof(normalInterval));
        }

        if (ecoInterval < normalInterval)
        {
            throw new ArgumentOutOfRangeException(nameof(ecoInterval));
        }

        MinimumInterval = minimumInterval;
        NormalInterval = normalInterval;
        EcoInterval = ecoInterval;
        SupportsManualRefresh = supportsManualRefresh;
    }

    public TimeSpan MinimumInterval { get; }

    public TimeSpan NormalInterval { get; }

    public TimeSpan EcoInterval { get; }

    public bool SupportsManualRefresh { get; }
}

