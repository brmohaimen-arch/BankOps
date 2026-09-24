using System.Diagnostics;

namespace BankOps.Api;

// FR-404: "Provide one correlation ID for user-visible failures... operator can give support the
// ID and engineers can locate corresponding trace/log." We piggyback on the W3C trace context
// Activity.Current already creates for every request (ASP.NET Core's default trace ID format is
// W3C) rather than inventing a second, parallel ID scheme — one ID, not two systems to reconcile.
public class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("BankOps.Api.Correlation")
            .BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }
}
