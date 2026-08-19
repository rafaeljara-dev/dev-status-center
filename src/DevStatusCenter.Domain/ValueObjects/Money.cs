using DevStatusCenter.Domain.Common;

namespace DevStatusCenter.Domain.ValueObjects;

public readonly record struct Money
{
    public Money(decimal amount, string currency)
    {
        Amount = Guard.NonNegative(amount, nameof(amount));
        Currency = Guard.NotBlank(currency, nameof(currency)).ToUpperInvariant();
        if (Currency.Length != 3)
        {
            throw new ArgumentException("Currency must be an ISO 4217 three-letter code.", nameof(currency));
        }
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public static Money Zero(string currency) => new(0m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Cannot combine {Currency} and {other.Currency} without an exchange rate.");
        }
    }
}

