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

# Run backend services
dotnet run --project src/Roaster.Api
dotnet run --project src/Roaster.Consumer

# Run migrations
dotnet run --project src/Roaster.Migration -- "Host=localhost;Database=roaster;Username=postgres;Password=postgres"

# Run frontend (from src/Roaster.Editor)
dotnet run
dotnet watch run   # hot reload

# Full stack via Docker
docker-compose up --build
```

---

## Architecture Summary

**Request lifecycle:** Browser → `POST /api/sessions` (create session) → `POST /api/submissions` (submit code, tied to session) → RabbitMQ `code-submitted` queue → `CodeSubmittedConsumer` (calls Ollama via Semantic Kernel) → saves `Review` + `ReviewMetrics` → browser polls `GET /api/submissions/{id}/review` until 200.

**Thread replies:** User posts to `/api/submissions/{subId}/reviews/{revId}/lines/{lineNum}/threads` → `UserThreadReplyConsumer` builds chat history → AI reply saved → `GET /api/threads/{threadId}/replies` returns conversation.

**Key tech:** ASP.NET Core 10 (Minimal APIs) · EF Core 10 + Npgsql (Postgres 17) · MassTransit 8 + RabbitMQ · Semantic Kernel 1.74.0 + Ollama (`qwen2.5-coder:7b`) · Blazor WASM 10 · MudBlazor 9.4 · DbUp 7.

**JSONB columns:** `Submission.Payload` (code + language), `Review.Comments` (line comments array), `SubmissionEvent.Metadata`.

**1:1 Session ↔ Submission.** Each session has at most one live submission. Resubmitting replaces the payload, deletes the existing review, and auto-captures the old payload in `SubmissionHistory`.

**Status enum (smallint):** 10=Submitted, 20=Processing, 30=Completed, 40=Failed.

**Retry:** `CodeSubmittedConsumer` — exponential (3 attempts, 10 s–120 s). `UserThreadReplyConsumer` — fixed [3 s, 10 s, 30 s].

**Frontend:** Dashboard (`/`) lists submissions via `MudDataGrid`. "New" button opens `NewSessionDialog` which generates a client-side UUID and navigates to `/session/{id}/{name}`. `SubmissionService` calls real API at `Api:BaseUrl` (default `http://localhost:8080`).

**Tests:** xUnit + Moq + FluentAssertions. Api tests use `WebApplicationFactory` + EF InMemory + mocked `IPublishEndpoint`. Consumer tests use `FakeConsumeContext`. No real broker or database needed for tests.
