using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PaymentGateway.Api.IntegrationTests.Controllers;

public sealed class PaymentsContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ProblemContentType = "application/problem+json";
    private const string PaymentsRoute = "/payments";

    private readonly WebApplicationFactory<Program> _factory;

    public PaymentsContractTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProcessPayment_WhenGivenAValidBody_RoutesToThePaymentsEndpoint()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(PaymentsRoute, ValidRequestBody());

        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemContentType);
    }

    [Fact]
    public async Task GetPayment_WhenGivenAPaymentId_RoutesToThePaymentsEndpoint()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"{PaymentsRoute}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemContentType);
    }

    [Fact]
    public async Task GetPayment_WhenPaymentIdIsNotAGuid_ReturnsNotFound()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"{PaymentsRoute}/not-a-guid");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ProcessPayment_WhenBodyCannotBeBound_ReturnsRejectedValidationProblem()
    {
        var client = _factory.CreateClient();
        var unbindableBody = new StringContent(
            """{"cardNumber":"2222405343248877","expiryMonth":"not-a-month"}""",
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync(PaymentsRoute, unbindableBody);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemContentType);

        var problem = await ReadProblemAsync(response);
        problem.GetProperty("paymentStatus").GetString().Should().Be("Rejected");
        problem.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status400BadRequest);
        problem.TryGetProperty("errors", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ProcessPayment_WhenBodyContainsAnUnknownProperty_ReturnsRejectedValidationProblem()
    {
        var client = _factory.CreateClient();
        var bodyWithUnknownProperty = new StringContent(
            """{"cardNumber":"2222405343248877","merchantId":"smuggled"}""",
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync(PaymentsRoute, bodyWithUnknownProperty);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await ReadProblemAsync(response);
        problem.GetProperty("paymentStatus").GetString().Should().Be("Rejected");
    }

    [Theory]
    [InlineData(PaymentsRoute, "post")]
    [InlineData($"{PaymentsRoute}/not-a-guid", "get")]
    public async Task ErrorResponses_WhenReturned_CarryATraceId(string route, string method)
    {
        var client = _factory.CreateClient();

        var response = method == "post"
            ? await client.PostAsJsonAsync(route, ValidRequestBody())
            : await client.GetAsync(route);

        var problem = await ReadProblemAsync(response);
        problem.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task OpenApiDocument_WhenRequested_DescribesBothPaymentOperations()
    {
        var client = _factory
            .WithWebHostBuilder(builder => builder.UseEnvironment(Environments.Development))
            .CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        paths.GetProperty(PaymentsRoute).TryGetProperty("post", out var processOperation).Should().BeTrue();
        processOperation.GetProperty("responses").TryGetProperty("201", out _).Should().BeTrue();
        processOperation.GetProperty("responses").TryGetProperty("400", out _).Should().BeTrue();
        processOperation.GetProperty("responses").TryGetProperty("502", out _).Should().BeTrue();
        processOperation.GetProperty("responses").TryGetProperty("504", out _).Should().BeTrue();

        paths.GetProperty($"{PaymentsRoute}/{{paymentId}}").TryGetProperty("get", out var retrieveOperation).Should().BeTrue();
        retrieveOperation.GetProperty("responses").TryGetProperty("200", out _).Should().BeTrue();
        retrieveOperation.GetProperty("responses").TryGetProperty("404", out _).Should().BeTrue();
    }

    [Fact]
    public async Task PaymentResponseSchema_WhenPublished_ExposesOnlyTheLastFourCardDigits()
    {
        var client = _factory
            .WithWebHostBuilder(builder => builder.UseEnvironment(Environments.Development))
            .CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        var paymentResponseProperties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty(nameof(PaymentResponse))
            .GetProperty("properties");

        paymentResponseProperties.TryGetProperty("lastFourCardDigits", out _).Should().BeTrue();
        paymentResponseProperties.TryGetProperty("cardNumber", out _).Should().BeFalse();
        paymentResponseProperties.TryGetProperty("cvv", out _).Should().BeFalse();
    }

    private static ProcessPaymentRequest ValidRequestBody() => new()
    {
        CardNumber = "2222405343248877",
        ExpiryMonth = 4,
        ExpiryYear = 2030,
        Currency = "GBP",
        Amount = 100,
        Cvv = "123"
    };

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
