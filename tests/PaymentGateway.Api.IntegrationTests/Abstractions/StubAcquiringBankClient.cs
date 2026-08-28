using DomainPaymentStatus = PaymentGateway.Domain.Payments.PaymentStatus;

namespace PaymentGateway.Api.IntegrationTests.Abstractions;

internal sealed class StubAcquiringBankClient : IAcquiringBankClient
{
    private const string _AuthorizationCode = "0bb07405-6d44-4b50-a14f-7ae0beff13ad";

    private readonly Func<AuthorizationResult> _respond;

    private StubAcquiringBankClient(Func<AuthorizationResult> respond) => _respond = respond;

    public bool WasCalled { get; private set; }

    public static StubAcquiringBankClient Returning(DomainPaymentStatus status) =>
        new(() => new AuthorizationResult { Status = status, AuthorizationCode = _AuthorizationCode });

    public static StubAcquiringBankClient Throwing(Exception exception) =>
        new(() => throw exception);

    public Task<AuthorizationResult> Authorize(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        WasCalled = true;

        return Task.FromResult(_respond());
    }
}
