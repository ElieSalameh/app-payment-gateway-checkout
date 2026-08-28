namespace PaymentGateway.Application.Abstractions;

public sealed record AuthorizationResult
{
    public required PaymentStatus Status { get; init; }

    public string? AuthorizationCode { get; init; }
}
