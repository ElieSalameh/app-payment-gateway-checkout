namespace PaymentGateway.Domain.Payments;

public sealed record Payment
{
    private Payment(PaymentId id, PaymentStatus status, CardDetails card, Money amount)
    {
        Id = id;
        Status = status;
        Card = card;
        Amount = amount;
    }

    public PaymentId Id { get; }

    public PaymentStatus Status { get; }

    public CardDetails Card { get; }

    public Money Amount { get; }

    public static Payment Authorized(PaymentId id, CardDetails card, Money amount) =>
        Create(id, PaymentStatus.Authorized, card, amount);

    public static Payment Declined(PaymentId id, CardDetails card, Money amount) =>
        Create(id, PaymentStatus.Declined, card, amount);

    public override string ToString() => $"Payment {Id} {Status} {Amount}";

    private static Payment Create(PaymentId id, PaymentStatus status, CardDetails card, Money amount)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(amount);

        return new Payment(id, status, card, amount);
    }
}
