namespace PaymentGateway.Application.Payments.GetPayment;

public sealed record GetPaymentQuery
{
    public required PaymentId PaymentId { get; init; }
}
