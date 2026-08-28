using System.Diagnostics;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.OpenApi.Models;

namespace PaymentGateway.Api.Configuration;

internal static class ServiceCollectionExtensions
{
    private const string _TraceIdExtensionName = "traceId";
    private const string _PaymentStatusExtensionName = "paymentStatus";
    private const string _RejectedPaymentStatus = "Rejected";
    private const string _ValidationProblemType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string _ValidationProblemTitle = "One or more validation errors occurred.";
    private const string _UnreadableFieldMessage = "The value is not valid for this field.";
    private const string _UnreadableBodyMessage = "The request body could not be read as a payment request.";
    private const string _JsonPathPrefix = "$.";
    private const string _BodyFieldName = "body";
    private const long _MaximumRequestBodySizeInBytes = 4 * 1024;

    public static IServiceCollection AddPaymentGatewayApi(this IServiceCollection services)
    {
        services
            .AddControllers()
            .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = RejectPayment);

        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions[_TraceIdExtensionName] = ResolveTraceId(context.HttpContext));

        services.Configure<KestrelServerOptions>(options =>
        {
            options.AddServerHeader = false;
            options.Limits.MaxRequestBodySize = _MaximumRequestBodySizeInBytes;
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
            Type = _ValidationProblemType,
            Title = _ValidationProblemTitle,
            Status = StatusCodes.Status400BadRequest
        };

        rejection.Extensions[_PaymentStatusExtensionName] = _RejectedPaymentStatus;
        rejection.Extensions[_TraceIdExtensionName] = ResolveTraceId(context.HttpContext);

        return new BadRequestObjectResult(rejection);
    }

    private static Dictionary<string, string[]> DescribeUnreadableFields(ModelStateDictionary modelState)
    {
        var unreadableFields = modelState.Keys
            .Where(key => key.StartsWith(_JsonPathPrefix, StringComparison.Ordinal))
            .ToDictionary(
                key => key[_JsonPathPrefix.Length..],
                _ => new[] { _UnreadableFieldMessage });

        return unreadableFields.Count > 0
            ? unreadableFields
            : new Dictionary<string, string[]> { [_BodyFieldName] = [_UnreadableBodyMessage] };
    }

    private static string ResolveTraceId(HttpContext httpContext) =>
        Activity.Current?.Id ?? httpContext.TraceIdentifier;
}
