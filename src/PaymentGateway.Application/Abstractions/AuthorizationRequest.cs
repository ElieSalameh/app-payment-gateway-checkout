namespace PaymentGateway.Application.Abstractions;

public sealed record AuthorizationRequest
{
    public required string CardNumber { get; init; }

    public required int ExpiryMonth { get; init; }

    public required int ExpiryYear { get; init; }

    public required string Currency { get; init; }

    public required long Amount { get; init; }

    public required string Cvv { get; init; }

    public override string ToString() =>
        $"{nameof(AuthorizationRequest)} {{ {nameof(ExpiryMonth)} = {ExpiryMonth}, {nameof(ExpiryYear)} = {ExpiryYear}, {nameof(Currency)} = {Currency}, {nameof(Amount)} = {Amount} }}";
}
