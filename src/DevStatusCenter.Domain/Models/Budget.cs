using DevStatusCenter.Domain.Common;
using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Domain.ValueObjects;

namespace DevStatusCenter.Domain.Models;

public sealed record Budget
{
    public Budget(
        string id,
        string name,
        Money limit,
        int warningPercent = 70,
        int importantPercent = 85,
        int criticalPercent = 95,
        string? serviceId = null,
        ServiceCategory? category = null)
    {
        Id = Guard.NotBlank(id, nameof(id));
        Name = Guard.NotBlank(name, nameof(name));
        Limit = limit;
        WarningPercent = ValidatePercent(warningPercent, nameof(warningPercent));
        ImportantPercent = ValidatePercent(importantPercent, nameof(importantPercent));
        CriticalPercent = ValidatePercent(criticalPercent, nameof(criticalPercent));
        ServiceId = serviceId?.Trim();
        Category = category;

        if (!(WarningPercent < ImportantPercent && ImportantPercent < CriticalPercent))
        {
            throw new ArgumentException("Budget thresholds must be strictly increasing.");
        }
    }

    public string Id { get; }

    public string Name { get; }

    public Money Limit { get; }

    public int WarningPercent { get; }

    public int ImportantPercent { get; }

    public int CriticalPercent { get; }

    public string? ServiceId { get; }

    public ServiceCategory? Category { get; }

    private static int ValidatePercent(int value, string parameterName)
    {
        return value is >= 1 and <= 100
            ? value
            : throw new ArgumentOutOfRangeException(parameterName, value, "Threshold must be between 1 and 100.");
    }
}

