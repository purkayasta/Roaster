# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Detailed documentation lives in `Documentations/`:
- **[BACKEND.md](Documentations/BACKEND.md)** — data flow, endpoints, data model, patterns, config, tests
- **[FRONTEND.md](Documentations/FRONTEND.md)** — Blazor WASM structure, components, services, conventions
- **[RUNNING.md](Documentations/RUNNING.md)** — local setup, Docker Compose, migrations, test commands

---

## Solution Structure

`Roaster.slnx` at the repo root — three solution folders:

- **Backend/** — `Roaster.Api`, `Roaster.Consumer`, `Roaster.Reviewer`, `Roaster.Data`, `Roaster.Contracts`, `Roaster.Migration`
- **Frontend/** — `Roaster.Editor` (Blazor WASM, .NET 10)
- **Tests/** — `Roaster.Api.Tests`, `Roaster.Consumer.Tests`, `Roaster.Data.Tests`, `Roaster.Reviewer.Tests`

Source in `src/`, tests in `tests/`.

---

## Quick Commands

```bash
# Build / test everything (from repo root)
dotnet build
dotnet test

# Run migrations
dotnet run --project src/Roaster.Migration -- "Host=localhost;Database=roaster;Username=postgres;Password=postgres"

# Run frontend (from src/Roaster.Editor)
dotnet run
dotnet watch run   # hot reload
```

## Make Targets

`make up` is **detached by default**. Pass `a=1` to attach and see logs.

```bash
# Local dev (no Docker — requires infra running)
make api            # dotnet watch run for Roaster.Api
make consumer       # dotnet watch run for Roaster.Consumer
make editor         # dotnet watch run for Roaster.Editor

# Docker — full stack (single docker-compose.yml)
make up             # start everything
make up a=1         # start and attach to logs
make down           # tear down
```

**Typical dev workflow:**
1. `make up` — start full stack in Docker (DB + RabbitMQ + migration + API + consumer + frontend)
2. Or for local hot-reload: `make api` + `make consumer` + `make editor` (requires Docker infra already running)

All Docker images use alpine variants for minimal size. Ollama model is configured via `OLLAMA_MODEL` env var (default: `qwen2.5-coder:7b`).

---

## Architecture Summary

**Request lifecycle:** Browser → `POST /api/sessions` (create session) → `POST /api/submissions` (submit code, tied to session) → RabbitMQ `code-submitted` queue → `CodeSubmittedConsumer` (fans out to three specialized reviewer agents — Security, Bug, Syntax — via Semantic Kernel + Ollama) → saves merged `Review` + `ReviewMetrics` → browser polls `GET /api/submissions/{id}/review` until 200.

**Thread replies:** User posts to `/api/submissions/{subId}/reviews/{revId}/lines/{lineNum}/threads` → `UserThreadReplyConsumer` builds chat history → AI reply saved → `GET /api/threads/{threadId}/replies` returns conversation.

**Multi-agent review.** `AiReviewService.ReviewCodeAsync` runs three specialized agents **sequentially** — Security, Bug, Syntax — each behind the `IReviewAgent` seam (today `ChatReviewAgent` over Semantic Kernel; the seam keeps a future move to Microsoft Agent Framework a one-file swap). Every agent gets all files in one message; when >1 file is submitted, a manifest primes cross-file reasoning. Comments are tagged with their agent (`[Security] …`), concatenated into one `ReviewerResult`, and de-duped downstream by `CodeReviewService`'s group-by `(file, line)`. A review fails only if **all three** agents fail to parse. All prompts carry a roasting tone (embedded `Prompts/*.txt`).

**Key tech:** ASP.NET Core 10 (Minimal APIs) · EF Core 10 + Npgsql (Postgres 17-alpine) · MassTransit 8 + RabbitMQ · Semantic Kernel 1.74.0 + Ollama (`qwen2.5-coder:7b`) · Blazor WASM 10 · MudBlazor 9.4 · BlazorMonaco 3.4 · DbUp 7.

**JSONB columns:** `Submission.Payload` (code + language), `Review.Comments` (line comments array), `Event.Metadata`.

**1:1 Session ↔ Submission.** Each session has at most one live submission. The API always returns the latest submission for a session.

**Soft delete.** Sessions are soft-deleted via `IsActive = false`. All list queries filter `IsActive = true`.

**Status enum (smallint):** 10=Submitted, 20=Processing, 30=Completed, 40=Failed.

**Retry:** `CodeSubmittedConsumer` — exponential (3 attempts, 10 s–120 s). `UserThreadReplyConsumer` — fixed [3 s, 10 s, 30 s].

**Frontend:** Dashboard (`/`) lists sessions via `SessionView` (`MudDataGrid`). "New" button opens `NewSessionDialog`, calls `POST /api/sessions`, navigates to `/session/{id}`. All action handlers (new/edit/delete) live in `Index.razor`; child components emit `EventCallback<Guid>` only. `Session.razor` loads session detail and renders the Monaco editor (`Code.razor`) with language + theme dropdowns. Custom themes (e.g. Nord Dark) are JSON `EmbeddedResource` files registered by `ThemeFactory` once per app session.

**Frontend services:** `SessionService`, `SubmissionService`, and `ReviewService` are typed `HttpClient` clients registered in `Program.cs`. `ReviewService` also handles thread replies and resolve.

**Backend services:** `Roaster.Api` has a `Services/` folder with one service per resource. All business logic lives there; endpoints are thin wrappers that return `ServiceResult<T>` directly (it implements `IResult`).

**Tests:** xUnit + Moq + FluentAssertions. Api tests use `WebApplicationFactory` + EF InMemory + mocked `IPublishEndpoint`. Consumer tests use `FakeConsumeContext`. No real broker or database needed for tests.

---

## Coding Conventions

- **`sealed` by default.** Every new class is `sealed` unless inheritance is explicitly required.
- **`record` for data.** DTOs, request/response models, and value objects use `sealed record`. Use `class` only when mutability or methods make `record` inappropriate.
- **No overengineering.** No abstractions, interfaces, or generics unless there are multiple concrete uses right now. Three similar lines beats a premature abstraction.
- **Performance at call sites.** Prefer `IReadOnlyList` over `IEnumerable` for already-materialised collections. Avoid unnecessary `.ToList()` copies. Be allocation-aware in hot paths (consumer processing, polling loops).
