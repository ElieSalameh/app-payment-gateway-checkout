namespace PaymentGateway.Domain.Shared;

public sealed record Money
{
    private Money(long amountInMinorUnits, Currency currency)
    {
        AmountInMinorUnits = amountInMinorUnits;
        Currency = currency;
    }

    public long AmountInMinorUnits { get; }

    public Currency Currency { get; }

    public static Money FromMinorUnits(long amountInMinorUnits, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);

        if (amountInMinorUnits <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amountInMinorUnits),
                "An amount must be greater than zero.");
        }

        return new Money(amountInMinorUnits, currency);
    }

    public override string ToString() => $"{AmountInMinorUnits} {Currency.Code}";
}
