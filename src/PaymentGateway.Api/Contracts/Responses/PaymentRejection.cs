namespace PaymentGateway.Api.Contracts.Responses;

internal static class PaymentRejection
{
    private const string _Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string _Title = "One or more validation errors occurred.";
    private const string _PaymentStatusExtensionName = "paymentStatus";
    private const string _RejectedPaymentStatus = "Rejected";

    public static ValidationProblemDetails Describe(IDictionary<string, string[]> errors)
    {
        var rejection = new ValidationProblemDetails(errors)
        {
            Type = _Type,
            Title = _Title,
            Status = StatusCodes.Status400BadRequest
        };

        rejection.Extensions[_PaymentStatusExtensionName] = _RejectedPaymentStatus;

        return rejection;
    }
}
