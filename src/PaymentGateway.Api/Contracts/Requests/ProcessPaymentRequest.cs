using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Contracts.Requests;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProcessPaymentRequest
{
    public string? CardNumber { get; init; }

    public int? ExpiryMonth { get; init; }

    public int? ExpiryYear { get; init; }

    public string? Currency { get; init; }

    public long? Amount { get; init; }

    public string? Cvv { get; init; }

    public override string ToString() =>
        $"{nameof(ProcessPaymentRequest)} {{ {nameof(ExpiryMonth)} = {ExpiryMonth}, {nameof(ExpiryYear)} = {ExpiryYear}, {nameof(Currency)} = {Currency}, {nameof(Amount)} = {Amount} }}";
}
