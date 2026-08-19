namespace DevStatusCenter.Domain.Common;

public static class Guard
{
    public static string NotBlank(string? value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    public static decimal NonNegative(decimal value, string parameterName)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value, parameterName);
        return value;
    }

    public static DateTimeOffset Utc(DateTimeOffset value) => value.ToUniversalTime();
}

