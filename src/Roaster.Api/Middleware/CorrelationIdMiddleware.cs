using Serilog.Context;

namespace Roaster.Api.Middleware;

public class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext ctx)
    {
        if (!ctx.Request.Headers.TryGetValue("X-Correlation-Id", out var existing)
            || !Guid.TryParse(existing, out var id))
            id = Guid.NewGuid();

        ctx.Items["CorrelationId"] = id;
        ctx.Response.Headers["X-Correlation-Id"] = id.ToString();

        using (LogContext.PushProperty("CorrelationId", id))
            await next(ctx);
    }
}
