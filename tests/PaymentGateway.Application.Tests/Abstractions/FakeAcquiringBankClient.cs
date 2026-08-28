namespace PaymentGateway.Application.Tests.Abstractions;

internal sealed class FakeAcquiringBankClient : IAcquiringBankClient
{
    private const string _AuthorizationCode = "0bb07405-6d44-4b50-a14f-7ae0beff13ad";

    private readonly Func<AuthorizationResult> _respond;

    private FakeAcquiringBankClient(Func<AuthorizationResult> respond) => _respond = respond;

    public AuthorizationRequest? ReceivedRequest { get; private set; }

    public bool WasCalled { get; private set; }

    public static FakeAcquiringBankClient Returning(PaymentStatus status) =>
        new(() => new AuthorizationResult { Status = status, AuthorizationCode = _AuthorizationCode });

    public static FakeAcquiringBankClient Throwing(Exception exception) =>
        new(() => throw exception);

    public Task<AuthorizationResult> Authorize(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        WasCalled = true;
        ReceivedRequest = request;

        return Task.FromResult(_respond());
    }
}
