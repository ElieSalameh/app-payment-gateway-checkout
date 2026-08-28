using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using DomainPaymentStatus = PaymentGateway.Domain.Payments.PaymentStatus;

namespace PaymentGateway.Api.IntegrationTests.Controllers;

public sealed class PaymentsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string _PaymentsRoute = "/payments";
    private const string _ProblemContentType = "application/problem+json";
    private const string _CardNumber = "2222405343248877";
    private const string _LastFourCardDigits = "8877";
    private const string _Cvv = "123";

    private readonly WebApplicationFactory<Program> _factory;

    public PaymentsEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task ProcessPayment_WhenTheBankAuthorizes_ReturnsCreatedWithTheStoredPayment()
    {
        var client = ClientFor(StubAcquiringBankClient.Returning(DomainPaymentStatus.Authorized));

        var response = await client.PostAsJsonAsync(_PaymentsRoute, ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payment = await ReadPaymentAsync(response);
        payment.Status.Should().Be(PaymentStatus.Authorized);
        payment.Id.Should().NotBeEmpty();
        payment.LastFourCardDigits.Should().Be(_LastFourCardDigits);
        payment.Amount.Should().Be(100);
        payment.Currency.Should().Be("GBP");
    }

    [Fact]
    public async Task ProcessPayment_WhenTheBankAuthorizes_PointsTheLocationHeaderAtTheStoredPayment()
    {
        var client = ClientFor(StubAcquiringBankClient.Returning(DomainPaymentStatus.Authorized));

        var response = await client.PostAsJsonAsync(_PaymentsRoute, ValidRequest());

        var payment = await ReadPaymentAsync(response);
        response.Headers.Location!.AbsolutePath.Should().Be($"{_PaymentsRoute}/{payment.Id}");
    }

    [Fact]
    public async Task ProcessPayment_WhenTheBankDeclines_ReturnsCreatedWithDeclinedStatus()
    {
        var client = ClientFor(StubAcquiringBankClient.Returning(DomainPaymentStatus.Declined));

        var response = await client.PostAsJsonAsync(_PaymentsRoute, ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ReadPaymentAsync(response)).Status.Should().Be(PaymentStatus.Declined);
    }

    [Fact]
    public async Task ProcessPayment_WhenTheRequestIsInvalid_ReturnsRejectedAndNeverCallsTheBank()
    {
        var bank = StubAcquiringBankClient.Returning(DomainPaymentStatus.Authorized);
        var client = ClientFor(bank);

        var response = await client.PostAsJsonAsync(_PaymentsRoute, ValidRequest() with { Cvv = "1" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be(_ProblemContentType);
        bank.WasCalled.Should().BeFalse();

        var problem = await ReadProblemAsync(response);
        problem.GetProperty("paymentStatus").GetString().Should().Be("Rejected");
        problem.GetProperty("errors").TryGetProperty("cvv", out _).Should().BeTrue();
        problem.TryGetProperty("id", out _).Should().BeFalse();
    }

    [Fact]
    public async Task ProcessPayment_WhenTheBankIsUnavailable_ReturnsBadGatewayAndStoresNothing()
    {
        var client = ClientFor(StubAcquiringBankClient.Throwing(new AcquiringBankUnavailableException()));

        var response = await client.PostAsJsonAsync(_PaymentsRoute, ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        response.Content.Headers.ContentType!.MediaType.Should().Be(_ProblemContentType);
        (await ReadProblemAsync(response)).GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ProcessPayment_WhenTheBankTimesOut_ReturnsGatewayTimeout()
    {
        var client = ClientFor(StubAcquiringBankClient.Throwing(new AcquiringBankTimeoutException(new TimeoutException())));

        var response = await client.PostAsJsonAsync(_PaymentsRoute, ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.GatewayTimeout);
    }

    [Fact]
    public async Task GetPayment_WhenThePaymentWasProcessed_ReturnsItWithTheCardMasked()
    {
        var client = ClientFor(StubAcquiringBankClient.Returning(DomainPaymentStatus.Authorized));
        var processed = await ReadPaymentAsync(await client.PostAsJsonAsync(_PaymentsRoute, ValidRequest()));

        var response = await client.GetAsync($"{_PaymentsRoute}/{processed.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var retrieved = await ReadPaymentAsync(response);
        retrieved.Should().BeEquivalentTo(processed);
        retrieved.LastFourCardDigits.Should().Be(_LastFourCardDigits);
    }

    [Fact]
    public async Task GetPayment_WhenThePaymentIsUnknown_ReturnsNotFound()
    {
        var client = ClientFor(StubAcquiringBankClient.Returning(DomainPaymentStatus.Authorized));

        var response = await client.GetAsync($"{_PaymentsRoute}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be(_ProblemContentType);
    }

    [Fact]
    public async Task GetPayment_WhenThePaymentIdIsEmpty_ReturnsNotFound()
    {
        var client = ClientFor(StubAcquiringBankClient.Returning(DomainPaymentStatus.Authorized));

        var response = await client.GetAsync($"{_PaymentsRoute}/{Guid.Empty}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PaymentResponses_WhenReturned_NeverCarryTheCardNumberOrCvv()
    {
        var client = ClientFor(StubAcquiringBankClient.Returning(DomainPaymentStatus.Authorized));

        var created = await client.PostAsJsonAsync(_PaymentsRoute, ValidRequest());
        var createdBody = await created.Content.ReadAsStringAsync();
        var payment = await ReadPaymentAsync(created);
        var retrievedBody = await client.GetStringAsync($"{_PaymentsRoute}/{payment.Id}");

        createdBody.Should().NotContain(_CardNumber).And.NotContain(_Cvv);
        retrievedBody.Should().NotContain(_CardNumber).And.NotContain(_Cvv);
    }

    private HttpClient ClientFor(IAcquiringBankClient bank) => _factory
        .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAcquiringBankClient>();
            services.AddSingleton(bank);
        }))
        .CreateClient();

    private static ProcessPaymentRequest ValidRequest() => new()
    {
        CardNumber = _CardNumber,
        ExpiryMonth = 4,
        ExpiryYear = 2030,
        Currency = "GBP",
        Amount = 100,
        Cvv = _Cvv
    };

    private static async Task<PaymentResponse> ReadPaymentAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<PaymentResponse>())!;

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.Clone();
    }
}
