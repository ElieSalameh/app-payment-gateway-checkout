namespace PaymentGateway.Domain.Tests.Shared;

public sealed class CurrencyTests
{
    [Theory]
    [InlineData("GBP")]
    [InlineData("USD")]
    [InlineData("EUR")]
    public void TryParse_WhenCodeIsSupported_ReturnsTheCurrency(string code)
    {
        var parsed = Currency.TryParse(code, out var currency);

        parsed.Should().BeTrue();
        currency!.Code.Should().Be(code);
    }

    [Theory]
    [InlineData("gbp")]
    [InlineData("Usd")]
    [InlineData("eUr")]
    public void TryParse_WhenCodeIsSupportedInAnyCasing_ReturnsTheCanonicalUppercaseCode(string code)
    {
        var parsed = Currency.TryParse(code, out var currency);

        parsed.Should().BeTrue();
        currency!.Code.Should().Be(code.ToUpperInvariant());
    }

    [Theory]
    [InlineData("JPY")]
    [InlineData("CHF")]
    [InlineData("XYZ")]
    public void TryParse_WhenCodeIsAnUnsupportedCurrency_ReturnsFalse(string code)
    {
        var parsed = Currency.TryParse(code, out var currency);

        parsed.Should().BeFalse();
        currency.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("GB")]
    [InlineData("GBPX")]
    public void TryParse_WhenCodeIsNotThreeCharacters_ReturnsFalse(string? code)
    {
        var parsed = Currency.TryParse(code, out _);

        parsed.Should().BeFalse();
    }

    [Fact]
    public void Parse_WhenCodeIsUnsupported_ThrowsWithoutNamingTheRejectedValue()
    {
        var parse = () => Currency.Parse("JPY");

        parse.Should().Throw<ArgumentException>().WithMessage("Currency must be one of GBP, USD, EUR.*");
    }

    [Fact]
    public void Supported_WhenRead_ContainsExactlyThreeCurrencies()
    {
        Currency.Supported.Should().HaveCount(3);
        Currency.Supported.Select(currency => currency.Code).Should().BeEquivalentTo("GBP", "USD", "EUR");
    }

    [Fact]
    public void Equality_WhenTwoCurrenciesShareACode_TreatsThemAsEqual()
    {
        Currency.Parse("GBP").Should().Be(Currency.Gbp);
        Currency.Gbp.Should().NotBe(Currency.Usd);
    }
}
