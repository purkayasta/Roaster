# Phase 3 — Correlation IDs

Wire a `CorrelationId` end-to-end: from the HTTP request → event POCO → MassTransit message header → Consumer log context → `SubmissionEvents` DB row. No new tables or projects needed — this is purely additive middleware and plumbing on top of Phase 1 and 2.

---

## 3.A — API Middleware

**Project:** `Roaster.Api`

### New file: `Middleware/CorrelationIdMiddleware.cs`

Reads `X-Correlation-Id` from the incoming request header. Generates a new UUID if absent. Stamps it on the response header and pushes it into Serilog's `LogContext` so every log line from that request carries the correlation ID.

```csharp
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
```

### Update `Program.cs` — register middleware before routing:

```csharp
app.UseMiddleware<CorrelationIdMiddleware>();
```

---

## 3.B — Thread CorrelationId Through Events

**Project:** `Roaster.Api`

Replace the placeholder `Guid.NewGuid()` calls added in Phase 2 with the real correlation ID from `HttpContext`.

### Update `SubmissionEndpoints.cs`:

```csharp
var correlationId = (Guid)ctx.Items["CorrelationId"]!;

await publisher.Publish(new CodeSubmittedEvent(
    submission.Id,
    correlationId,
    submission.CreatedAt));
```

Also save it to `SubmissionEvents`:

```csharp
db.SubmissionEvents.Add(new SubmissionEvent
{
    SubmissionId  = submission.Id,
    EventType     = SubmissionEventType.Submitted,
    CorrelationId = correlationId,
    OccurredAt    = submission.CreatedAt
});
```

### Update `ThreadEndpoints.cs` — same pattern:

```csharp
var correlationId = (Guid)ctx.Items["CorrelationId"]!;

db.SubmissionEvents.Add(new SubmissionEvent
{
    SubmissionId  = submissionId,
    EventType     = SubmissionEventType.UserReplied,
    CorrelationId = correlationId,
    OccurredAt    = DateTime.UtcNow
});

await publisher.Publish(new UserThreadReplyEvent(
    thread.Id, submissionId, lineNumber, correlationId, DateTime.UtcNow));
```

---

## 3.C — Consumer LogContext

**Project:** `Roaster.Consumer`

MassTransit automatically propagates `CorrelationId` through message headers. Each consumer reads it at the start and pushes it into Serilog so all log lines from that execution are correlated.

### Update `CodeSubmittedConsumer.cs`:

```csharp
public async Task Consume(ConsumeContext<CodeSubmittedEvent> context)
{
    using (LogContext.PushProperty("CorrelationId", context.Message.CorrelationId))
    using (LogContext.PushProperty("SubmissionId", context.Message.SubmissionId))
    {
        // existing consumer logic — all logs here carry CorrelationId
    }
}
```

### Update `UserThreadReplyConsumer.cs` — same pattern.

---

## How CorrelationId Flows End-to-End

```
HTTP Request
  X-Correlation-Id header (or generated)
       │
       ▼
CorrelationIdMiddleware
  → ctx.Items["CorrelationId"]
  → Serilog LogContext (all API logs)
       │
       ▼
SubmissionEvents table row
  CorrelationId column
       │
       ▼
CodeSubmittedEvent POCO
  CorrelationId field
       │
       ▼
MassTransit message header
       │
       ▼
Consumer LogContext
  → Serilog LogContext (all Consumer logs)
       │
       ▼
SubmissionEvents rows written by Consumer
  CorrelationId column
```

This means a single HTTP request can be traced end-to-end across API logs, Consumer logs, and database rows using the same `CorrelationId` value.