namespace PaymentGateway.Application.Payments.ProcessPayment;

public sealed class ProcessPaymentValidator : AbstractValidator<ProcessPaymentCommand>
{
    private const int _MinimumCardNumberLength = 14;
    private const int _MaximumCardNumberLength = 19;
    private const int _CurrencyCodeLength = 3;
    private const int _FirstMonthOfYear = 1;
    private const int _LastMonthOfYear = 12;
    private const int _EarliestExpiryYear = 1000;
    private const int _LatestExpiryYear = 9999;
    private const int _MinimumCvvLength = 3;
    private const int _MaximumCvvLength = 4;
    private const long _SmallestAmountInMinorUnits = 1;

    private static readonly string _SupportedCurrencyCodes =
        string.Join(", ", Currency.Supported.Select(currency => currency.Code));

    private readonly TimeProvider _timeProvider;

    public ProcessPaymentValidator(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;

        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.CardNumber)
            .NotEmpty()
            .WithMessage("Card number is required.")
            .Length(_MinimumCardNumberLength, _MaximumCardNumberLength)
            .WithMessage($"Card number must be between {_MinimumCardNumberLength} and {_MaximumCardNumberLength} digits.")
            .Must(ContainsDigitsOnly)
            .WithMessage("Card number must contain digits only.");

        RuleFor(command => command.ExpiryMonth)
            .NotNull()
            .WithMessage("Expiry month is required.")
            .InclusiveBetween(_FirstMonthOfYear, _LastMonthOfYear)
            .WithMessage($"Expiry month must be between {_FirstMonthOfYear} and {_LastMonthOfYear}.");

        RuleFor(command => command.ExpiryYear)
            .NotNull()
            .WithMessage("Expiry year is required.")
            .InclusiveBetween(_EarliestExpiryYear, _LatestExpiryYear)
            .WithMessage("Expiry year must be a four digit year.")
            .Must((command, _) => IsExpiryInTheFuture(command))
            .WithMessage("Expiry date must be in the future.")
            .When(HasExpiryMonthWithinYear, ApplyConditionTo.CurrentValidator);

        RuleFor(command => command.Currency)
            .NotEmpty()
            .WithMessage("Currency is required.")
            .Length(_CurrencyCodeLength)
            .WithMessage($"Currency must be exactly {_CurrencyCodeLength} characters.")
            .Must(Currency.IsSupported)
            .WithMessage($"Currency must be one of {_SupportedCurrencyCodes}.");

        RuleFor(command => command.Amount)
            .NotNull()
            .WithMessage("Amount is required.")
            .GreaterThanOrEqualTo(_SmallestAmountInMinorUnits)
            .WithMessage("Amount must be greater than zero.");

        RuleFor(command => command.Cvv)
            .NotEmpty()
            .WithMessage("CVV is required.")
            .Length(_MinimumCvvLength, _MaximumCvvLength)
            .WithMessage($"CVV must be {_MinimumCvvLength} or {_MaximumCvvLength} digits.")
            .Must(ContainsDigitsOnly)
            .WithMessage("CVV must contain digits only.");
    }

    private static bool ContainsDigitsOnly(string? value) =>
        value is not null && value.All(char.IsAsciiDigit);

    private static bool HasExpiryMonthWithinYear(ProcessPaymentCommand command) =>
        command.ExpiryMonth is >= _FirstMonthOfYear and <= _LastMonthOfYear;

    private bool IsExpiryInTheFuture(ProcessPaymentCommand command) =>
        !CardDetails.IsExpired(command.ExpiryMonth!.Value, command.ExpiryYear!.Value, _timeProvider.GetUtcNow());
}
