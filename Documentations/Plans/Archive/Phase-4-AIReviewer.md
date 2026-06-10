# Phase 4 — AI Code Reviewer

Add the actual AI review capability. Introduces `Roaster.Reviewer` (class library with Semantic Kernel + Ollama), `ReviewMetrics` for performance tracking, and wires the Consumer stubs from Phase 2 into real implementations.

---

## 4.A — ReviewMetrics Model

**Project:** `Roaster.Data` (additive changes)

Add a dedicated table for structured performance data — written once per completed review. Keeps aggregate queries simple (no JSONB parsing needed).

### New model: `ReviewMetrics.cs`

```csharp
public class ReviewMetrics
{
    public long Id { get; init; }
    public long SubmissionId { get; init; }
    public string? ModelId { get; init; }           // e.g. "deepseek-coder:6.7b"
    public int QueueWaitMs { get; init; }           // Submitted → ProcessingStarted
    public int ReviewDurationMs { get; init; }      // ProcessingStarted → ReviewCompleted
    public int TotalDurationMs { get; init; }       // Submitted → ReviewCompleted
    public int CommentCount { get; init; }
    public int CodeLengthChars { get; init; }
    public DateTime CreatedAt { get; init; }

    public Submission Submission { get; set; } = null!;
}
```

### New: `ReviewMetricsConfiguration.cs`

```csharp
public class ReviewMetricsConfiguration : IEntityTypeConfiguration<ReviewMetrics>
{
    public void Configure(EntityTypeBuilder<ReviewMetrics> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).UseIdentityByDefaultColumn();
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.ToTable("ReviewMetrics");
    }
}
```

### Update `Submission.cs` — add Metrics nav property

```csharp
public ReviewMetrics? Metrics { get; set; }
```

### Update `SubmissionConfiguration.cs` — wire the nav property

```csharp
builder.HasOne(s => s.Metrics)
       .WithOne(m => m.Submission)
       .HasForeignKey<ReviewMetrics>(m => m.SubmissionId)
       .OnDelete(DeleteBehavior.Cascade);
```

### Update `RoasterDbContext.cs` — add DbSet

```csharp
public DbSet<ReviewMetrics> ReviewMetrics => Set<ReviewMetrics>();
```

---

## 4.B — Reviewer Class Library

**Project:** `Roaster.Reviewer` (new project)

**Pre-checklist:**
- [ ] Add `Roaster.Reviewer` to `Roaster.slnx` under solution folder `Backend`

**SDK:** `Microsoft.NET.Sdk` (net10.0, class library)

**NuGet:**
```xml
<PackageReference Include="Microsoft.SemanticKernel" Version="1.22.0" />
<PackageReference Include="Microsoft.SemanticKernel.Connectors.Ollama" Version="1.22.0-alpha" />
```

> **Stable fallback:** `Microsoft.SemanticKernel.Connectors.OpenAI` pointed at `http://ollama:11434/v1`. The whole service is behind `IAiReviewService` — swapping is one constructor line change.

**Project references:** none (pure library)

**Folder structure:**
```
src/Roaster.Reviewer/
  Abstractions/
    IAiReviewService.cs
    AiReviewResult.cs
    AiLineComment.cs
    AiChatMessage.cs
  Options/
    OllamaOptions.cs
  Services/
    SemanticKernelAiReviewService.cs
```

### IAiReviewService.cs

```csharp
public interface IAiReviewService
{
    Task<AiReviewResult> ReviewCodeAsync(
        string code,
        string language,
        CancellationToken ct = default);

    Task<string> ReplyToThreadAsync(
        string code,
        string originalComment,
        IList<AiChatMessage> history,
        string userMessage,
        CancellationToken ct = default);
}

public record AiReviewResult(string? Summary, List<AiLineComment> LineComments);

// Severity as int to match ReviewSeverity enum: 10=Gentle, 20=Medium, 30=Brutal
public record AiLineComment(int LineNumber, string? Comment, int Severity);

public record AiChatMessage(string Role, string Content); // "user" | "assistant"
```

### OllamaOptions.cs

```csharp
public class OllamaOptions
{
    public required string Endpoint { get; init; }  // e.g. "http://ollama:11434"
    public required string ModelId  { get; init; }  // e.g. "deepseek-coder:6.7b"
}
```

Recommended models:
- `deepseek-coder:6.7b` — best JSON compliance, ~6GB VRAM (q4 quantized)
- `codellama:13b` — broader language support, ~8GB VRAM (q4 quantized)

### SemanticKernelAiReviewService.cs

**System prompt — code review** (returns strict JSON only):

```
You are an expert code reviewer. Return ONLY a valid JSON object with no markdown, no prose outside the JSON:
{
  "summary": "1-2 sentence overall assessment.",
  "comments": [
    { "lineNumber": 5, "comment": "Explanation of the issue.", "severity": 20 }
  ]
}
Severity values: 10 = Gentle (style), 20 = Medium (correctness/performance), 30 = Brutal (security/data loss).
If there are no issues, return an empty comments array.
```

**System prompt — thread reply:**

```
You are an expert code reviewer continuing a conversation with a developer.
You have the original code, your original comment, and the conversation history.
Reply concisely in plain text. No JSON. No markdown headers.
Build on the conversation — do not repeat your original comment verbatim.
```

**JSON parsing fallback chain** (`ParseReviewResponse`):
1. Direct `JsonDocument.Parse(raw)`
2. Strip ` ```json ... ``` ` markdown fence via regex, retry
3. Extract first `{` to last `}`, retry
4. On `JsonException`: log truncated raw response, return `new AiReviewResult(null, [])` — graceful empty result, no retry triggered

---

## 4.C — Wire AI into Consumer

**Project:** `Roaster.Consumer` (additive changes)

### New NuGet reference to `Roaster.Reviewer`:

```xml
<ProjectReference Include="..\Roaster.Reviewer\Roaster.Reviewer.csproj" />
```

### Update `Program.cs` — register AI service:

```csharp
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("Ollama"));
builder.Services.AddSingleton<IAiReviewService, SemanticKernelAiReviewService>();
```

### Replace stub in `CodeSubmittedConsumer.cs` with full implementation:

```csharp
public async Task Consume(ConsumeContext<CodeSubmittedEvent> context)
{
    using (LogContext.PushProperty("CorrelationId", context.Message.CorrelationId))
    using (LogContext.PushProperty("SubmissionId", context.Message.SubmissionId))
    {
        var msg = context.Message;

        var submission = await db.Submissions.FindAsync(msg.SubmissionId);
        if (submission is null) { logger.LogWarning("Submission {Id} not found", msg.SubmissionId); return; }

        var queuedAt           = msg.SubmittedAt;
        var processingStartedAt = DateTime.UtcNow;

        db.SubmissionEvents.Add(new SubmissionEvent
        {
            SubmissionId = submission.Id, EventType = SubmissionEventType.ProcessingStarted,
            CorrelationId = msg.CorrelationId, OccurredAt = processingStartedAt
        });
        submission.Status = SubmissionStatus.Processing;
        await db.SaveChangesAsync();

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var aiResult = await ai.ReviewCodeAsync(
                submission.Payload.Content, submission.Payload.Language);

            var review = new Review
            {
                SubmissionId = submission.Id,
                Summary      = aiResult.Summary,
                Comments     = aiResult.LineComments.Select(c => new ReviewLineComment
                {
                    LineNumber = c.LineNumber,
                    Comment    = c.Comment,
                    Severity   = (ReviewSeverity)c.Severity
                }).ToList(),
                CreatedAt = DateTime.UtcNow
            };

            db.Reviews.Add(review);
            submission.Status = SubmissionStatus.Completed;
            await db.SaveChangesAsync();

            stopwatch.Stop();

            db.ReviewMetrics.Add(new ReviewMetrics
            {
                SubmissionId     = submission.Id,
                ModelId          = ollamaOptions.Value.ModelId,
                QueueWaitMs      = (int)(processingStartedAt - queuedAt).TotalMilliseconds,
                ReviewDurationMs = (int)stopwatch.Elapsed.TotalMilliseconds,
                TotalDurationMs  = (int)(DateTime.UtcNow - queuedAt).TotalMilliseconds,
                CommentCount     = review.Comments.Count,
                CodeLengthChars  = submission.Payload.Content.Length,
                CreatedAt        = DateTime.UtcNow
            });

            db.SubmissionEvents.Add(new SubmissionEvent
            {
                SubmissionId  = submission.Id,
                EventType     = SubmissionEventType.ReviewCompleted,
                CorrelationId = msg.CorrelationId,
                OccurredAt    = DateTime.UtcNow
            });

            await db.SaveChangesAsync();

            await publisher.Publish(new ReviewCompletedEvent(
                submission.Id, review.Id, msg.CorrelationId, DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Review failed for submission {Id}", msg.SubmissionId);
            submission.Status = SubmissionStatus.Failed;
            db.SubmissionEvents.Add(new SubmissionEvent
            {
                SubmissionId  = submission.Id,
                EventType     = SubmissionEventType.Failed,
                CorrelationId = msg.CorrelationId,
                Metadata      = JsonSerializer.Serialize(new { error = ex.Message }),
                OccurredAt    = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            throw; // re-throw → MassTransit retry
        }
    }
}
```

### Replace stub in `UserThreadReplyConsumer.cs` with full implementation:

1. Load thread with `Review → Submission` chain + all `Replies`
2. Extract `code = thread.Review.Submission.Payload.Content`
3. Find original comment from JSONB: `thread.Review.Comments.FirstOrDefault(c => c.LineNumber == thread.LineNumber)`
4. Build history from existing replies ordered by `CreatedAt`
5. Get the latest user reply content from DB (the API saved it before publishing the event)
6. Call `ai.ReplyToThreadAsync(code, originalComment, history, userReplyContent)`
7. Save `ThreadReply` (IsAiReply = true)
8. Record `SubmissionEvent` (AiReplied)