using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BankOps.Api;

// FR-404 / BankOps_05_Backend_Design.md "HTTP API shape": "Return RFC-style problem details with
// safe `code`, user text, `correlationId`, retry guidance and field validation; never stack traces
// or connector credentials." This is the one place an unhandled exception becomes an HTTP response
// — everything else in the pipeline should throw, not try to format its own error body.
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;

        logger.LogError(exception,
            "Unhandled exception. CorrelationId={CorrelationId}", correlationId);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Detail = "Something went wrong while processing your request. " +
                     "Give the correlation ID below to support if this keeps happening.",
            Type = "https://bankops.internal/problems/unhandled-exception",
        };
        problemDetails.Extensions["code"] = "UNHANDLED_EXCEPTION";
        problemDetails.Extensions["correlationId"] = correlationId;
        problemDetails.Extensions["retryGuidance"] = "safe-to-retry-idempotent-requests-only";

        httpContext.Response.StatusCode = problemDetails.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
