namespace PaymentGateway.Application.Exceptions;

public sealed class PaymentNotFoundException : Exception
{
    public PaymentNotFoundException(PaymentId paymentId)
        : base($"Payment {paymentId} was not found.")
    {
    }
}
