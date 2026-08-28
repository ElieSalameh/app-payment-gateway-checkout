namespace PaymentGateway.Domain.Payments;

public sealed record CardDetails
{
    private const int _LastFourDigitsLength = 4;
    private const int _MinimumCardNumberLength = 14;
    private const int _MaximumCardNumberLength = 19;
    private const int _FirstMonthOfYear = 1;
    private const int _LastMonthOfYear = 12;
    private const int _EarliestExpiryYear = 1000;
    private const int _LatestExpiryYear = 9999;

    private CardDetails(string lastFourDigits, int expiryMonth, int expiryYear)
    {
        LastFourDigits = lastFourDigits;
        ExpiryMonth = expiryMonth;
        ExpiryYear = expiryYear;
    }

    public string LastFourDigits { get; }

    public int ExpiryMonth { get; }

    public int ExpiryYear { get; }

    public static CardDetails FromCardNumber(string cardNumber, int expiryMonth, int expiryYear)
    {
        ArgumentNullException.ThrowIfNull(cardNumber);
        GuardCardNumber(cardNumber);
        GuardExpiry(expiryMonth, expiryYear);

        return new CardDetails(MaskCardNumber(cardNumber), expiryMonth, expiryYear);
    }

    public static bool IsExpired(int expiryMonth, int expiryYear, DateTimeOffset asOf)
    {
        GuardExpiry(expiryMonth, expiryYear);

        var firstDayOfExpiryMonth = new DateTimeOffset(expiryYear, expiryMonth, 1, 0, 0, 0, TimeSpan.Zero);
        var firstDayOfCurrentMonth = new DateTimeOffset(asOf.UtcDateTime.Year, asOf.UtcDateTime.Month, 1, 0, 0, 0, TimeSpan.Zero);

        return firstDayOfCurrentMonth > firstDayOfExpiryMonth;
    }

    public bool HasExpired(DateTimeOffset asOf) => IsExpired(ExpiryMonth, ExpiryYear, asOf);

    public override string ToString() => $"**** **** **** {LastFourDigits}";

    private static string MaskCardNumber(string cardNumber) =>
        cardNumber[^_LastFourDigitsLength..];

    private static void GuardCardNumber(string cardNumber)
    {
        if (cardNumber.Length is < _MinimumCardNumberLength or > _MaximumCardNumberLength)
        {
            throw new ArgumentException(
                $"A card number must be between {_MinimumCardNumberLength} and {_MaximumCardNumberLength} digits.",
                nameof(cardNumber));
        }

        foreach (var character in cardNumber)
        {
            if (!char.IsAsciiDigit(character))
            {
                throw new ArgumentException("A card number must contain digits only.", nameof(cardNumber));
            }
        }
    }

    private static void GuardExpiry(int expiryMonth, int expiryYear)
    {
        if (expiryMonth is < _FirstMonthOfYear or > _LastMonthOfYear)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiryMonth),
                $"An expiry month must be between {_FirstMonthOfYear} and {_LastMonthOfYear}.");
        }

        if (expiryYear is < _EarliestExpiryYear or > _LatestExpiryYear)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiryYear),
                $"An expiry year must be between {_EarliestExpiryYear} and {_LatestExpiryYear}.");
        }
    }
}
