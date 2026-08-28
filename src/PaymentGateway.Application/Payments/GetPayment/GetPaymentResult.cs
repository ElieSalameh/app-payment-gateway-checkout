namespace PaymentGateway.Application.Payments.GetPayment;

public sealed record GetPaymentResult
{
    public required PaymentId Id { get; init; }

    public required PaymentStatus Status { get; init; }

    public required string LastFourCardDigits { get; init; }

    public required int ExpiryMonth { get; init; }

    public required int ExpiryYear { get; init; }

    public required string Currency { get; init; }

    public required long Amount { get; init; }

    public static GetPaymentResult From(Payment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);

        return new GetPaymentResult
        {
            Id = payment.Id,
            Status = payment.Status,
            LastFourCardDigits = payment.Card.LastFourDigits,
            ExpiryMonth = payment.Card.ExpiryMonth,
            ExpiryYear = payment.Card.ExpiryYear,
            Currency = payment.Amount.Currency.Code,
            Amount = payment.Amount.AmountInMinorUnits
        };
    }
}
