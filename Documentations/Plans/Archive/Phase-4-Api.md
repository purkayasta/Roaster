# Phase 4 — Roaster.Api

## Pre-checklist

- [ ] Add `Roaster.Api` project to `Roaster.slnx` under solution folder `Backend`

---

## Overview

The public-facing Minimal API. Accepts code submissions, exposes review results, and handles user thread replies. Publishes events to RabbitMQ via MassTransit. No SignalR — the user refreshes the page to pick up status changes.

**SDK:** `Microsoft.NET.Sdk.Web` (net10.0)

**NuGet packages:**
```xml
<PackageReference Include="MassTransit" Version="8.3.6" />
<PackageReference Include="MassTransit.RabbitMQ" Version="8.3.6" />
<PackageReference Include="Serilog.AspNetCore" Version="8.0.3" />
<PackageReference Include="Serilog.Sinks.Console" Version="5.0.0" />
<PackageReference Include="Scalar.AspNetCore" Version="2.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.NpgSql" Version="8.0.2" />
<PackageReference Include="AspNetCore.HealthChecks.Rabbitmq" Version="8.0.2" />
```

**Project references:** `Roaster.Data`, `Roaster.Contracts`

---

## Folder Structure

```
src/Roaster.Api/
  Endpoints/
    SubmissionEndpoints.cs
    ReviewEndpoints.cs
    ThreadEndpoints.cs
  Middleware/
    CorrelationIdMiddleware.cs
  Models/
    SubmissionRequest.cs
    ReplyRequest.cs
  Program.cs
  appsettings.json
  appsettings.Development.json
```

---

## Program.cs

```csharp
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

builder.Services.AddDbContext<RoasterDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddMassTransit(x =>
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Uri"]);
        cfg.ConfigureEndpoints(ctx);
    }));

builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(p =>
        p.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>()!)
         .AllowAnyHeader()
         .AllowAnyMethod()));

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Postgres")!)
    .AddRabbitMQ(rabbitConnectionString: builder.Configuration["RabbitMQ:Uri"]!);

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseCors();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
    app.MapScalarApiReference();  // Scalar API docs — dev only

app.MapOpenApi();
app.MapSubmissionEndpoints();
app.MapReviewEndpoints();
app.MapThreadEndpoints();
app.MapHealthChecks("/health");

await app.RunAsync();
```

---

## Correlation ID Middleware (`Middleware/CorrelationIdMiddleware.cs`)

Reads `X-Correlation-Id` from the incoming request header. Generates a new UUID if absent. Stamps it on the response header and pushes it into Serilog's `LogContext` for the duration of the request so every log line from that request carries the correlation ID.

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

---

## Request Models (`Models/`)

Validation is declared via data annotations. Applied to the endpoint group — no inline `if` guards in the handler.

### SubmissionRequest.cs

```csharp
public class SubmissionRequest
{
    [Required]
    [StringLength(50000, MinimumLength = 1, ErrorMessage = "Code must be between 1 and 50,000 characters.")]
    public string Code { get; init; } = string.Empty;

    [Required]
    [AllowedValues("csharp", "javascript", "typescript", "python",
                   "java", "go", "rust", "cpp", "sql",
                   ErrorMessage = "Unsupported language.")]
    public string Language { get; init; } = string.Empty;
}
```

### ReplyRequest.cs

```csharp
public class ReplyRequest
{
    [Required]
    [StringLength(5000, MinimumLength = 1)]
    public string Content { get; init; } = string.Empty;
}
```

---

## Endpoints

Each file in `Endpoints/` is a static class with a `MapXxxEndpoints(this IEndpointRouteBuilder app)` extension method. All route definitions and handler logic live in that single file.

---

### SubmissionEndpoints.cs

#### POST /api/submissions

**Flow:**
1. Validate request via annotations — returns `400` on failure
2. Sanitize code: `Regex.Replace(code, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "")`
3. Save `Submission` with `Payload = new CodePayload { Content = sanitized, Language = req.Language }`, `Status = Pending`
4. Record `SubmissionEvent` (EventType = Submitted, CorrelationId from `ctx.Items["CorrelationId"]`)
5. Publish `CodeSubmittedEvent { SubmissionId, CorrelationId, SubmittedAt = submission.CreatedAt }`
6. Return `202 Accepted`, body = `SubmissionDto`, `Location: /api/submissions/{id}`

**Request body:**
```json
{ "code": "string", "language": "csharp" }
```

**Response `202`:**
```json
{
  "id": 1,
  "language": "csharp",
  "status": "Pending",
  "createdAt": "...",
  "updatedAt": "..."
}
```

#### GET /api/submissions/{id}

- Returns `SubmissionDto` — `Payload` (code content) is intentionally excluded to keep polling lightweight
- `404` if not found

#### GET /api/submissions

- Returns paged `SubmissionDto[]` ordered by `CreatedAt DESC`
- Query params: `?page=1&pageSize=20`

---

### ReviewEndpoints.cs

#### GET /api/submissions/{id}/review

- `404` if submission not found
- `204 No Content` if `Status ≠ Completed` — client retries on next refresh
- Load `Review` with `Threads` (to resolve `ThreadId` per line number)
- Map `Review.Comments` (JSONB list) → `List<ReviewCommentDto>`, joining thread IDs by `LineNumber`
- Return `200 ReviewDto`

---

### ThreadEndpoints.cs

#### POST /api/submissions/{submissionId}/reviews/{reviewId}/lines/{lineNumber}/threads

**Flow:**
1. Validate `reviewId` belongs to `submissionId`
2. Validate `lineNumber` exists in `Review.Comments` JSONB
3. Upsert `CommentThread (ReviewId, LineNumber)` — unique constraint on `(ReviewId, LineNumber)` handles concurrent creates
4. Save user `ThreadReply` (IsAiReply = false)
5. Record `SubmissionEvent` (EventType = UserReplied)
6. Publish `UserThreadReplyEvent`
7. Return `202 Accepted` with `{ "threadId": ..., "replyId": ... }`

#### GET /api/threads/{threadId}/replies

- Returns `List<ThreadReplyDto>` ordered by `CreatedAt ASC`
- `404` if thread not found

---

## API Endpoint Summary

| Method | Path | Response |
|--------|------|----------|
| POST | `/api/submissions` | `202 SubmissionDto` |
| GET | `/api/submissions/{id}` | `200 SubmissionDto` / `404` |
| GET | `/api/submissions` | `200 SubmissionDto[]` paged |
| GET | `/api/submissions/{id}/review` | `200 ReviewDto` / `204` / `404` |
| POST | `/api/submissions/{id}/reviews/{reviewId}/lines/{line}/threads` | `202 { threadId, replyId }` |
| GET | `/api/threads/{threadId}/replies` | `200 ThreadReplyDto[]` / `404` |
| GET | `/health` | health check JSON |
| GET | `/scalar/v1` | Scalar API docs (dev only) |