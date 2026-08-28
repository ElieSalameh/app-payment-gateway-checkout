using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PaymentGateway.Api.IntegrationTests.Controllers;

public sealed class PaymentsContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string _ProblemContentType = "application/problem+json";
    private const string _PaymentsRoute = "/payments";
    private const string _UnbindableBody = """{"cardNumber":"2222405343248877","expiryMonth":"not-a-month"}""";
    private const string _BodyWithUnknownProperty = """{"cardNumber":"2222405343248877","merchantId":"smuggled"}""";

    private readonly WebApplicationFactory<Program> _factory;

    public PaymentsContractTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetPayment_WhenPaymentIdIsNotAGuid_ReturnsNotFound()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"{_PaymentsRoute}/not-a-guid");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ProcessPayment_WhenBodyCannotBeBound_ReturnsRejectedValidationProblem()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(_PaymentsRoute, JsonBody(_UnbindableBody));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be(_ProblemContentType);

        var problem = await ReadProblemAsync(response);
        problem.GetProperty("paymentStatus").GetString().Should().Be("Rejected");
        problem.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status400BadRequest);
        problem.TryGetProperty("errors", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ProcessPayment_WhenBodyContainsAnUnknownProperty_ReturnsRejectedValidationProblem()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(_PaymentsRoute, JsonBody(_BodyWithUnknownProperty));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await ReadProblemAsync(response);
        problem.GetProperty("paymentStatus").GetString().Should().Be("Rejected");
    }

    [Theory]
    [InlineData(_PaymentsRoute, "post")]
    [InlineData($"{_PaymentsRoute}/not-a-guid", "get")]
    public async Task ErrorResponses_WhenReturned_CarryATraceId(string route, string method)
    {
        var client = _factory.CreateClient();

        var response = method == "post"
            ? await client.PostAsync(route, JsonBody(_UnbindableBody))
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

        paths.GetProperty(_PaymentsRoute).TryGetProperty("post", out var processOperation).Should().BeTrue();
        processOperation.GetProperty("responses").TryGetProperty("201", out _).Should().BeTrue();
        processOperation.GetProperty("responses").TryGetProperty("400", out _).Should().BeTrue();
        processOperation.GetProperty("responses").TryGetProperty("502", out _).Should().BeTrue();
        processOperation.GetProperty("responses").TryGetProperty("504", out _).Should().BeTrue();

        paths.GetProperty($"{_PaymentsRoute}/{{paymentId}}").TryGetProperty("get", out var retrieveOperation).Should().BeTrue();
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

    private static StringContent JsonBody(string body) => new(body, Encoding.UTF8, "application/json");

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.Clone();
    }
}
