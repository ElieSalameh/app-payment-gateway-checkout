namespace PaymentGateway.Application.Exceptions;

public sealed class AcquiringBankUnavailableException : Exception
{
    private const string _Message = "The acquiring bank is unavailable.";

    public AcquiringBankUnavailableException()
        : base(_Message)
    {
    }

    public AcquiringBankUnavailableException(Exception innerException)
        : base(_Message, innerException)
    {
    }
}
