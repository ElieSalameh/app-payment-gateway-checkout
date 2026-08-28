namespace PaymentGateway.Infrastructure.Tests.Persistence;

public sealed class InMemoryPaymentRepositoryTests
{
    private const string _CardNumber = "2222405343248877";
    private const string _LastFourDigits = "8877";
    private const int _ExpiryMonth = 4;
    private const int _ExpiryYear = 2030;
    private const long _AmountInMinorUnits = 100;
    private const int _ConcurrentWriterCount = 200;

    [Fact]
    public async Task GetById_WhenThePaymentWasAdded_ReturnsIt()
    {
        var repository = new InMemoryPaymentRepository();
        var payment = AuthorizedPayment(PaymentId.New());

        await repository.Add(payment, CancellationToken.None);
        var stored = await repository.GetById(payment.Id, CancellationToken.None);

        stored.Should().BeSameAs(payment);
    }

    [Fact]
    public async Task GetById_WhenTheIdIsUnknown_ReturnsNull()
    {
        var repository = new InMemoryPaymentRepository();

        var stored = await repository.GetById(PaymentId.New(), CancellationToken.None);

        stored.Should().BeNull();
    }

    [Fact]
    public async Task GetById_WhenAnotherPaymentIsStoredUnderADifferentId_ReturnsNull()
    {
        var repository = new InMemoryPaymentRepository();

        await repository.Add(AuthorizedPayment(PaymentId.New()), CancellationToken.None);
        var stored = await repository.GetById(PaymentId.New(), CancellationToken.None);

        stored.Should().BeNull();
    }

    [Fact]
    public async Task GetById_WhenThePaymentWasDeclined_PreservesTheStatus()
    {
        var repository = new InMemoryPaymentRepository();
        var payment = Payment.Declined(PaymentId.New(), Card(), Amount());

        await repository.Add(payment, CancellationToken.None);
        var stored = await repository.GetById(payment.Id, CancellationToken.None);

        stored!.Status.Should().Be(PaymentStatus.Declined);
    }

    [Fact]
    public async Task GetById_WhenThePaymentWasAdded_HoldsOnlyTheLastFourDigits()
    {
        var repository = new InMemoryPaymentRepository();
        var payment = AuthorizedPayment(PaymentId.New());

        await repository.Add(payment, CancellationToken.None);
        var stored = await repository.GetById(payment.Id, CancellationToken.None);

        stored!.Card.LastFourDigits.Should().Be(_LastFourDigits);
        stored.ToString().Should().NotContain(_CardNumber);
    }

    [Fact]
    public async Task Add_WhenTheIdIsAlreadyStored_Throws()
    {
        var repository = new InMemoryPaymentRepository();
        var paymentId = PaymentId.New();
        await repository.Add(AuthorizedPayment(paymentId), CancellationToken.None);

        var addAgain = async () => await repository.Add(AuthorizedPayment(paymentId), CancellationToken.None);

        await addAgain.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Add_WhenThePaymentIsNull_Throws()
    {
        var repository = new InMemoryPaymentRepository();

        var add = async () => await repository.Add(null!, CancellationToken.None);

        await add.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Add_WhenCancellationIsRequested_Throws()
    {
        var repository = new InMemoryPaymentRepository();
        var payment = AuthorizedPayment(PaymentId.New());

        var add = async () => await repository.Add(payment, new CancellationToken(canceled: true));

        await add.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetById_WhenCancellationIsRequested_Throws()
    {
        var repository = new InMemoryPaymentRepository();

        var get = async () => await repository.GetById(PaymentId.New(), new CancellationToken(canceled: true));

        await get.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Add_WhenManyPaymentsAreWrittenConcurrently_StoresEveryPayment()
    {
        var repository = new InMemoryPaymentRepository();
        var payments = Enumerable
            .Range(0, _ConcurrentWriterCount)
            .Select(_ => AuthorizedPayment(PaymentId.New()))
            .ToList();

        await Task.WhenAll(payments.Select(payment =>
            Task.Run(() => repository.Add(payment, CancellationToken.None))));

        foreach (var payment in payments)
        {
            var stored = await repository.GetById(payment.Id, CancellationToken.None);

            stored.Should().BeSameAs(payment);
        }
    }

    private static Payment AuthorizedPayment(PaymentId paymentId) =>
        Payment.Authorized(paymentId, Card(), Amount());

    private static CardDetails Card() =>
        CardDetails.FromCardNumber(_CardNumber, _ExpiryMonth, _ExpiryYear);

    private static Money Amount() =>
        Money.FromMinorUnits(_AmountInMinorUnits, Currency.Gbp);
}
