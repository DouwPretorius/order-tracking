using Microsoft.AspNetCore.Diagnostics;
using stock_api.Contracts;
using stock_api.Services;

namespace stock_api.Infrastructure;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, message) = exception switch
        {
            DuplicateOrderSubmissionException duplicate =>
                (StatusCodes.Status409Conflict, duplicate.Message),
            _ =>
                (StatusCodes.Status500InternalServerError,
                    "An unexpected error occurred while processing the request."),
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Unhandled exception while processing {Method} {Path}.",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation(
                "Order submission rejected because it duplicates order #{OriginalOrderId}.",
                ((DuplicateOrderSubmissionException)exception).OriginalOrderId);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            new ApiErrorResponse(message),
            cancellationToken);
        return true;
    }
}
