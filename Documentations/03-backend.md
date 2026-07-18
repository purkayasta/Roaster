# 3. Backend

## Projects

| Project | SDK | Role |
|---|---|---|
| **Roaster.Api** | Web | HTTP endpoints, service layer, validation, publishes events |
| **Roaster.Consumer** | Worker | Consumes RabbitMQ messages, orchestrates review flow |
| **Roaster.Reviewer** | Class Library | `IAiReviewService` — Semantic Kernel + Ollama connector |
| **Roaster.Data** | Class Library | EF Core `RoasterDbContext`, entity configs |
| **Roaster.Contracts** | Class Library | Shared events/DTOs, no external dependencies |
| **Roaster.Migration** | Console App | DbUp; runs `Scripts/Tables/*.sql` then `Scripts/Indexes/*.sql` |

All target **.NET 10**.

## API Endpoints

| Method | Path | Returns | Notes |
|---|---|---|---|
| POST | `/api/sessions` | 201 `SessionDto` | Create session |
| GET | `/api/sessions` | 200 `SessionSubmissionDto[]` | `page`, `pageSize` query params |
| GET | `/api/sessions/{id}` | 200 `SessionDetailDto` | Session + latest submission |
| DELETE | `/api/sessions/{id}` | 204 | Soft delete (`IsActive=false`) |
| POST | `/api/submissions` | 202 `SubmissionDto` | Submit/resubmit code |
| GET | `/api/submissions/{id}` | 200 `SubmissionDto` | Single submission |
| GET | `/api/submissions/{id}/review` | 204 / 200 `ReviewDto` | Poll until completed |
| POST | `/api/submissions/{subId}/reviews/{revId}/lines/{lineNum}/threads` | 202 `ThreadCreatedDto` | Reply — creates thread if needed |
| PATCH | `/api/threads/{threadId}/resolve` | 204 | Mark thread resolved |
| GET | `/api/threads/{threadId}/replies` | 200 `ThreadReplyDto[]` | Ordered conversation |
| GET | `/health` | 200 / 503 | Postgres + RabbitMQ health |

OpenAPI/Scalar UI: `/scalar/v1` (development only).

## Submission Flow (detail)

```
POST /api/submissions
  → CorrelationIdMiddleware assigns X-Correlation-Id
  → Validate (sessionId, code 1–50k chars, language enum), strip control chars
  → No existing submission  → create (Status=Submitted)
  → Existing + Processing   → 409 Conflict
  → Existing + other status → replace Payload, reset Status=Submitted, drop old Review
  → Log Event(Submitted), publish SubmissionCreatedEvent → 202 Accepted

RabbitMQ "code-submitted" (PrefetchCount=1, ConcurrentMessageLimit=1)
  → CodeSubmittedConsumer: Status → Processing → Event(ProcessingStarted)
  → IAiReviewService.ReviewCodeAsync()
  → Success: save Review + ReviewMetrics + Event(ReviewCompleted), Status → Completed, publish ReviewCompletedEvent
  → Parse failure: Status → Failed, Event(Failed) with real parser error in Metadata (no rethrow)
  → Exception: Status → Failed, Event(Failed) with ex.Message in Metadata, rethrow → MassTransit retry
```

## Data Model

| Entity | PK | Key Columns | Notes |
|---|---|---|---|
| `Session` | UUID | `Name`, `CreatedAt`, `IsActive` | Soft delete; 1:1 with live `Submission` |
| `Submission` | bigserial | `SessionId` (unique FK), `Payloads JSONB[]`, `Status` | One row can hold multiple files |
| `Review` | bigserial | `SubmissionId` FK, `Summary`, `Comments JSONB` | Written by the Consumer |
| `CommentThread` | bigserial | `ReviewId` FK, `LineNumber`, `Status` | Unique (ReviewId, LineNumber) |
| `ThreadReply` | bigserial | `ThreadId` FK, `Content`, `IsAiReply` | User + AI messages |
| `Event` | bigserial | `SubmissionId`, `SessionId`, `EventType`, `CorrelationId`, `Metadata JSONB` | Audit log — viewable in the UI Events page |
| `ReviewMetrics` | bigserial | `SubmissionId`, `ModelId`, timing/count ints | Latency + token telemetry |

Enum values: see [06-data-model.md](06-data-model.md).

## Key Patterns

- **Service layer.** One service per resource under `Roaster.Api/Services/`. Services return
  `ServiceResult<T>` (implements `IResult`); endpoints are thin — `return await service.XxxAsync(...)`.
- **JSONB owned types.** `Submission.Payloads`, `Review.Comments`, `Event.Metadata` are all JSONB.
- **MassTransit retry.** `CodeSubmittedConsumer`: exponential (3x, 10s→120s). `UserThreadReplyConsumer`:
  fixed [3s, 10s, 30s]. Consumers rethrow on AI failure so retries actually fire.
- **AI JSON parsing never throws.** `ReviewResponseParser` degrades through 4 fallback stages
  (direct parse → markdown-fence strip → brace-substring extract → control-char repair) before
  giving up. On total failure the real parser error (JSON exception + raw response snippet, or a
  context-length-exceeded hint) is threaded into the `Failed` event's `Metadata` — visible in the
  Events page, not a generic placeholder message.

## Configuration

### Roaster.Api
```json
{
  "ConnectionStrings": { "Postgres": "Host=localhost;Database=roaster;Username=postgres;Password=postgres" },
  "RabbitMQ": { "Uri": "rabbitmq://guest:guest@localhost" },
  "Cors": { "Origins": ["http://localhost:5094", "https://localhost:7052"] }
}
```

### Roaster.Consumer
```json
{
  "ConnectionStrings": { "Postgres": "..." },
  "RabbitMQ": { "Uri": "..." },
  "Ollama": { "Endpoint": "http://localhost:11434", "ModelId": "qwen2.5-coder:3b", "ContextLength": 8096 }
}
```

`Ollama:ContextLength` maps to Ollama's `num_ctx` — the token budget shared by the prompt and the
response for a single AI call. Overridable via `OLLAMA_CONTEXT_LENGTH` env var in Docker Compose.

## Test Conventions

xUnit 2.9.3 + Moq 4.20.72 + FluentAssertions 6.12.1, no real broker or database.

| Project | Approach |
|---|---|
| `Roaster.Api.Tests` | `WebApplicationFactory<Program>`, EF InMemory, mocked `IPublishEndpoint` |
| `Roaster.Consumer.Tests` | Per-test InMemory DbContext, `FakeConsumeContext`, mocked `IAiReviewService`/`IPublishEndpoint` |
| `Roaster.Reviewer.Tests` | `ReviewResponseParser` fallback stages, chat-history building |
| `Roaster.Data.Tests` | EF configs — FK cascades, owned-type mapping, enum conversions |
