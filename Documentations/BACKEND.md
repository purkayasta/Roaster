# Backend Architecture

## Projects

| Project | SDK | Role |
|---|---|---|
| **Roaster.Api** | Web | HTTP endpoints, validation, middleware, publishes events |
| **Roaster.Consumer** | Worker | Consumes RabbitMQ messages, orchestrates review flow |
| **Roaster.Reviewer** | Class Library | `IAiReviewService` — Semantic Kernel 1.74.0 + Ollama connector (alpha) |
| **Roaster.Data** | Class Library | EF Core `RoasterDbContext`, entity configs, no migrations |
| **Roaster.Contracts** | Class Library | Shared events and DTOs; no external dependencies |
| **Roaster.Migration** | Console App | DbUp 7.0.1; runs `Scripts/Tables/*.sql` then `Scripts/Indexes/*.sql` |

All projects target **.NET 10**.

---

## End-to-End Data Flow

```
POST /api/sessions
  → Validates CreateSessionRequest (name: 1–255 chars)
  → Saves Session to Postgres (IsActive = true)
  → Returns 201 Created with SessionDto

GET /api/sessions
  → Queries Sessions (IsActive = true) with LEFT JOIN on Submission
  → Returns paginated SessionSubmissionDto list (sessions without submissions have null Language/Status)
  → Query params: page, pageSize

GET /api/sessions/{id}
  → Returns SessionDetailDto (name, code content, language, status)
  → 404 if not found or soft-deleted

DELETE /api/sessions/{id}
  → Soft delete: sets IsActive = false
  → 204 No Content; 404 if not found

POST /api/submissions
  → CorrelationIdMiddleware (generates/extracts X-Correlation-Id UUID into Serilog + response header)
  → Validates SubmissionRequest (sessionId: Guid, code: 1–50k chars, language enum)
  → Looks up Session by sessionId → 404 if not found
  → Strips control characters from code
  → If no existing Submission for session → creates new (Status=Submitted)
  → If existing Submission with Status=Processing → 409 Conflict
  → If existing Submission (non-Processing) → updates Payload, resets Status=Submitted, removes old Review
  → Logs Event(Submitted) with correlationId = session.Id
  → Publishes CodeSubmittedEvent to RabbitMQ
  → Returns 202 Accepted

GET /api/submissions/{id}
  → Returns single SubmissionDto; 404 if not found

RabbitMQ: code-submitted queue (PrefetchCount=1, ConcurrentMessageLimit=1)
  → CodeSubmittedConsumer
  → Status: Submitted → Processing; logs Event(ProcessingStarted)
  → Calls IAiReviewService.ReviewCodeAsync()
  → Saves Review (Summary + Comments JSONB) + ReviewMetrics + Event(ReviewCompleted)
  → Status: Processing → Completed
  → Publishes ReviewCompletedEvent
  → On failure: Status → Failed, logs Event(Failed), rethrows (triggers MassTransit retry)

GET /api/submissions/{id}/review
  → Returns 204 while Status ≠ Completed; 200 with ReviewDto when done
  → Frontend polls this endpoint

POST /api/submissions/{subId}/reviews/{revId}/lines/{lineNum}/threads
  → Creates CommentThread + ThreadReply (IsAiReply=false)
  → Logs Event(UserReplied)
  → Publishes UserThreadReplyEvent

RabbitMQ: user-thread-reply queue
  → UserThreadReplyConsumer builds full chat history from prior replies
  → Calls IAiReviewService.ReplyToThreadAsync()
  → Saves AI ThreadReply (IsAiReply=true)
  → Logs Event(AiReplied)

GET /api/threads/{threadId}/replies
  → Returns ordered conversation (user/AI alternating)
```

---

## API Endpoints Summary

| Method | Path | Returns | Notes |
|---|---|---|---|
| POST | `/api/sessions` | 201 `SessionDto` | Create session |
| GET | `/api/sessions` | 200 `SessionSubmissionDto[]` | `page`, `pageSize` query params |
| GET | `/api/sessions/{id}` | 200 `SessionDetailDto` | Session + submission detail |
| DELETE | `/api/sessions/{id}` | 204 | Soft delete (sets IsActive=false) |
| POST | `/api/submissions` | 202 `SubmissionDto` | Submit/resubmit code |
| GET | `/api/submissions/{id}` | 200 `SubmissionDto` | Single submission |
| GET | `/api/submissions/{id}/review` | 204 / 200 `ReviewDto` | Poll until completed |
| POST | `/api/submissions/{subId}/reviews/{revId}/lines/{lineNum}/threads` | 201 | User reply |
| GET | `/api/threads/{threadId}/replies` | 200 `ThreadReplyDto[]` | Conversation |
| GET | `/health` | 200 / 503 | Postgres + RabbitMQ health |

OpenAPI/Scalar UI: `/scalar/v1` (development only).

---

## Data Model

### Entities

| Entity | PK | Key Columns | Notes |
|---|---|---|---|
| `Session` | UUID | `Name varchar(255)`, `CreatedAt`, `IsActive bool` | Soft delete via `IsActive`; one session → one submission |
| `Submission` | bigserial | `SessionId UUID (unique FK)`, `Payload JSONB`, `Status smallint` | Unique on SessionId |
| `Review` | bigserial | `SubmissionId FK`, `Summary text`, `Comments JSONB` | Created by consumer |
| `CommentThread` | bigserial | `ReviewId FK`, `LineNumber int` | Unique (ReviewId, LineNumber) |
| `ThreadReply` | bigserial | `ThreadId FK`, `Content text`, `IsAiReply bool` | User and AI messages |
| `Event` | bigserial | `SubmissionId FK`, `SessionId UUID`, `EventType smallint`, `CorrelationId UUID`, `Metadata JSONB` | Audit log |
| `SubmissionHistory` | bigserial | `SubmissionId FK`, `SessionId FK`, `Payload JSONB`, `ChangedAt` | Auto-captured on payload change |
| `ReviewMetrics` | bigserial | `SubmissionId FK`, `ModelId varchar(100)`, timing/count ints | Performance data |

### Enum Values

**SubmissionStatus** (stored as smallint):
- 10 = Submitted, 20 = Processing, 30 = Completed, 40 = Failed

**ReviewSeverity** (stored as smallint):
- 10 = Gentle, 20 = Medium, 30 = Brutal

**EventType** (stored as smallint):
- 10 = Submitted, 20 = ProcessingStarted, 30 = ReviewCompleted, 40 = Failed, 50 = UserReplied, 60 = AiReplied

---

## Key Patterns

**JSONB owned types.**
- `Submission.Payload` → `CodePayload { Content, Language }` — single JSONB column
- `Review.Comments` → `List<ReviewLineComment>` — JSONB array
- `Event.Metadata` — JSONB for arbitrary event context

**Session soft delete.** `Session.IsActive` defaults to `true`. `DELETE /api/sessions/{id}` sets it `false`. All `GET /api/sessions` queries filter `IsActive = true`.

**Session ↔ Submission (1:1).** `SessionId` on `Submission` has a unique constraint. Each session has at most one live submission. Resubmitting replaces the payload and deletes the existing `Review` (if any); the old payload is preserved in `SubmissionHistory`.

**Dashboard query direction.** `GET /api/sessions` queries from `Sessions` and LEFT JOINs `Submission`, so sessions without any submission appear in the list with null Language/Status/SubmissionId.

**SubmissionHistory auto-capture.** `RoasterDbContext.SaveChangesAsync` detects modified `Submission.Payload` and writes a `SubmissionHistory` row automatically, without callers needing to do it.

**CorrelationId in Events = SessionId.** The `CorrelationId` stored in `Event` rows is the Session's UUID, not the HTTP-header UUID. The `CorrelationIdMiddleware` UUID only flows into Serilog context and the `X-Correlation-Id` response header.

**MassTransit retry config.**
- `CodeSubmittedConsumer`: `Exponential(3, start=10 s, max=120 s, increment=10 s)`.
- `UserThreadReplyConsumer`: fixed intervals [3 s, 10 s, 30 s].
- Consumers explicitly rethrow on AI failure so MassTransit retries fire.

**AI JSON parsing fallback.** `ReviewResponseParser` tries: direct `JsonSerializer.Deserialize` → markdown code-block extraction → substring search. Returns an empty `AiReviewResult` on total failure (no exception propagated).

---

## Configuration

### Roaster.Api (`appsettings.json`)
```json
{
  "ConnectionStrings": { "Postgres": "Host=localhost;Database=roaster;Username=postgres;Password=postgres" },
  "RabbitMQ": { "Uri": "rabbitmq://guest:guest@localhost" },
  "Cors": { "Origins": ["http://localhost:5094", "https://localhost:7052"] }
}
```

### Roaster.Consumer (`appsettings.json`)
```json
{
  "ConnectionStrings": { "Postgres": "..." },
  "RabbitMQ": { "Uri": "..." },
  "Ollama": { "Endpoint": "http://localhost:11434", "ModelId": "phi4-mini:latest" }
}
```

---

## Test Conventions

All test projects use **xUnit 2.9.3 + Moq 4.20.72 + FluentAssertions 6.12.1**.

- `Roaster.Api.Tests` — `WebApplicationFactory<Program>` (`ApiFactory`); swaps DbContext to EF InMemory, mocks `IPublishEndpoint`. InMemory root is shared across scopes so HTTP request scopes can read test-setup data.
- `Roaster.Consumer.Tests` — per-test Guid-keyed InMemory DbContext, `FakeConsumeContext` helper, mocks `IAiReviewService` and `IPublishEndpoint`.
- `Roaster.Reviewer.Tests` — unit tests for `ReviewResponseParser` (malformed JSON, markdown, conversation-history building).
- `Roaster.Data.Tests` — EF configuration tests (FK cascade rules, owned-type mapping, enum conversions, `UpdatedAt` behavior).
