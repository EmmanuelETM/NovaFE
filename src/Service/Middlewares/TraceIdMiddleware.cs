using Serilog.Context;

namespace NovaFE.Service.Middlewares;

public class TraceIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Trace-Id";
    private const string ItemsKey = "TraceId";

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Use incoming header, or generate a fresh Guid
        if (!context.Request.Headers.TryGetValue(HeaderName, out var traceIdValues)
            || string.IsNullOrWhiteSpace(traceIdValues))
        {
            traceIdValues = Guid.NewGuid().ToString();
            context.Request.Headers.Append(HeaderName, traceIdValues);
        }

        var traceId = traceIdValues.ToString();

        context.TraceIdentifier = traceId;
        context.Items[ItemsKey] = traceId;

        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(HeaderName))
                context.Response.Headers.Append(HeaderName, traceId);
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(ItemsKey, traceId))
        {
            await next(context);
        }
    }
}