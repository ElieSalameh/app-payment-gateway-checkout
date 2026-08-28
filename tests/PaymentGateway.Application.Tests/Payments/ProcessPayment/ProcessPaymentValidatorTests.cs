namespace PaymentGateway.Application.Tests.Payments.ProcessPayment;

public sealed class ProcessPaymentValidatorTests
{
    private const string _ValidCardNumber = "2222405343248877";
    private const string _ValidCvv = "123";

    private static readonly DateTimeOffset _Now = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Validate_WhenEveryFieldIsValid_ReturnsNoErrors()
    {
        var validator = ValidatorAt(_Now);

        var result = validator.TestValidate(ValidCommand());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenCardNumberIsMissing_ReturnsRequiredError(string? cardNumber)
    {
        var validator = ValidatorAt(_Now);

        var result = validator.TestValidate(ValidCommand() with { CardNumber = cardNumber });

        result.ShouldHaveValidationErrorFor(command => command.CardNumber)
            .WithErrorMessage("Card number is required.");
    }

    [Theory]
    [InlineData("1234567890123")]
    [InlineData("12345678901234567890")]
    public void Validate_WhenCardNumberLengthIsOutsideFourteenToNineteen_ReturnsLengthError(string cardNumber)
    {
        var validator = ValidatorAt(_Now);

        var result = validator.TestValidate(ValidCommand() with { CardNumber = cardNumber });

        result.ShouldHaveValidationErrorFor(command => command.CardNumber)
            .WithErrorMessage("Card number must be between 14 and 19 digits.");
    }

    [Theory]
    [InlineData("12345678901234")]
    [InlineData("1234567890123456789")]
    public void Validate_WhenCardNumberLengthIsAtABoundary_ReturnsNoCardNumberError(string cardNumber)
    {
        var validator = ValidatorAt(_Now);

        var result = validator.TestValidate(ValidCommand() with { CardNumber = cardNumber });

        result.ShouldNotHaveValidationErrorFor(command => command.CardNumber);
    }

    [Theory]
    [InlineData("2222405343248 77")]
    [InlineData("2222405343248a77")]
    [InlineData("2222-4053-4324-8877")]
    public void Validate_WhenCardNumberContainsANonDigit_ReturnsDigitsOnlyError(string cardNumber)
    {
        var validator = ValidatorAt(_Now);

        var result = validator.TestValidate(ValidCommand() with { CardNumber = cardNumber });

        result.ShouldHaveValidationErrorFor(command => command.CardNumber)
            .WithErrorMessage("Card number must contain digits only.");
    }

    [Fact]
    public void Validate_WhenExpiryMonthIsMissing_ReturnsRequiredError()
    {
        var validator = ValidatorAt(_Now);

        var result = validator.TestValidate(ValidCommand() with { ExpiryMonth = null });

        result.ShouldHaveValidationErrorFor(command => command.ExpiryMonth)
            .WithErrorMessage("Expiry month is required.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void Validate_WhenExpiryMonthIsOutsideOneToTwelve_ReturnsRangeError(int expiryMonth)
    {
        var validator = ValidatorAt(_Now);

        var result = validator.TestValidate(ValidCommand() with { ExpiryMonth = expiryMonth });

        result.ShouldHaveValidationErrorFor(command => command.ExpiryMonth)
            .WithErrorMessage("Expiry month must be between 1 and 12.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    public void Validate_WhenExpiryMonthIsAtABoundary_ReturnsNoExpiryMonthError(int expiryMonth)
    {
        var validator = ValidatorAt(_Now);

        var result = validator.TestValidate(ValidCommand() with { ExpiryMonth = expiryMonth, ExpiryYear = 2030 });

        result.ShouldNotHaveValidationErrorFor(command => command.ExpiryMonth);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Validate_WhenExpiryMonthIsOutOfRange_SkipsTheCombinedExpiryRule(int expiryMonth)
    {
        var validator = ValidatorAt(_Now);

        var result = validator.TestValidate(ValidCommand() with { ExpiryMonth = expiryMonth });

        result.ShouldNotHaveValidationErrorFor(command => command.ExpiryYear);
    }

    [Fact]
    public void Validate_WhenExpiryYearIsMissing_ReturnsRequiredError()
    {
        var validator = ValidatorAt(_Now);

        var result = validator.TestValidate(ValidCommand() with { ExpiryYear = null });

        result.ShouldHaveValidationErrorFor(command => command.ExpiryYear)
            .WithErrorMessage("Expiry year is required.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    [InlineData(-2030)]
    public void Validate_WhenExpiryYearIsNotFourDigits_ReturnsFourDigitYearError(int expiryYear)
    {
        var validator = ValidatorAt(_Now);

        var result = validator.TestValidate(ValidCommand() with { ExpiryYear = expiryYear });

        result.ShouldHaveValidationErrorFor(command => command.ExpiryYear)
            .WithErrorMessage("Expiry year must be a four digit year.");
    }

    [Theory]
    [InlineData(5, 2025)]
    [InlineData(12, 2024)]
    [InlineData(6, 2024)]
    public void Validate_WhenExpiryIsInThePast_ReturnsExpiryInTheFutureError(int expiryMonth, int expiryYear)
    {
        var validator = ValidatorAt(_Now);

        var result = validator.TestValidate(ValidCommand() with { ExpiryMonth = expiryMonth, ExpiryYear = expiryYear });

        result.ShouldHaveValidationErrorFor(command => command.ExpiryYear)
            .WithErrorMessage("Expiry date must be in the future.");
    }

    [Theory]
    [InlineData(6, 2025)]
    [InlineData(7, 2025)]
    [InlineData(1, 2026)]
    public void Validate_WhenExpiryIsThisMonthOrLater_ReturnsNoExpiryError(int expiryMonth, int expiryYear)
    {
        var validator = ValidatorAt(_Now);

        var result = validator.TestValidate(ValidCommand() with { ExpiryMonth = expiryMonth, ExpiryYear = expiryYear });

        result.ShouldNotHaveValidationErrorFor(command => command.ExpiryYear);
    }

    [Fact]
    public void Validate_AtTheLastInstantOfTheExpiryMonth_ReturnsNoExpiryError()
    {
        var validator = ValidatorAt(new DateTimeOffset(2025, 6, 30, 23, 59, 59, TimeSpan.Zero));

        var result = validator.TestValidate(ValidCommand() with { ExpiryMonth = 6, ExpiryYear = 2025 });

        result.ShouldNotHaveValidationErrorFor(command => command.ExpiryYear);
    }

    [Fact]
    public void Validate_AtTheFirstInstantAfterTheExpiryMonth_ReturnsExpiryInTheFutureError()
    {
        var validator = ValidatorAt(new DateTimeOffset(2025, 7, 1, 0, 0, 0, TimeSpan.Zero));

        var result = validator.TestValidate(ValidCommand() with { ExpiryMonth = 6, ExpiryYear = 2025 });

        result.ShouldHaveValidationErrorFor(command => command.ExpiryYear)
            .WithErrorMessage("Expiry date must be in the future.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenCurrencyIsMissing_ReturnsRequiredError(string? currency)
    {
        var validator = ValidatorAt(_Now);

        var result = validator.TestValidate(ValidCommand() with { Currency = currency });

        result.ShouldHaveValidationErrorFor(command => command.Currency)
            .WithErrorMessage("Currency is required.");
    }

    [Theory]
    [InlineData("GB")]
    [InlineData("GBPP")]
    public void Validate_WhenCurrencyIsNotThreeCharacters_ReturnsLengthError(string currency)
    {
        var validator = ValidatorAt(_Now);

        var result = validator.TestValidate(ValidCommand() with { Currency = currency });

        result.ShouldHaveValidationErrorFor(command => command.Currency)
            .WithErrorMessage("Currency must be exactly 3 characters.");
    }

    [Theory]
    [InlineData("JPY")]
    [InlineData("CHF")]
    [InlineData("AUD")]
    public void Validate_WhenCurrencyIsNotSupported_ReturnsSupportedCurrencyError(string currency)
    {
        var validator = ValidatorAt(_Now);

        var result = validator.TestValidate(ValidCommand() with { Currency = currency });

        result.ShouldHaveValidationErrorFor(command => command.Currency)
            .WithErrorMessage("Currency must be one of GBP, USD, EUR.");
    }

    [Theory]
    [InlineData("GBP")]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("gbp")]
    [InlineData("eUr")]
    public void Validate_WhenCurrencyIsSupported_ReturnsNoCurrencyError(string currency)
    {
        var validator = ValidatorAt(_Now);

        var result = validator.TestValidate(ValidCommand() with { Currency = currency });

        result.ShouldNotHaveValidationErrorFor(command => command.Currency);
    }

    [Fact]
    public void Validate_WhenAmountIsMissing_ReturnsRequiredError()
    {
        var validator = ValidatorAt(_Now);

        var result = validator.TestValidate(ValidCommand() with { Amount = null });

        result.ShouldHaveValidationErrorFor(command => command.Amount)
            .WithErrorMessage("Amount is required.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void Validate_WhenAmountIsNotGreaterThanZero_ReturnsGreaterThanZeroError(long amount)
    {
        var validator = ValidatorAt(_Now);

        var result = validator.TestValidate(ValidCommand() with { Amount = amount });

        result.ShouldHaveValidationErrorFor(command => command.Amount)
            .WithErrorMessage("Amount must be greater than zero.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(long.MaxValue)]
    public void Validate_WhenAmountIsGreaterThanZero_ReturnsNoAmountError(long amount)
    {
        var validator = ValidatorAt(_Now);

        var result = validator.TestValidate(ValidCommand() with { Amount = amount });

        result.ShouldNotHaveValidationErrorFor(command => command.Amount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenCvvIsMissing_ReturnsRequiredError(string? cvv)
    {
        var validator = ValidatorAt(_Now);

        var result = validator.TestValidate(ValidCommand() with { Cvv = cvv });

        result.ShouldHaveValidationErrorFor(command => command.Cvv)
            .WithErrorMessage("CVV is required.");
    }

    [Theory]
    [InlineData("12")]
    [InlineData("12345")]
    public void Validate_WhenCvvLengthIsOutsideThreeToFour_ReturnsLengthError(string cvv)
    {
        var validator = ValidatorAt(_Now);

        var result = validator.TestValidate(ValidCommand() with { Cvv = cvv });

        result.ShouldHaveValidationErrorFor(command => command.Cvv)
            .WithErrorMessage("CVV must be 3 or 4 digits.");
    }

    [Theory]
    [InlineData("123")]
    [InlineData("1234")]
    public void Validate_WhenCvvLengthIsAtABoundary_ReturnsNoCvvError(string cvv)
    {
        var validator = ValidatorAt(_Now);

        var result = validator.TestValidate(ValidCommand() with { Cvv = cvv });

        result.ShouldNotHaveValidationErrorFor(command => command.Cvv);
    }

    [Theory]
    [InlineData("12a")]
    [InlineData("1 3")]
    [InlineData("12$4")]
    public void Validate_WhenCvvContainsANonDigit_ReturnsDigitsOnlyError(string cvv)
    {
        var validator = ValidatorAt(_Now);

        var result = validator.TestValidate(ValidCommand() with { Cvv = cvv });

        result.ShouldHaveValidationErrorFor(command => command.Cvv)
            .WithErrorMessage("CVV must contain digits only.");
    }

    [Fact]
    public void Validate_WhenEveryFieldIsMissing_ReturnsOneErrorPerField()
    {
        var validator = ValidatorAt(_Now);

        var result = validator.Validate(new ProcessPaymentCommand());

        result.Errors.Select(failure => failure.PropertyName).Should().BeEquivalentTo(
            nameof(ProcessPaymentCommand.CardNumber),
            nameof(ProcessPaymentCommand.ExpiryMonth),
            nameof(ProcessPaymentCommand.ExpiryYear),
            nameof(ProcessPaymentCommand.Currency),
            nameof(ProcessPaymentCommand.Amount),
            nameof(ProcessPaymentCommand.Cvv));
    }

    [Fact]
    public void Validate_WhenTheRequestIsRejected_NeverEchoesTheCardNumberOrCvv()
    {
        var validator = ValidatorAt(_Now);
        var command = ValidCommand() with { Currency = "JPY" };

        var result = validator.Validate(command);

        var messages = string.Join(" ", result.Errors.Select(failure => failure.ErrorMessage));
        messages.Should().NotContain(_ValidCardNumber);
        messages.Should().NotContain(_ValidCvv);
    }

    [Fact]
    public void Constructor_WhenTimeProviderIsNull_ThrowsArgumentNullException()
    {
        var construct = () => new ProcessPaymentValidator(null!);

        construct.Should().Throw<ArgumentNullException>();
    }

    private static ProcessPaymentValidator ValidatorAt(DateTimeOffset asOf) =>
        new(new FakeTimeProvider(asOf));

    private static ProcessPaymentCommand ValidCommand() => new()
    {
        CardNumber = _ValidCardNumber,
        ExpiryMonth = 12,
        ExpiryYear = 2030,
        Currency = "GBP",
        Amount = 100,
        Cvv = _ValidCvv
    };
}
