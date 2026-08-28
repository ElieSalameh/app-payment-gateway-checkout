namespace PaymentGateway.Application.Tests.Payments.GetPayment;

public sealed class GetPaymentHandlerTests
{
    private const string _CardNumber = "2222405343248877";
    private const string _LastFourCardDigits = "8877";
    private const long _Amount = 100;
    private const int _ExpiryMonth = 4;
    private const int _ExpiryYear = 2030;

    [Fact]
    public async Task Handle_WhenThePaymentExists_ReturnsTheStoredPayment()
    {
        var repository = new FakePaymentRepository();
        var payment = AuthorizedPayment();
        await repository.Add(payment, CancellationToken.None);

        var result = await HandlerFor(repository).Handle(QueryFor(payment.Id), CancellationToken.None);

        result.Id.Should().Be(payment.Id);
        result.Status.Should().Be(PaymentStatus.Authorized);
        result.LastFourCardDigits.Should().Be(_LastFourCardDigits);
        result.ExpiryMonth.Should().Be(_ExpiryMonth);
        result.ExpiryYear.Should().Be(_ExpiryYear);
        result.Currency.Should().Be(Currency.Gbp.Code);
        result.Amount.Should().Be(_Amount);
    }

    [Fact]
    public async Task Handle_WhenThePaymentDoesNotExist_ThrowsPaymentNotFoundException()
    {
        var handler = HandlerFor(new FakePaymentRepository());

        var handle = () => handler.Handle(QueryFor(PaymentId.New()), CancellationToken.None);

        await handle.Should().ThrowAsync<PaymentNotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenAnotherPaymentIsStored_ThrowsPaymentNotFoundException()
    {
        var repository = new FakePaymentRepository();
        await repository.Add(AuthorizedPayment(), CancellationToken.None);

        var handle = () => HandlerFor(repository).Handle(QueryFor(PaymentId.New()), CancellationToken.None);

        await handle.Should().ThrowAsync<PaymentNotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenTheQueryIsNull_ThrowsArgumentNullException()
    {
        var handler = HandlerFor(new FakePaymentRepository());

        var handle = () => handler.Handle(null!, CancellationToken.None);

        await handle.Should().ThrowAsync<ArgumentNullException>();
    }

    private static GetPaymentHandler HandlerFor(FakePaymentRepository repository) =>
        new(repository, NullLogger<GetPaymentHandler>.Instance);

    private static GetPaymentQuery QueryFor(PaymentId paymentId) => new() { PaymentId = paymentId };

    private static Payment AuthorizedPayment() => Payment.Authorized(
        PaymentId.New(),
        CardDetails.FromCardNumber(_CardNumber, _ExpiryMonth, _ExpiryYear),
        Money.FromMinorUnits(_Amount, Currency.Gbp));
}
