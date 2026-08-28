using PaymentGateway.Application.Exceptions;
using PaymentGateway.Application.Payments.GetPayment;
using PaymentGateway.Application.Payments.ProcessPayment;
using DomainPaymentStatus = PaymentGateway.Domain.Payments.PaymentStatus;
using PaymentId = PaymentGateway.Domain.Payments.PaymentId;

namespace PaymentGateway.Api.Controllers;

[ApiController]
[Route("payments")]
public sealed class PaymentsController : ControllerBase
{
    private const string _GetPaymentRouteName = "GetPayment";

    private readonly ProcessPaymentHandler _processPaymentHandler;
    private readonly GetPaymentHandler _getPaymentHandler;

    public PaymentsController(ProcessPaymentHandler processPaymentHandler, GetPaymentHandler getPaymentHandler)
    {
        ArgumentNullException.ThrowIfNull(processPaymentHandler);
        ArgumentNullException.ThrowIfNull(getPaymentHandler);

        _processPaymentHandler = processPaymentHandler;
        _getPaymentHandler = getPaymentHandler;
    }

    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<PaymentResponse>> ProcessPayment(
        ProcessPaymentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _processPaymentHandler.Handle(ToCommand(request), cancellationToken);
        var response = ToResponse(result);

        return CreatedAtRoute(_GetPaymentRouteName, new { paymentId = response.Id }, response);
    }

    [HttpGet("{paymentId:guid}", Name = _GetPaymentRouteName)]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentResponse>> GetPayment(Guid paymentId, CancellationToken cancellationToken)
    {
        if (paymentId == Guid.Empty)
        {
            throw new PaymentNotFoundException(paymentId);
        }

        var query = new GetPaymentQuery { PaymentId = PaymentId.From(paymentId) };
        var result = await _getPaymentHandler.Handle(query, cancellationToken);

        return Ok(ToResponse(result));
    }

    private static ProcessPaymentCommand ToCommand(ProcessPaymentRequest request) => new()
    {
        CardNumber = request.CardNumber,
        ExpiryMonth = request.ExpiryMonth,
        ExpiryYear = request.ExpiryYear,
        Currency = request.Currency,
        Amount = request.Amount,
        Cvv = request.Cvv
    };

    private static PaymentResponse ToResponse(ProcessPaymentResult result) => new()
    {
        Id = result.Id.Value,
        Status = ToWireStatus(result.Status),
        LastFourCardDigits = result.LastFourCardDigits,
        ExpiryMonth = result.ExpiryMonth,
        ExpiryYear = result.ExpiryYear,
        Currency = result.Currency,
        Amount = result.Amount
    };

    private static PaymentResponse ToResponse(GetPaymentResult result) => new()
    {
        Id = result.Id.Value,
        Status = ToWireStatus(result.Status),
        LastFourCardDigits = result.LastFourCardDigits,
        ExpiryMonth = result.ExpiryMonth,
        ExpiryYear = result.ExpiryYear,
        Currency = result.Currency,
        Amount = result.Amount
    };

    private static PaymentStatus ToWireStatus(DomainPaymentStatus status) => status switch
    {
        DomainPaymentStatus.Authorized => PaymentStatus.Authorized,
        DomainPaymentStatus.Declined => PaymentStatus.Declined,
        _ => throw new ArgumentOutOfRangeException(nameof(status), "A stored payment is either authorized or declined.")
    };
}
