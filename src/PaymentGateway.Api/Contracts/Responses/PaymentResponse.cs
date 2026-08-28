namespace PaymentGateway.Api.Contracts.Responses;

public sealed record PaymentResponse
{
    public required Guid Id { get; init; }

    public required PaymentStatus Status { get; init; }

    public required string LastFourCardDigits { get; init; }

    public required int ExpiryMonth { get; init; }

    public required int ExpiryYear { get; init; }

    public required string Currency { get; init; }

    public required long Amount { get; init; }
}
