using DevStatusCenter.Domain.Common;
using DevStatusCenter.Domain.Enums;

namespace DevStatusCenter.Domain.Models;

public sealed record UsageMetric
{
    public UsageMetric(string code, string displayName, MetricKind kind, string unit)
    {
        Code = Guard.NotBlank(code, nameof(code));
        DisplayName = Guard.NotBlank(displayName, nameof(displayName));
        Kind = kind;
        Unit = Guard.NotBlank(unit, nameof(unit));
    }

    public string Code { get; }

    public string DisplayName { get; }

    public MetricKind Kind { get; }

    public string Unit { get; }
}

