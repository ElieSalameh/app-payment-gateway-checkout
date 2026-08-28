namespace PaymentGateway.Application.Abstractions;

public interface IAcquiringBankClient
{
    Task<AuthorizationResult> Authorize(AuthorizationRequest request, CancellationToken cancellationToken);
}
