namespace PaymentGateway.Infrastructure.Bank;

internal sealed record BankSimulatorPaymentResponse
{
    [JsonPropertyName("authorized")]
    public bool Authorized { get; init; }

    [JsonPropertyName("authorization_code")]
    public string? AuthorizationCode { get; init; }
}
