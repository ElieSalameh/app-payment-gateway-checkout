using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using PaymentGateway.Application.Exceptions;

namespace PaymentGateway.Api.Middleware;

internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    private const string _RejectedFieldSeparator = ", ";
    private const string _PaymentNotFoundTitle = "Payment not found";
    private const string _PaymentNotFoundDetail = "No payment was found for the supplied id.";
    private const string _AcquiringBankUnavailableTitle = "Acquiring bank is unavailable";
    private const string _AcquiringBankUnavailableDetail = "The acquiring bank is unavailable, please retry.";
    private const string _AcquiringBankTimeoutTitle = "Acquiring bank timed out";
    private const string _AcquiringBankTimeoutDetail = "The request to the acquiring bank timed out.";
    private const string _UnexpectedTitle = "An unexpected error occurred";
    private const string _UnexpectedDetail = "An unexpected error occurred while processing the request.";

    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = Describe(exception);

        LogOutcome(exception, problemDetails.Status);

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }

    private static ProblemDetails Describe(Exception exception) => exception switch
    {
        ValidationException validationException => PaymentRejection.Describe(DescribeFailures(validationException)),
        PaymentNotFoundException => Describe(
            StatusCodes.Status404NotFound,
            _PaymentNotFoundTitle,
            _PaymentNotFoundDetail),
        AcquiringBankUnavailableException => Describe(
            StatusCodes.Status502BadGateway,
            _AcquiringBankUnavailableTitle,
            _AcquiringBankUnavailableDetail),
        AcquiringBankTimeoutException or TaskCanceledException or TimeoutException => Describe(
            StatusCodes.Status504GatewayTimeout,
            _AcquiringBankTimeoutTitle,
            _AcquiringBankTimeoutDetail),
        _ => Describe(StatusCodes.Status500InternalServerError, _UnexpectedTitle, _UnexpectedDetail)
    };

    private static ProblemDetails Describe(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail
    };

    private static Dictionary<string, string[]> DescribeFailures(ValidationException exception) =>
        exception.Errors
            .GroupBy(failure => ToContractFieldName(failure.PropertyName))
            .ToDictionary(
                failures => failures.Key,
                failures => failures.Select(failure => failure.ErrorMessage).ToArray());

    private static string ToContractFieldName(string propertyName) =>
        JsonNamingPolicy.CamelCase.ConvertName(propertyName);

    private void LogOutcome(Exception exception, int? status)
    {
        if (exception is ValidationException validationException)
        {
            _logger.LogWarning(
                "Payment rejected on {RejectedFields}",
                DescribeRejectedFields(validationException));

            return;
        }

        if (status >= StatusCodes.Status500InternalServerError && exception is not AcquiringBankUnavailableException
            && exception is not AcquiringBankTimeoutException)
        {
            _logger.LogError(exception, "Request failed with an unhandled exception");

            return;
        }

        _logger.LogWarning("Request failed with status {StatusCode}", status);
    }

    private static string DescribeRejectedFields(ValidationException exception) =>
        string.Join(
            _RejectedFieldSeparator,
            exception.Errors
                .Select(failure => ToContractFieldName(failure.PropertyName))
                .Distinct());
}
