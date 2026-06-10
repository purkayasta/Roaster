# Phase 6 — Roaster.Consumer

## Pre-checklist

- [ ] Add `Roaster.Consumer` project to `Roaster.slnx` under solution folder `Backend`

---

## Overview

Worker Service that listens to RabbitMQ events via MassTransit and orchestrates the AI review pipeline. Contains no AI logic directly — it delegates all inference to `Roaster.Reviewer`. Writes all results back to the shared PostgreSQL database.

**SDK:** `Microsoft.NET.Sdk.Worker` (net10.0)

**NuGet packages:**
```xml
<PackageReference Include="MassTransit" Version="8.3.6" />
<PackageReference Include="MassTransit.RabbitMQ" Version="8.3.6" />
<PackageReference Include="Serilog.Extensions.Hosting" Version="8.0.0" />
<PackageReference Include="Serilog.Sinks.Console" Version="5.0.0" />
```

**Project references:** `Roaster.Data`, `Roaster.Contracts`, `Roaster.Reviewer`

---

## Folder Structure

```
src/Roaster.Consumer/
  Consumers/
    CodeSubmittedConsumer.cs
    UserThreadReplyConsumer.cs
  Extensions/
    ServiceCollectionExtensions.cs
  Program.cs
  appsettings.json
```

---

## Program.cs

```csharp
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

builder.Services.AddDbContext<RoasterDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("Ollama"));
builder.Services.AddSingleton<IAiReviewService, SemanticKernelAiReviewService>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<CodeSubmittedConsumer>();
    x.AddConsumer<UserThreadReplyConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Uri"]);

        cfg.ReceiveEndpoint("code-submitted", e =>
        {
            e.ConfigureConsumer<CodeSubmittedConsumer>(ctx);
            e.UseMessageRetry(r => r.Exponential(5,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(60),
                TimeSpan.FromSeconds(5)));
        });

        cfg.ReceiveEndpoint("user-thread-reply", e =>
        {
            e.ConfigureConsumer<UserThreadReplyConsumer>(ctx);
            e.UseMessageRetry(r => r.Intervals(
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)));
        });
    });
});

var host = builder.Build();
await host.RunAsync();
```

---

## Correlation ID

MassTransit automatically propagates `CorrelationId` through message headers. Each consumer reads it at the start and pushes it into Serilog `LogContext` so every log line from that consumer execution carries the same correlation ID as the originating HTTP request.

```csharp
using (LogContext.PushProperty("CorrelationId", context.CorrelationId ?? Guid.Empty))
{
    // all logging inside here is correlated
}
```

---

## CodeSubmittedConsumer (`Consumers/CodeSubmittedConsumer.cs`)

Triggered when `Roaster.Api` publishes a `CodeSubmittedEvent`.

### Full Step-by-Step Flow

1. Push `CorrelationId` into Serilog `LogContext`
2. Record `queuedAt = event.SubmittedAt`, `processingStartedAt = DateTime.UtcNow`
3. Load `Submission` by `SubmissionId` from DB
   - If not found: log warning and return (no retry — message is invalid)
4. Save `SubmissionEvent` (EventType = ProcessingStarted, CorrelationId)
5. Set `submission.Status = Processing`, call `SaveChangesAsync`
6. Start `Stopwatch`
7. Call `_ai.ReviewCodeAsync(submission.Payload.Content, submission.Payload.Language)`
8. Build `Review` entity:
   ```csharp
   var review = new Review
   {
       SubmissionId = submission.Id,
       Summary = aiResult.Summary,
       Comments = aiResult.LineComments.Select(c => new ReviewLineComment
       {
           LineNumber = c.LineNumber,
           Comment    = c.Comment,
           Severity   = (ReviewSeverity)c.Severity
       }).ToList(),
       CreatedAt = DateTime.UtcNow
   };
   ```
9. Save `Review` to DB
10. Set `submission.Status = Completed`, call `SaveChangesAsync`
11. Stop `Stopwatch`
12. Save `ReviewMetrics`:
    ```csharp
    new ReviewMetrics
    {
        SubmissionId     = submission.Id,
        ModelId          = _ollamaOptions.ModelId,
        QueueWaitMs      = (int)(processingStartedAt - queuedAt).TotalMilliseconds,
        ReviewDurationMs = (int)stopwatch.Elapsed.TotalMilliseconds,
        TotalDurationMs  = (int)(DateTime.UtcNow - queuedAt).TotalMilliseconds,
        CommentCount     = review.Comments.Count,
        CodeLengthChars  = submission.Payload.Content.Length,
        CreatedAt        = DateTime.UtcNow
    }
    ```
13. Save `SubmissionEvent` (EventType = ReviewCompleted, metadata: `{ "durationMs": total }`)
14. Publish `ReviewCompletedEvent`

### On Any Exception

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Review failed for submission {Id}", msg.SubmissionId);
    submission.Status = SubmissionStatus.Failed;
    await _db.SaveChangesAsync();
    _db.SubmissionEvents.Add(new SubmissionEvent
    {
        SubmissionId  = submission.Id,
        EventType     = SubmissionEventType.Failed,
        CorrelationId = context.CorrelationId ?? Guid.Empty,
        Metadata      = JsonSerializer.Serialize(new { error = ex.Message }),
        OccurredAt    = DateTime.UtcNow
    });
    await _db.SaveChangesAsync();
    throw; // re-throw → MassTransit retry policy activates
}
```

**Retry policy:** exponential backoff, 5 attempts, 2s → 60s. After exhaustion, message moves to `code-submitted-error` dead-letter queue.

---

## UserThreadReplyConsumer (`Consumers/UserThreadReplyConsumer.cs`)

Triggered when `Roaster.Api` publishes a `UserThreadReplyEvent`.

### Full Step-by-Step Flow

1. Push `CorrelationId` into Serilog `LogContext`
2. Load `CommentThread` by `ThreadId`, eagerly including:
   - `Thread.Review`
   - `Thread.Review.Submission`
   - `Thread.Replies` (all existing replies, ordered by `CreatedAt`)
   - If not found: log warning and return
3. Extract context:
   ```csharp
   var code            = thread.Review.Submission.Payload.Content;
   var originalComment = thread.Review.Comments
       .FirstOrDefault(c => c.LineNumber == thread.LineNumber)?.Comment;
   ```
4. Build conversation history:
   ```csharp
   var history = thread.Replies
       .OrderBy(r => r.CreatedAt)
       .Select(r => new AiChatMessage(r.IsAiReply ? "assistant" : "user", r.Content!))
       .ToList();
   ```
5. Call `_ai.ReplyToThreadAsync(code, originalComment, history, event.UserMessage)`

   > Note: `event.UserMessage` is not stored in the event — the consumer should read the most recent user reply from the DB instead, based on `ThreadId` and the fact that the user reply was already saved by the API before publishing the event. The API saves the `ThreadReply` (IsAiReply=false) before publishing, so the consumer simply passes the content of that last reply.

6. Save AI `ThreadReply`:
   ```csharp
   new ThreadReply
   {
       ThreadId   = thread.Id,
       Content    = aiReply,
       IsAiReply  = true,
       CreatedAt  = DateTime.UtcNow
   }
   ```
7. Save `SubmissionEvent` (EventType = AiReplied, CorrelationId)
8. Call `SaveChangesAsync`

**Retry policy:** fixed intervals — 3s, 10s, 30s. After exhaustion, message moves to `user-thread-reply-error` dead-letter queue.