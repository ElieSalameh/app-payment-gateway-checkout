namespace PaymentGateway.Domain.Payments;

public readonly record struct PaymentId
{
    private PaymentId(Guid value) => Value = value;

    public Guid Value { get; }

    public static PaymentId New() => new(Guid.NewGuid());

    public static PaymentId From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("A payment id cannot be empty.", nameof(value))
        : new PaymentId(value);

    public override string ToString() => Value.ToString();
}
