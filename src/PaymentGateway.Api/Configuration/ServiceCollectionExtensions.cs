using System.Diagnostics;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.OpenApi.Models;

namespace PaymentGateway.Api.Configuration;

internal static class ServiceCollectionExtensions
{
    private const string TraceIdExtensionName = "traceId";
    private const string PaymentStatusExtensionName = "paymentStatus";
    private const string RejectedPaymentStatus = "Rejected";
    private const string ValidationProblemType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string ValidationProblemTitle = "One or more validation errors occurred.";
    private const string UnreadableFieldMessage = "The value is not valid for this field.";
    private const string UnreadableBodyMessage = "The request body could not be read as a payment request.";
    private const string JsonPathPrefix = "$.";
    private const string BodyFieldName = "body";
    private const long MaximumRequestBodySizeInBytes = 4 * 1024;

    public static IServiceCollection AddPaymentGatewayApi(this IServiceCollection services)
    {
        services
            .AddControllers()
            .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = RejectPayment);

        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions[TraceIdExtensionName] = ResolveTraceId(context.HttpContext));

        services.Configure<KestrelServerOptions>(options =>
        {
            options.AddServerHeader = false;
            options.Limits.MaxRequestBodySize = MaximumRequestBodySizeInBytes;
        });

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options => options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Payment Gateway",
            Version = "v1"
        }));

        return services;
    }

    private static IActionResult RejectPayment(ActionContext context)
    {
        var rejection = new ValidationProblemDetails(DescribeUnreadableFields(context.ModelState))
        {
            Type = ValidationProblemType,
            Title = ValidationProblemTitle,
            Status = StatusCodes.Status400BadRequest
        };

        rejection.Extensions[PaymentStatusExtensionName] = RejectedPaymentStatus;
        rejection.Extensions[TraceIdExtensionName] = ResolveTraceId(context.HttpContext);

        return new BadRequestObjectResult(rejection);
    }

    private static Dictionary<string, string[]> DescribeUnreadableFields(ModelStateDictionary modelState)
    {
        var unreadableFields = modelState.Keys
            .Where(key => key.StartsWith(JsonPathPrefix, StringComparison.Ordinal))
            .ToDictionary(
                key => key[JsonPathPrefix.Length..],
                _ => new[] { UnreadableFieldMessage });

        return unreadableFields.Count > 0
            ? unreadableFields
            : new Dictionary<string, string[]> { [BodyFieldName] = [UnreadableBodyMessage] };
    }

    private static string ResolveTraceId(HttpContext httpContext) =>
        Activity.Current?.Id ?? httpContext.TraceIdentifier;
}
