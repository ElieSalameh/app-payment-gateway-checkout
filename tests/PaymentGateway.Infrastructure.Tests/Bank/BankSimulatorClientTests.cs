namespace PaymentGateway.Infrastructure.Tests.Bank;

public sealed class BankSimulatorClientTests
{
    private const string _CardNumber = "2222405343248877";
    private const string _Cvv = "123";
    private const string _AuthorizationCode = "0bb07405-6d44-4b50-a14f-7ae0beff13ad";
    private const string _BaseAddress = "http://localhost:8080";

    [Fact]
    public async Task Authorize_WhenTheBankAuthorizes_ReturnsAuthorized()
    {
        var handler = RespondingWith(HttpStatusCode.OK, AuthorizedBody(authorized: true));

        var result = await ClientFor(handler).Authorize(ValidRequest(), CancellationToken.None);

        result.Status.Should().Be(PaymentStatus.Authorized);
        result.AuthorizationCode.Should().Be(_AuthorizationCode);
    }

    [Fact]
    public async Task Authorize_WhenTheBankDoesNotAuthorize_ReturnsDeclined()
    {
        var handler = RespondingWith(HttpStatusCode.OK, AuthorizedBody(authorized: false));

        var result = await ClientFor(handler).Authorize(ValidRequest(), CancellationToken.None);

        result.Status.Should().Be(PaymentStatus.Declined);
    }

    [Fact]
    public async Task Authorize_Always_PostsToThePaymentsPath()
    {
        var handler = RespondingWith(HttpStatusCode.OK, AuthorizedBody(authorized: true));

        await ClientFor(handler).Authorize(ValidRequest(), CancellationToken.None);

        handler.RequestedUri.Should().Be(new Uri($"{_BaseAddress}/payments"));
    }

    [Fact]
    public async Task Authorize_Always_SendsTheBankSnakeCaseFieldNames()
    {
        var handler = RespondingWith(HttpStatusCode.OK, AuthorizedBody(authorized: true));

        await ClientFor(handler).Authorize(ValidRequest(), CancellationToken.None);

        var body = JsonDocument.Parse(handler.RequestBody!).RootElement;
        body.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "card_number",
            "expiry_date",
            "currency",
            "amount",
            "cvv");
    }

    [Fact]
    public async Task Authorize_Always_SendsTheCardDetailsTheBankNeeds()
    {
        var handler = RespondingWith(HttpStatusCode.OK, AuthorizedBody(authorized: true));

        await ClientFor(handler).Authorize(ValidRequest(), CancellationToken.None);

        var body = JsonDocument.Parse(handler.RequestBody!).RootElement;
        body.GetProperty("card_number").GetString().Should().Be(_CardNumber);
        body.GetProperty("cvv").GetString().Should().Be(_Cvv);
        body.GetProperty("currency").GetString().Should().Be("GBP");
        body.GetProperty("amount").GetInt64().Should().Be(100);
    }

    [Theory]
    [InlineData(4, 2025, "04/2025")]
    [InlineData(12, 2030, "12/2030")]
    [InlineData(1, 2099, "01/2099")]
    public async Task Authorize_Always_SendsTheExpiryAsMonthAndFourDigitYear(
        int expiryMonth,
        int expiryYear,
        string expected)
    {
        var handler = RespondingWith(HttpStatusCode.OK, AuthorizedBody(authorized: true));
        var request = ValidRequest() with { ExpiryMonth = expiryMonth, ExpiryYear = expiryYear };

        await ClientFor(handler).Authorize(request, CancellationToken.None);

        var body = JsonDocument.Parse(handler.RequestBody!).RootElement;
        body.GetProperty("expiry_date").GetString().Should().Be(expected);
    }

    [Fact]
    public async Task Authorize_WhenTheBankIsUnavailable_ThrowsAcquiringBankUnavailableException()
    {
        var handler = RespondingWith(HttpStatusCode.ServiceUnavailable, string.Empty);

        var authorize = async () => await ClientFor(handler).Authorize(ValidRequest(), CancellationToken.None);

        await authorize.Should().ThrowAsync<AcquiringBankUnavailableException>();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task Authorize_WhenTheBankReturnsAnErrorStatus_ThrowsAcquiringBankUnavailableException(
        HttpStatusCode statusCode)
    {
        var handler = RespondingWith(statusCode, string.Empty);

        var authorize = async () => await ClientFor(handler).Authorize(ValidRequest(), CancellationToken.None);

        await authorize.Should().ThrowAsync<AcquiringBankUnavailableException>();
    }

    [Fact]
    public async Task Authorize_WhenTheBankCannotBeReached_ThrowsAcquiringBankUnavailableException()
    {
        var handler = Throwing(new HttpRequestException("connection refused"));

        var authorize = async () => await ClientFor(handler).Authorize(ValidRequest(), CancellationToken.None);

        await authorize.Should().ThrowAsync<AcquiringBankUnavailableException>();
    }

    [Fact]
    public async Task Authorize_WhenTheBankReturnsUnreadableJson_ThrowsAcquiringBankUnavailableException()
    {
        var handler = RespondingWith(HttpStatusCode.OK, "not json");

        var authorize = async () => await ClientFor(handler).Authorize(ValidRequest(), CancellationToken.None);

        await authorize.Should().ThrowAsync<AcquiringBankUnavailableException>();
    }

    [Fact]
    public async Task Authorize_WhenTheBankTimesOut_ThrowsAcquiringBankTimeoutException()
    {
        var handler = Throwing(new TimeoutRejectedException());

        var authorize = async () => await ClientFor(handler).Authorize(ValidRequest(), CancellationToken.None);

        await authorize.Should().ThrowAsync<AcquiringBankTimeoutException>();
    }

    [Fact]
    public async Task Authorize_WhenADependencyFailureIsSurfaced_NeverEchoesTheCardNumberOrCvv()
    {
        var handler = RespondingWith(HttpStatusCode.ServiceUnavailable, string.Empty);

        var authorize = async () => await ClientFor(handler).Authorize(ValidRequest(), CancellationToken.None);

        var thrown = await authorize.Should().ThrowAsync<AcquiringBankUnavailableException>();
        thrown.Which.Message.Should().NotContain(_CardNumber);
        thrown.Which.Message.Should().NotContain(_Cvv);
    }

    [Fact]
    public async Task Authorize_WhenCancellationIsRequested_ThrowsTaskCanceledException()
    {
        var handler = RespondingWith(HttpStatusCode.OK, AuthorizedBody(authorized: true));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var authorize = async () => await ClientFor(handler).Authorize(ValidRequest(), cancellation.Token);

        await authorize.Should().ThrowAsync<TaskCanceledException>();
    }

    private static BankSimulatorClient ClientFor(StubHttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri(_BaseAddress) },
            NullLogger<BankSimulatorClient>.Instance);

    private static StubHttpMessageHandler RespondingWith(HttpStatusCode statusCode, string body) =>
        new(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });

    private static StubHttpMessageHandler Throwing(Exception exception) =>
        new(_ => throw exception);

    private static string AuthorizedBody(bool authorized) =>
        $$"""{"authorized": {{(authorized ? "true" : "false")}}, "authorization_code": "{{_AuthorizationCode}}"}""";

    private static AuthorizationRequest ValidRequest() => new()
    {
        CardNumber = _CardNumber,
        ExpiryMonth = 4,
        ExpiryYear = 2030,
        Currency = "GBP",
        Amount = 100,
        Cvv = _Cvv
    };
}
