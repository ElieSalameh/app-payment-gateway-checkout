using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using DomainPaymentStatus = PaymentGateway.Domain.Payments.PaymentStatus;

namespace PaymentGateway.Api.IntegrationTests.Controllers;

public sealed class PaymentsLoggingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string _PaymentsRoute = "/payments";
    private const string _CardNumber = "2222405343248877";
    private const string _Cvv = "123";

    private readonly WebApplicationFactory<Program> _factory;

    public PaymentsLoggingTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task ProcessPayment_WhenThePaymentIsAuthorized_LogsNeitherTheCardNumberNorTheCvv()
    {
        var logs = new CapturingLoggerProvider();
        var client = ClientFor(StubAcquiringBankClient.Returning(DomainPaymentStatus.Authorized), logs);

        await client.PostAsJsonAsync(_PaymentsRoute, ValidRequest());

        logs.Rendered.Should().NotContain(_CardNumber).And.NotContain(_Cvv);
    }

    [Fact]
    public async Task ProcessPayment_WhenTheBankIsUnavailable_LogsNeitherTheCardNumberNorTheCvv()
    {
        var logs = new CapturingLoggerProvider();
        var client = ClientFor(StubAcquiringBankClient.Throwing(new AcquiringBankUnavailableException()), logs);

        await client.PostAsJsonAsync(_PaymentsRoute, ValidRequest());

        logs.Rendered.Should().NotContain(_CardNumber).And.NotContain(_Cvv);
    }

    [Fact]
    public async Task ProcessPayment_WhenTheRequestIsRejected_LogsTheFieldNamesWithoutTheSubmittedValues()
    {
        var logs = new CapturingLoggerProvider();
        var client = ClientFor(StubAcquiringBankClient.Returning(DomainPaymentStatus.Authorized), logs);

        await client.PostAsJsonAsync(_PaymentsRoute, ValidRequest() with { CardNumber = _CardNumber, Cvv = "1" });

        logs.Rendered.Should().Contain("Payment rejected on").And.Contain("cvv");
        logs.Rendered.Should().NotContain(_CardNumber);
    }

    [Fact]
    public async Task ProcessPayment_WhenThePaymentIsRecorded_LogsTheOutcomeUnderThePaymentIdScope()
    {
        var logs = new CapturingLoggerProvider();
        var client = ClientFor(StubAcquiringBankClient.Returning(DomainPaymentStatus.Authorized), logs);

        var response = await client.PostAsJsonAsync(_PaymentsRoute, ValidRequest());
        var payment = (await response.Content.ReadFromJsonAsync<PaymentResponse>())!;

        logs.Rendered.Should().Contain($"Payment {payment.Id} recorded with status Authorized");
        logs.Lines.Should().Contain(line => line.Contains("scope:") && line.Contains(payment.Id.ToString()));
    }

    [Fact]
    public async Task GetPayment_WhenThePaymentIsUnknown_LogsTheLookupAsAWarning()
    {
        var logs = new CapturingLoggerProvider();
        var client = ClientFor(StubAcquiringBankClient.Returning(DomainPaymentStatus.Authorized), logs);
        var missingPaymentId = Guid.NewGuid();

        await client.GetAsync($"{_PaymentsRoute}/{missingPaymentId}");

        logs.Lines.Should().Contain(line =>
            line.StartsWith("Warning") && line.Contains($"Payment {missingPaymentId} was not found"));
    }

    private HttpClient ClientFor(IAcquiringBankClient bank, CapturingLoggerProvider logs) => _factory
        .WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAcquiringBankClient>();
                services.AddSingleton(bank);
            });

            builder.ConfigureLogging(logging => logging.AddProvider(logs));
        })
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
}
