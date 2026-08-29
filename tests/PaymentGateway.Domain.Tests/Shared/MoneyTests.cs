namespace PaymentGateway.Domain.Tests.Shared;

public sealed class MoneyTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1050)]
    [InlineData(long.MaxValue)]
    public void FromMinorUnits_WhenAmountIsGreaterThanZero_KeepsTheAmountAndCurrency(long amountInMinorUnits)
    {
        var money = Money.FromMinorUnits(amountInMinorUnits, Currency.Gbp);

        money.AmountInMinorUnits.Should().Be(amountInMinorUnits);
        money.Currency.Should().Be(Currency.Gbp);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void FromMinorUnits_WhenAmountIsNotGreaterThanZero_Throws(long amountInMinorUnits)
    {
        var create = () => Money.FromMinorUnits(amountInMinorUnits, Currency.Gbp);

        create.Should().Throw<ArgumentOutOfRangeException>().WithMessage("An amount must be greater than zero.*");
    }

    [Fact]
    public void Equality_WhenAmountAndCurrencyMatch_TreatsThemAsEqual()
    {
        Money.FromMinorUnits(1050, Currency.Usd).Should().Be(Money.FromMinorUnits(1050, Currency.Usd));
        Money.FromMinorUnits(1050, Currency.Usd).Should().NotBe(Money.FromMinorUnits(1050, Currency.Eur));
        Money.FromMinorUnits(1050, Currency.Usd).Should().NotBe(Money.FromMinorUnits(1051, Currency.Usd));
    }

    [Fact]
    public void ToString_WhenRendered_ShowsMinorUnitsAndCurrency()
    {
        Money.FromMinorUnits(1050, Currency.Usd).ToString().Should().Be("1050 USD");
    }
}
