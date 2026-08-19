namespace DevStatusCenter.Domain.Models;

public sealed record RefreshPolicy
{
    public RefreshPolicy(
        TimeSpan minimumInterval,
        TimeSpan normalInterval,
        TimeSpan ecoInterval,
        bool supportsManualRefresh = true)
    {
        // Throw helpers en vez de if + throw: mantienen el camino feliz sin el bloque de
        // excepción, que es lo que permite al JIT inlinear el constructor (CA1512).
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(minimumInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(normalInterval, minimumInterval);
        ArgumentOutOfRangeException.ThrowIfLessThan(ecoInterval, normalInterval);

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

