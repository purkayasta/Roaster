# Roaster Backend — Phase Summary

Top-level index for all phase-level implementation plans. Each phase builds on the previous — nothing is planned upfront beyond what that phase needs. Requirements can change; phases are kept small and independent.

---

## Global Pre-checklist

Before starting any phase:

- [x] `Roaster.slnx` exists at the solution root
- [ ] All backend projects are added to `Roaster.slnx` under a `Backend` solution folder:
  - [ ] `Roaster.Data`
  - [ ] `Roaster.Contracts`
  - [ ] `Roaster.Api`
  - [ ] `Roaster.Consumer`
  - [ ] `Roaster.Reviewer`
  - [ ] `Roaster.Migration`

---

## Phases

| # | Phase | Scope |
|---|-------|-------|
| [0](Plans/Phase-0-Setup.md) | **Setup & Rename** | Repo rename, `Roaster.slnx` creation, solution layout |
| [1](Plans/Phase-1-API.md) | **API** | Models, DbContext, contracts (DTOs only), minimal API endpoints |
| [2](Plans/Phase-2-Events.md) | **Events** | SubmissionEvent model, event contracts, Consumer project, API publishes events |
| [3](Plans/Phase-3-CorrelationIDs.md) | **Correlation IDs** | Middleware, propagation through events, consumer log context |
| [4](Plans/Phase-4-AIReviewer.md) | **AI Reviewer** | ReviewMetrics model, Reviewer library (Semantic Kernel + Ollama), Consumer stubs replaced |
| [5](Plans/Phase-5-UnitTests.md) | **Unit Tests** | Validation, AI parsing, consumer orchestration, EF config |
| [6](Plans/Phase-6-Migration.md) | **DbUp Migrations** | Migration console app, all SQL scripts (Tables then Indexes) |
| [7](Plans/Phase-7-Docker.md) | **Docker** | Dockerfiles, docker-compose with postgres, rabbitmq, ollama, migration |
| [8](Plans/Phase-8-Frontend.md) | **Frontend** | Wire Blazor WASM to real API, replace mock service |
| [9](Plans/Phase-9-Continue.md) | **Continue** | Grafana + Loki, rate limiting, DLQ monitoring, integration tests, auth |

---

## What Each Phase Adds

```
Phase 1  →  Working API (no events, no AI — status stays Pending)
Phase 2  →  Events flow: submission published, consumer listens, status updates
Phase 3  →  CorrelationId flows API → event → consumer → DB
Phase 4  →  AI review actually runs; ReviewMetrics saved
Phase 5  →  Test coverage on all of the above
Phase 6  →  DB schema managed by DbUp SQL scripts
Phase 7  →  Everything runs in Docker
Phase 8  →  Blazor frontend calls real API
Phase 9  →  Observability, safety, and scale
```

---

## Solution Layout

```
Roaster.slnx
Editor/                    # Blazor WASM frontend (existing)
src/
  Roaster.Data/            # Added Phase 1 — EF Core models, DbContext, configs
  Roaster.Contracts/       # Added Phase 1 — DTOs; Events added Phase 2
  Roaster.Api/             # Added Phase 1
  Roaster.Consumer/        # Added Phase 2
  Roaster.Reviewer/        # Added Phase 4
  Roaster.Migration/       # Added Phase 6
Documentations/
  Summary.md
  Plans/
    Phase-0-Setup.md
    Phase-1-API.md
    Phase-2-Events.md
    Phase-3-CorrelationIDs.md
    Phase-4-AIReviewer.md
    Phase-5-UnitTests.md
    Phase-6-Migration.md
    Phase-7-Docker.md
    Phase-8-Frontend.md
    Phase-9-Continue.md
docker/
  Dockerfile.api
  Dockerfile.consumer
  Dockerfile.migration
docker-compose.yml
plan.md
```

---

## Core Design Decisions

| Decision | Choice | Reason |
|----------|--------|--------|
| ID type | `long` (bigserial) | Sequential inserts, smaller FK joins vs UUID |
| Property mutability | `init` everywhere; `set` for `Status` + `UpdatedAt` only | Communicates immutability clearly |
| Enum backing | `int` with gaps (10/20/30) | Future values insertable without renumbering |
| Code storage | JSONB `Payload` on Submissions | Groups code + language as a document |
| Review comments | JSONB `Comments` on Reviews | Always read together; avoids join table |
| Thread reference | `(ReviewId, LineNumber)` UNIQUE | No per-comment FK needed |
| Performance data | `ReviewMetrics` table | Aggregate-friendly; no JSONB parsing |
| Schema management | DbUp + SQL (Tables → Indexes) | Full control; no EF migration lock-in |
| Events | IDs only | Consumers fetch fresh data from DB |
| AI project | Class library (not HTTP service) | Code-level separation; no inter-service HTTP |
| API docs | Scalar at `/scalar/v1` (dev only) | Modern OpenAPI UI; gated on `IsDevelopment()` |
| Real-time | None (page refresh) | No SignalR dependency in current scope |