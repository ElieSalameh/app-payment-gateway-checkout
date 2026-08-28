namespace PaymentGateway.Application.Tests.Payments.ProcessPayment;

public sealed class ProcessPaymentHandlerTests
{
    private const string _CardNumber = "2222405343248877";
    private const string _LastFourCardDigits = "8877";
    private const string _Cvv = "123";
    private const string _Currency = "GBP";
    private const long _Amount = 100;

    private static readonly DateTimeOffset _Now = new(2026, 8, 28, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WhenTheBankAuthorizes_ReturnsAuthorized()
    {
        var bank = FakeAcquiringBankClient.Returning(PaymentStatus.Authorized);

        var result = await HandlerFor(bank, new FakePaymentRepository()).Handle(ValidCommand(), CancellationToken.None);

        result.Status.Should().Be(PaymentStatus.Authorized);
        result.Id.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTheBankDeclines_ReturnsDeclined()
    {
        var bank = FakeAcquiringBankClient.Returning(PaymentStatus.Declined);

        var result = await HandlerFor(bank, new FakePaymentRepository()).Handle(ValidCommand(), CancellationToken.None);

        result.Status.Should().Be(PaymentStatus.Declined);
    }

    [Theory]
    [InlineData(PaymentStatus.Authorized)]
    [InlineData(PaymentStatus.Declined)]
    public async Task Handle_WhenTheBankAnswers_StoresThePaymentUnderTheReturnedId(PaymentStatus status)
    {
        var repository = new FakePaymentRepository();

        var result = await HandlerFor(FakeAcquiringBankClient.Returning(status), repository)
            .Handle(ValidCommand(), CancellationToken.None);

        var stored = await repository.GetById(result.Id, CancellationToken.None);
        stored.Should().NotBeNull();
        stored!.Status.Should().Be(status);
    }

    [Fact]
    public async Task Handle_WhenTheBankAuthorizes_ReturnsTheSubmittedPaymentDetails()
    {
        var handler = HandlerFor(FakeAcquiringBankClient.Returning(PaymentStatus.Authorized), new FakePaymentRepository());

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.LastFourCardDigits.Should().Be(_LastFourCardDigits);
        result.ExpiryMonth.Should().Be(_Now.Month);
        result.ExpiryYear.Should().Be(_Now.Year + 1);
        result.Currency.Should().Be(_Currency);
        result.Amount.Should().Be(_Amount);
    }

    [Fact]
    public async Task Handle_Always_SendsTheFullCardNumberAndCvvToTheBank()
    {
        var bank = FakeAcquiringBankClient.Returning(PaymentStatus.Authorized);

        await HandlerFor(bank, new FakePaymentRepository()).Handle(ValidCommand(), CancellationToken.None);

        bank.ReceivedRequest.Should().NotBeNull();
        bank.ReceivedRequest!.CardNumber.Should().Be(_CardNumber);
        bank.ReceivedRequest.Cvv.Should().Be(_Cvv);
    }

    [Fact]
    public async Task Handle_Always_StoresOnlyTheLastFourCardDigits()
    {
        var repository = new FakePaymentRepository();

        await HandlerFor(FakeAcquiringBankClient.Returning(PaymentStatus.Authorized), repository)
            .Handle(ValidCommand(), CancellationToken.None);

        var stored = repository.StoredPayments.Single();
        stored.Card.LastFourDigits.Should().Be(_LastFourCardDigits);
        stored.ToString().Should().NotContain(_CardNumber);
    }

    [Fact]
    public async Task Handle_WhenTheCommandIsInvalid_ThrowsValidationException()
    {
        var handler = HandlerFor(FakeAcquiringBankClient.Returning(PaymentStatus.Authorized), new FakePaymentRepository());

        var handle = () => handler.Handle(ValidCommand() with { Cvv = "12" }, CancellationToken.None);

        await handle.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_WhenTheCommandIsInvalid_DoesNotCallTheBankAndStoresNothing()
    {
        var bank = FakeAcquiringBankClient.Returning(PaymentStatus.Authorized);
        var repository = new FakePaymentRepository();

        var handle = () => HandlerFor(bank, repository).Handle(ValidCommand() with { Amount = 0 }, CancellationToken.None);

        await handle.Should().ThrowAsync<ValidationException>();
        bank.WasCalled.Should().BeFalse();
        repository.StoredPayments.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTheBankIsUnavailable_PropagatesTheFailureAndStoresNothing()
    {
        var bank = FakeAcquiringBankClient.Throwing(new AcquiringBankUnavailableException());
        var repository = new FakePaymentRepository();

        var handle = () => HandlerFor(bank, repository).Handle(ValidCommand(), CancellationToken.None);

        await handle.Should().ThrowAsync<AcquiringBankUnavailableException>();
        repository.StoredPayments.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTheBankTimesOut_PropagatesTheFailureAndStoresNothing()
    {
        var bank = FakeAcquiringBankClient.Throwing(new AcquiringBankTimeoutException(new TimeoutException()));
        var repository = new FakePaymentRepository();

        var handle = () => HandlerFor(bank, repository).Handle(ValidCommand(), CancellationToken.None);

        await handle.Should().ThrowAsync<AcquiringBankTimeoutException>();
        repository.StoredPayments.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTheCommandIsNull_ThrowsArgumentNullException()
    {
        var handler = HandlerFor(FakeAcquiringBankClient.Returning(PaymentStatus.Authorized), new FakePaymentRepository());

        var handle = () => handler.Handle(null!, CancellationToken.None);

        await handle.Should().ThrowAsync<ArgumentNullException>();
    }

    private static ProcessPaymentHandler HandlerFor(FakeAcquiringBankClient bank, FakePaymentRepository repository) =>
        new(
            new ProcessPaymentValidator(new FakeTimeProvider(_Now)),
            bank,
            repository,
            NullLogger<ProcessPaymentHandler>.Instance);

    private static ProcessPaymentCommand ValidCommand() => new()
    {
        CardNumber = _CardNumber,
        ExpiryMonth = _Now.Month,
        ExpiryYear = _Now.Year + 1,
        Currency = _Currency,
        Amount = _Amount,
        Cvv = _Cvv
    };
}
