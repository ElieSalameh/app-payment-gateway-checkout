namespace PaymentGateway.Application.Exceptions;

public sealed class PaymentNotFoundException : Exception
{
    public PaymentNotFoundException(PaymentId paymentId)
        : base(DescribeMissing(paymentId.Value))
    {
    }

    public PaymentNotFoundException(Guid paymentId)
        : base(DescribeMissing(paymentId))
    {
    }

    private static string DescribeMissing(Guid paymentId) => $"Payment {paymentId} was not found.";
}
