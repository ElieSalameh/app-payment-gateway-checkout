namespace PaymentGateway.Domain.Tests.Payments;

public sealed class CardDetailsTests
{
    private const string _FourteenDigitCard = "22224053432488";
    private const string _NineteenDigitCard = "2222405343248877123";

    [Theory]
    [InlineData("2222405343248877", "8877")]
    [InlineData(_FourteenDigitCard, "2488")]
    [InlineData(_NineteenDigitCard, "7123")]
    public void FromCardNumber_WhenCardNumberIsValid_KeepsOnlyTheLastFourDigits(string cardNumber, string expectedLastFour)
    {
        var card = CardDetails.FromCardNumber(cardNumber, expiryMonth: 4, expiryYear: 2030);

        card.LastFourDigits.Should().Be(expectedLastFour);
    }

    [Theory]
    [InlineData("2222405343248877")]
    [InlineData(_FourteenDigitCard)]
    [InlineData(_NineteenDigitCard)]
    public void FromCardNumber_WhenCardNumberIsValid_NeverRetainsTheFullCardNumber(string cardNumber)
    {
        var card = CardDetails.FromCardNumber(cardNumber, expiryMonth: 4, expiryYear: 2030);

        card.LastFourDigits.Should().NotBe(cardNumber);
        card.ToString().Should().NotContain(cardNumber);
        card.ToString().Should().Be($"**** **** **** {cardNumber[^4..]}");
    }

    [Theory]
    [InlineData("2222405343248")]
    [InlineData("22224053432488771234")]
    [InlineData("")]
    public void FromCardNumber_WhenCardNumberLengthIsOutsideFourteenToNineteen_Throws(string cardNumber)
    {
        var create = () => CardDetails.FromCardNumber(cardNumber, expiryMonth: 4, expiryYear: 2030);

        create.Should().Throw<ArgumentException>().WithMessage("A card number must be between 14 and 19 digits.*");
    }

    [Theory]
    [InlineData("2222405343248a77")]
    [InlineData("2222 4053 4324 8877")]
    [InlineData("-222405343248877")]
    public void FromCardNumber_WhenCardNumberContainsANonDigit_Throws(string cardNumber)
    {
        var create = () => CardDetails.FromCardNumber(cardNumber, expiryMonth: 4, expiryYear: 2030);

        create.Should().Throw<ArgumentException>().WithMessage("A card number must contain digits only.*");
    }

    [Fact]
    public void FromCardNumber_WhenCardNumberIsRejected_DoesNotLeakItInTheExceptionMessage()
    {
        const string _cardNumber = "2222405343248a77";

        var create = () => CardDetails.FromCardNumber(_cardNumber, expiryMonth: 4, expiryYear: 2030);

        create.Should().Throw<ArgumentException>().Which.Message.Should().NotContain(_cardNumber);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    public void FromCardNumber_WhenExpiryMonthIsWithinTheYear_IsAccepted(int expiryMonth)
    {
        var card = CardDetails.FromCardNumber("2222405343248877", expiryMonth, expiryYear: 2030);

        card.ExpiryMonth.Should().Be(expiryMonth);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void FromCardNumber_WhenExpiryMonthIsOutsideOneToTwelve_Throws(int expiryMonth)
    {
        var create = () => CardDetails.FromCardNumber("2222405343248877", expiryMonth, expiryYear: 2030);

        create.Should().Throw<ArgumentOutOfRangeException>().WithMessage("An expiry month must be between 1 and 12.*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    [InlineData(10000)]
    public void FromCardNumber_WhenExpiryYearIsNotFourDigits_Throws(int expiryYear)
    {
        var create = () => CardDetails.FromCardNumber("2222405343248877", expiryMonth: 4, expiryYear: expiryYear);

        create.Should().Throw<ArgumentOutOfRangeException>().WithMessage("An expiry year must be between 1000 and 9999.*");
    }

    [Fact]
    public void HasExpired_OnTheLastDayOfTheExpiryMonth_ReturnsFalse()
    {
        var card = CardDetails.FromCardNumber("2222405343248877", expiryMonth: 4, expiryYear: 2030);

        card.HasExpired(new DateTimeOffset(2030, 4, 30, 23, 59, 59, TimeSpan.Zero)).Should().BeFalse();
    }

    [Fact]
    public void HasExpired_OnTheFirstDayAfterTheExpiryMonth_ReturnsTrue()
    {
        var card = CardDetails.FromCardNumber("2222405343248877", expiryMonth: 4, expiryYear: 2030);

        card.HasExpired(new DateTimeOffset(2030, 5, 1, 0, 0, 0, TimeSpan.Zero)).Should().BeTrue();
    }

    [Fact]
    public void HasExpired_WhenTheExpiryMonthIsDecember_RollsIntoTheFollowingYear()
    {
        var card = CardDetails.FromCardNumber("2222405343248877", expiryMonth: 12, expiryYear: 2030);

        card.HasExpired(new DateTimeOffset(2030, 12, 31, 23, 59, 59, TimeSpan.Zero)).Should().BeFalse();
        card.HasExpired(new DateTimeOffset(2031, 1, 1, 0, 0, 0, TimeSpan.Zero)).Should().BeTrue();
    }

    [Fact]
    public void IsExpired_DuringTheExpiryMonth_ReturnsFalse()
    {
        CardDetails.IsExpired(expiryMonth: 6, expiryYear: 2025, new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero)).Should().BeFalse();
        CardDetails.IsExpired(expiryMonth: 6, expiryYear: 2025, new DateTimeOffset(2025, 6, 30, 23, 59, 59, TimeSpan.Zero)).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_OnceTheExpiryMonthHasPassed_ReturnsTrue()
    {
        CardDetails.IsExpired(expiryMonth: 6, expiryYear: 2025, new DateTimeOffset(2025, 7, 1, 0, 0, 0, TimeSpan.Zero)).Should().BeTrue();
    }

    [Fact]
    public void IsExpired_AtTheLatestSupportedExpiry_DoesNotOverflow()
    {
        CardDetails.IsExpired(expiryMonth: 12, expiryYear: 9999, new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero)).Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 2030)]
    [InlineData(13, 2030)]
    [InlineData(6, 999)]
    [InlineData(6, 10000)]
    public void IsExpired_WhenTheExpiryIsOutOfRange_ThrowsArgumentOutOfRangeException(int expiryMonth, int expiryYear)
    {
        var isExpired = () => CardDetails.IsExpired(expiryMonth, expiryYear, new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero));

        isExpired.Should().Throw<ArgumentOutOfRangeException>();
    }
}
