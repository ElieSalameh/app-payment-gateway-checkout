namespace PaymentGateway.Api.Controllers;

[ApiController]
[Route("payments")]
public sealed class PaymentsController : ControllerBase
{
    private const string _NotImplementedTitle = "Payment processing is not available yet";
    private const string _NotImplementedDetail = "This release publishes the payment contract only. Processing and retrieval arrive in a later release.";

    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status504GatewayTimeout)]
    public ActionResult<PaymentResponse> ProcessPayment(ProcessPaymentRequest request) => NotImplementedYet();

    [HttpGet("{paymentId:guid}", Name = "GetPayment")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public ActionResult<PaymentResponse> GetPayment(Guid paymentId) => NotImplementedYet();

    private ObjectResult NotImplementedYet() => Problem(
        title: _NotImplementedTitle,
        detail: _NotImplementedDetail,
        statusCode: StatusCodes.Status501NotImplemented);
}
