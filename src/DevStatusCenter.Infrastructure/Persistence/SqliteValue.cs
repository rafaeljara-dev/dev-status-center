using System.Globalization;

namespace DevStatusCenter.Infrastructure.Persistence;

internal static class SqliteValue
{
    public static string Decimal(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    public static decimal ReadDecimal(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    public static long Instant(DateTimeOffset value) => value.ToUniversalTime().ToUnixTimeMilliseconds();

    public static object NullableInstant(DateTimeOffset? value) =>
        value is null ? DBNull.Value : Instant(value.Value);

    public static DateTimeOffset ReadInstant(long value) => DateTimeOffset.FromUnixTimeMilliseconds(value);

    public static DateTimeOffset? ReadNullableInstant(object value) =>
        value is DBNull ? null : ReadInstant(Convert.ToInt64(value, CultureInfo.InvariantCulture));
}

