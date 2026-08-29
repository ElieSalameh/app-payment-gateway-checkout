namespace PaymentGateway.Infrastructure.Bank;

public sealed class BankSimulatorClient : IAcquiringBankClient
{
    private const string _PaymentsPath = "payments";
    private const string _ExpiryDateFormat = "MM/yyyy";

    private readonly HttpClient _httpClient;
    private readonly ILogger<BankSimulatorClient> _logger;

    public BankSimulatorClient(HttpClient httpClient, ILogger<BankSimulatorClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<AuthorizationResult> Authorize(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        using var response = await SendPayment(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Acquiring bank returned {StatusCode}, the payment outcome is unknown",
                (int)response.StatusCode);

            throw new AcquiringBankUnavailableException();
        }

        var payment = await ReadPayment(response, cancellationToken);

        return new AuthorizationResult
        {
            Status = payment.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            AuthorizationCode = payment.AuthorizationCode
        };
    }

    private async Task<HttpResponseMessage> SendPayment(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.PostAsJsonAsync(_PaymentsPath, ToSimulatorRequest(request), cancellationToken);
        }
        catch (TimeoutRejectedException exception)
        {
            _logger.LogWarning("Acquiring bank did not respond in time, the payment outcome is unknown");

            throw new AcquiringBankTimeoutException(exception);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Acquiring bank could not be reached, the payment outcome is unknown");

            throw new AcquiringBankUnavailableException(exception);
        }
    }

    private async Task<BankSimulatorPaymentResponse> ReadPayment(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<BankSimulatorPaymentResponse>(cancellationToken)
                ?? throw new AcquiringBankUnavailableException();
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Acquiring bank returned a response that could not be read");

            throw new AcquiringBankUnavailableException(exception);
        }
    }

    private static BankSimulatorPaymentRequest ToSimulatorRequest(AuthorizationRequest request) => new()
    {
        CardNumber = request.CardNumber,
        ExpiryDate = FormatExpiryDate(request.ExpiryMonth, request.ExpiryYear),
        Currency = request.Currency,
        Amount = request.Amount,
        Cvv = request.Cvv
    };

    private static string FormatExpiryDate(int expiryMonth, int expiryYear) =>
        new DateOnly(expiryYear, expiryMonth, 1).ToString(_ExpiryDateFormat, CultureInfo.InvariantCulture);
}
