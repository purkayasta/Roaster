# 2. Architecture

## System Diagram

```mermaid
flowchart LR
    Browser["Browser\n(Blazor WASM)"] -->|HTTP| API["Roaster.Api"]
    API -->|EF Core| PG[("PostgreSQL")]
    API -->|publish event| MQ(["RabbitMQ"])
    MQ -->|consume| Consumer["Roaster.Consumer"]
    Consumer -->|EF Core| PG
    Consumer -->|Semantic Kernel| Reviewer["Roaster.Reviewer"]
    Reviewer -->|HTTP| Ollama[("Ollama\n(native, host machine)")]
    Migration["Roaster.Migration\n(DbUp, one-shot)"] -->|SQL scripts| PG
    Consumer -->|publish event| MQ
    Browser -.->|poll| API
```

Everything except Ollama runs in Docker. Ollama runs natively on the host so it can use Apple
Silicon/GPU acceleration — the Consumer reaches it via `host.docker.internal:11434`.

## Request Lifecycle

```mermaid
sequenceDiagram
    participant B as Browser
    participant A as Roaster.Api
    participant Q as RabbitMQ
    participant C as Roaster.Consumer
    participant R as Roaster.Reviewer (Ollama)

    B->>A: POST /api/sessions
    A-->>B: 201 SessionDto
    B->>A: POST /api/submissions (code + language)
    A->>A: Save Submission (Status=Submitted)
    A->>Q: Publish SubmissionCreatedEvent
    A-->>B: 202 Accepted
    Q->>C: Consume event
    C->>C: Status → Processing
    C->>R: ReviewCodeAsync(files)
    R-->>C: comments + summary (per file)
    C->>C: Save Review + ReviewMetrics, Status → Completed
    C->>Q: Publish ReviewCompletedEvent
    loop every 3s
        B->>A: GET /api/submissions/{id}/review
        A-->>B: 204 (not ready) or 200 (ready)
    end
```

## Thread Replies

```mermaid
sequenceDiagram
    participant B as Browser
    participant A as Roaster.Api
    participant Q as RabbitMQ
    participant C as ThreadReplyService
    participant R as Roaster.Reviewer (Ollama)

    B->>A: POST .../lines/{line}/threads (reply text)
    A->>A: Save ThreadReply (IsAiReply=false)
    A->>Q: Publish ReplyEvent
    Q->>C: Consume event
    C->>C: Build chat history from prior replies
    C->>R: ReplyToThreadAsync(...)
    R-->>C: plain-text reply
    C->>C: Save ThreadReply (IsAiReply=true)
    B->>A: GET /api/threads/{id}/replies
    A-->>B: full conversation
```

## Docker Compose Topology

```mermaid
flowchart TD
    postgres["postgres:17-alpine"] --> migration["migration\n(DbUp, restart: no)"]
    migration --> roaster-api["roaster-api :8080"]
    rabbitmq["rabbitmq:4-management-alpine"] --> roaster-api
    rabbitmq --> roaster-consumer["roaster-consumer"]
    postgres --> roaster-consumer
    roaster-api --> roaster-frontend["roaster-frontend :5094\n(nginx)"]
    ollama[("ollama\n(native, host.docker.internal:11434)")] -.-> roaster-consumer
```

## Key Architectural Decisions

- **1 session : 1 live submission.** `Submission.SessionId` is a unique FK; resubmitting replaces
  the payload and drops the old review rather than creating a new submission row.
- **Per-file AI review, not one giant prompt.** Each file in a multi-file submission gets its own
  Ollama call and its own system-prompt context — one oversized file can't blow the context budget
  for the rest of the submission. See [05-ai-pipeline.md](05-ai-pipeline.md).
- **Defensive JSON parsing, never throws.** The AI is not a guaranteed structured-output engine;
  parsing degrades through multiple fallback stages instead of failing the whole review.
- **Soft delete for sessions.** `IsActive` flag, never a hard delete — audit `Event` rows always
  stay valid.
- **CorrelationId = SessionId.** `Event.CorrelationId` is the session's UUID, not the HTTP
  correlation header — it's the stable identifier for grouping a session's full history.
