namespace PaymentGateway.Application.Payments.ProcessPayment;

public sealed partial class ProcessPaymentHandler
{
    private const string _PaymentIdScopeTemplate = "PaymentId:{PaymentId}";

    private readonly IValidator<ProcessPaymentCommand> _validator;
    private readonly IAcquiringBankClient _acquiringBankClient;
    private readonly IPaymentRepository _paymentRepository;
    private readonly ILogger<ProcessPaymentHandler> _logger;

    public ProcessPaymentHandler(
        IValidator<ProcessPaymentCommand> validator,
        IAcquiringBankClient acquiringBankClient,
        IPaymentRepository paymentRepository,
        ILogger<ProcessPaymentHandler> logger)
    {
        _validator = validator;
        _acquiringBankClient = acquiringBankClient;
        _paymentRepository = paymentRepository;
        _logger = logger;
    }

    public async Task<ProcessPaymentResult> Handle(ProcessPaymentCommand command, CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var paymentId = PaymentId.New();

        using var paymentScope = _logger.BeginScope(_PaymentIdScopeTemplate, paymentId);

        var authorization = await _acquiringBankClient.Authorize(ToAuthorizationRequest(command), cancellationToken);
        var payment = ToPayment(paymentId, command, authorization.Status);

        await _paymentRepository.Add(payment, cancellationToken);

        PaymentRecorded(_logger, paymentId, payment.Status);

        return ProcessPaymentResult.From(payment);
    }

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Payment {PaymentId} recorded with status {PaymentStatus}")]
    private static partial void PaymentRecorded(ILogger logger, PaymentId paymentId, PaymentStatus paymentStatus);

    private static AuthorizationRequest ToAuthorizationRequest(ProcessPaymentCommand command) => new()
    {
        CardNumber = command.CardNumber!,
        ExpiryMonth = command.ExpiryMonth!.Value,
        ExpiryYear = command.ExpiryYear!.Value,
        Currency = command.Currency!,
        Amount = command.Amount!.Value,
        Cvv = command.Cvv!
    };

    private static Payment ToPayment(PaymentId paymentId, ProcessPaymentCommand command, PaymentStatus status)
    {
        var card = CardDetails.FromCardNumber(command.CardNumber!, command.ExpiryMonth!.Value, command.ExpiryYear!.Value);
        var amount = Money.FromMinorUnits(command.Amount!.Value, Currency.Parse(command.Currency));

        return status is PaymentStatus.Authorized
            ? Payment.Authorized(paymentId, card, amount)
            : Payment.Declined(paymentId, card, amount);
    }
}
