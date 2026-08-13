using Serilog.Context;

namespace QuotesApi.Middleware;

// Pushes the per-request trace identifier into Serilog's LogContext so every log
// line written while handling this request - including ones from ExceptionMiddleware -
// is enriched with a TraceId property. Must run before ExceptionMiddleware so exception
// logs carry the id too.
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Trace-Id"] = context.TraceIdentifier;

        using (LogContext.PushProperty("TraceId", context.TraceIdentifier))
        {
            await _next(context);
        }
    }
}
