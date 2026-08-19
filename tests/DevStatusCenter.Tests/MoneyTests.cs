using DevStatusCenter.Domain.ValueObjects;

namespace DevStatusCenter.Tests;

public sealed class MoneyTests
{
    [Fact]
    public void Add_RejectsDifferentCurrencies()
    {
        var usd = new Money(10m, "USD");
        var mxn = new Money(10m, "MXN");

        Assert.Throws<InvalidOperationException>(() => usd.Add(mxn));
    }

    [Fact]
    public void Constructor_NormalizesCurrency()
    {
        var amount = new Money(1.25m, "usd");

        Assert.Equal("USD", amount.Currency);
        Assert.Equal(1.25m, amount.Amount);
    }
}

