namespace PaymentGateway.Application.Payments.GetPayment;

public sealed partial class GetPaymentHandler
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ILogger<GetPaymentHandler> _logger;

    public GetPaymentHandler(IPaymentRepository paymentRepository, ILogger<GetPaymentHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _logger = logger;
    }

    public async Task<GetPaymentResult> Handle(GetPaymentQuery query, CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetById(query.PaymentId, cancellationToken);

        if (payment is null)
        {
            PaymentNotFound(_logger, query.PaymentId);

            throw new PaymentNotFoundException(query.PaymentId);
        }

        return GetPaymentResult.From(payment);
    }

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Payment {PaymentId} was not found")]
    private static partial void PaymentNotFound(ILogger logger, PaymentId paymentId);
}
