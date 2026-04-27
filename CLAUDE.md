# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Solution

`Roaster.slnx` at the repo root is the solution file with three solution folders:
- **Backend/** — `Roaster.Api`, `Roaster.Consumer`, `Roaster.Reviewer`, `Roaster.Data`, `Roaster.Contracts`, `Roaster.Migration`
- **Frontend/** — `Roaster.Editor` (Blazor WASM)
- **Tests/** — `Roaster.Api.Tests`, `Roaster.Consumer.Tests`, `Roaster.Data.Tests`, `Roaster.Reviewer.Tests`

---

## Commands

### Full solution

```bash
dotnet build                  # build everything
dotnet test                   # run all tests
```

### Backend services (run from repo root)

```bash
dotnet run --project src/Roaster.Api
dotnet run --project src/Roaster.Consumer

# Run migrations (pass connection string as arg or via env)
dotnet run --project src/Roaster.Migration -- "Host=localhost;Database=roaster;Username=postgres;Password=postgres"
# or: export MIGRATION_CONNECTION_STRING="..." && dotnet run --project src/Roaster.Migration
```

### Frontend (run from `Editor/` subdirectory)

```bash
dotnet run          # http://localhost:5094, https://localhost:7052
dotnet watch run    # hot reload
```

### Tests

```bash
dotnet test tests/Roaster.Api.Tests
dotnet test tests/Roaster.Api.Tests --filter "SubmissionEndpointTests"               # by class
dotnet test tests/Roaster.Api.Tests --filter "SubmissionEndpointTests.Post_Valid_Returns202"  # by method
dotnet test -v detailed                # verbose output
```

---

## Backend Architecture

### End-to-end data flow

```
POST /api/submissions
  → CorrelationIdMiddleware (generates/extracts X-Correlation-Id UUID)
  → Validates SubmissionRequest (code: 1–50k chars, language enum)
  → Strips control characters from code
  → Saves Submission (Status=Pending) + SubmissionEvent(Submitted) in one transaction
  → Publishes CodeSubmittedEvent to RabbitMQ
  → Returns 202 Accepted

RabbitMQ: code-submitted queue
  → CodeSubmittedConsumer (Roaster.Consumer)
  → Status: Pending → Processing; logs ProcessingStarted event
  → Calls IAiReviewService.ReviewCodeAsync() (Roaster.Reviewer → Ollama via Semantic Kernel)
  → Saves Review (Summary + Comments JSONB) + ReviewMetrics + SubmissionEvent(ReviewCompleted)
  → Status: Processing → Completed
  → Publishes ReviewCompletedEvent
  → On failure: Status → Failed, logs Failed event, rethrows (triggers MassTransit retry)

GET /api/submissions/{id}/review
  → Returns 204 while Status ≠ Completed; 200 with ReviewDto when done
  → Frontend polls this endpoint

POST /api/submissions/{id}/reviews/{reviewId}/lines/{lineNumber}/threads
  → Creates CommentThread + ThreadReply (IsAiReply=false)
  → Publishes UserThreadReplyEvent

RabbitMQ: user-thread-reply queue
  → UserThreadReplyConsumer builds full chat history from prior replies
  → Calls IAiReviewService.ReplyToThreadAsync()
  → Saves AI ThreadReply (IsAiReply=true)

GET /api/threads/{threadId}/replies
  → Returns ordered conversation (user/AI alternating)
```

### Project responsibilities

| Project | Role |
|---|---|
| **Roaster.Api** | HTTP endpoints, validation, middleware, publishes events |
| **Roaster.Consumer** | Worker service; consumes RabbitMQ messages, orchestrates review flow |
| **Roaster.Reviewer** | `IAiReviewService` impl — Semantic Kernel 1.74.0 + Ollama connector (alpha) |
| **Roaster.Data** | EF Core `RoasterDbContext`, entity configs, migrations-free schema ownership |
| **Roaster.Contracts** | Shared records: events (`CodeSubmittedEvent`, etc.), request/response DTOs |
| **Roaster.Migration** | DbUp 7.0.1 console app; runs `Scripts/Tables/*.sql` then `Scripts/Indexes/*.sql` |

### Key patterns

**JSONB owned types.** Two EF-owned-type patterns are used:
- `Submission.Payload` → `CodePayload { Content, Language }` stored as a single JSONB column
- `Review.Comments` → `List<ReviewLineComment>` stored as a JSONB array

**Correlation ID propagation.** `CorrelationIdMiddleware` injects the ID into Serilog context, HTTP response headers, all RabbitMQ events, and `SubmissionEvent` rows — so a single UUID traces a request across API, broker, consumer, and database.

**MassTransit retry config.** `CodeSubmittedConsumer` uses exponential back-off (5 retries, 2 s–60 s). `UserThreadReplyConsumer` uses fixed intervals [3 s, 10 s, 30 s]. Consumers explicitly rethrow on AI failure so MassTransit retries fire.

**AI JSON parsing fallback.** `ReviewResponseParser` tries: direct `JsonSerializer.Deserialize` → markdown code-block extraction → substring search. Returns an empty `AiReviewResult` on total failure (no exception propagated from parsing itself).

**Severity enum values.** Line comment severity is stored as smallint: 10 = Gentle, 20 = Medium, 30 = Brutal.

**Status enum values.** Submission status is smallint: 10 = Pending, 20 = Processing, 30 = Completed, 40 = Failed.

### Configuration

`Roaster.Api/appsettings.json` — `ConnectionStrings:Postgres`, `RabbitMQ:Uri`, `Cors:Origins`  
`Roaster.Consumer/appsettings.json` — same DB/RabbitMQ keys plus `Ollama:Endpoint` and `Ollama:ModelId`

Health check endpoint: `GET /health` (checks Postgres + RabbitMQ; 503 if either is down).  
OpenAPI/Scalar UI: available in development at `/scalar/v1`.

### Test conventions

All test projects use **xUnit + Moq + FluentAssertions**.

- `Roaster.Api.Tests` — `WebApplicationFactory<Program>` (`ApiFactory`), swaps DbContext to EF InMemory, mocks `IPublishEndpoint` (no real broker needed).
- `Roaster.Consumer.Tests` — per-test Guid-keyed InMemory DbContext, `FakeConsumeContext` helper, mocks `IAiReviewService` and `IPublishEndpoint`.
- `Roaster.Reviewer.Tests` — unit tests for `ReviewResponseParser` (malformed JSON, markdown escaping) and conversation-history building.
- `Roaster.Data.Tests` — EF configuration tests (FK cascade rules, owned-type mapping).

The InMemory root is shared across scopes in `ApiFactory` so HTTP request scopes can read data written by test setup.

---

## Frontend Architecture (Roaster.Editor)

**Blazor WebAssembly** (.NET 10) that runs entirely in the browser.

**Startup:** `wwwroot/index.html` → Blazor runtime → `Program.cs` → `App.razor` (routing) → `Layout/MainLayout.razor`.

**Key directories:**
- `Pages/` — routable pages (`RoastMyCode.razor`, `CodeWorkspace.razor`, `CodeEditor.razor`)
- `Components/` — `RoastThread.razor` renders inline AI comments
- `Services/` — `IRoastApiService` / `MockRoastApiService` (2 s latency, hardcoded comments for lines 3 & 5)
- `wwwroot/monaco-interop.js` — JS bridge for Monaco view zones
- `wwwroot/syntax-highlight.js` — Highlight.js wrapper

**UI libraries (CDN):** MudBlazor v9.4.0 · BlazorMonaco v3.4.0 (Monaco v0.54.0 via RequireJS) · Fluent UI v4.14.1

### "Hidden div" Monaco integration

`RoastThread` components can't live inside Monaco's DOM directly:

1. Blazor renders `RoastThread` inside a hidden `<div>` in `RoastMyCode.razor`
2. After `StateHasChanged()` + 100 ms delay (lets Blazor flush to DOM), `monaco-interop.js` is called
3. `injectViewZone()` moves the hidden div into a Monaco view zone (6-line height reservation) via `editor.changeViewZones()`
4. Blazor keeps managing state and callbacks despite the DOM relocation

This means Blazor's component tree and the visual DOM tree can legally diverge — components stay live after JS moves their nodes.

### Other conventions

- `.razor.css` files are CSS-isolated to their component
- `_Imports.razor` holds global `@using` statements (including `Roaster.Editor.Components`)
- `IJSRuntime` called via `InvokeVoidAsync` / `InvokeAsync<T>`; Monaco instance retrieval has a built-in 5 s wait
- `EventCallback<T>` for child→parent communication (`OnClose`, `OnReply`)
- State mutations outside event handlers require `StateHasChanged()`
- Dark mode toggle persisted to `localStorage` via `MainLayout.razor`
- To swap mock for real backend: replace `MockRoastApiService` registration in `Program.cs` with an `HttpRoastApiService` — no component code changes needed
