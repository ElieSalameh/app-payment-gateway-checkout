namespace PaymentGateway.Application.Exceptions;

public sealed class AcquiringBankTimeoutException : Exception
{
    private const string _Message = "The acquiring bank did not respond in time.";

    public AcquiringBankTimeoutException()
        : base(_Message)
    {
    }

    public AcquiringBankTimeoutException(Exception innerException)
        : base(_Message, innerException)
    {
    }
}
