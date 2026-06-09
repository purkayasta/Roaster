# Roaster — Plan

For all implementation details, see [Documentations/Summary.md](Documentations/Summary.md).

---

## Global Pre-checklist

- [x] `Roaster.slnx` at solution root
- [x] `Roaster.Editor` added to `Roaster.slnx` under `Frontend` solution folder
- [x] All backend projects added to `Roaster.slnx` under `Backend` solution folder (pending `Roaster.Migration` in Phase 6):
  - [x] `Roaster.Data`
  - [x] `Roaster.Contracts`
  - [x] `Roaster.Api`
  - [x] `Roaster.Consumer`
  - [x] `Roaster.Reviewer`
  - [x] `Roaster.Migration`

---

## Phases

| # | Phase | Status | Detail |
|---|-------|--------|--------|
| 0 | Setup & Rename | ✅ Done | [Phase-0-Setup.md](Documentations/Plans/Phase-0-Setup.md) |
| 1 | API | ✅ Done | [Phase-1-API.md](Documentations/Plans/Phase-1-API.md) |
| 2 | Events | ✅ Done | [Phase-2-Events.md](Documentations/Plans/Phase-2-Events.md) |
| 3 | Correlation IDs | ✅ Done | [Phase-3-CorrelationIDs.md](Documentations/Plans/Phase-3-CorrelationIDs.md) |
| 4 | AI Reviewer | ✅ Done | [Phase-4-AIReviewer.md](Documentations/Plans/Phase-4-AIReviewer.md) |
| 5 | Unit Tests | ✅ Done | [Phase-5-UnitTests.md](Documentations/Plans/Phase-5-UnitTests.md) |
| 6 | DbUp Migrations | ⬜ Not started | [Phase-6-Migration.md](Documentations/Plans/Phase-6-Migration.md) |
| 7 | Docker | ✅ Done | [Phase-7-Docker.md](Documentations/Plans/Phase-7-Docker.md) |
| 8 | Frontend | ✅ Done | [Phase-8-Frontend.md](Documentations/Plans/Phase-8-Frontend.md) |
| 9 | Continue | ⬜ Not started | [Phase-9-Continue.md](Documentations/Plans/Phase-9-Continue.md) |

---

## Multi-Agent Reviewer Track

Evolving the AI reviewer from a single Semantic Kernel chat call into a specialized
multi-agent workflow, structured so a later move from Semantic Kernel (SK) to the
Microsoft Agent Framework (MAF) is a one-file swap. All work is isolated to
`Roaster.Reviewer` (+ its test project). Staying on SK for now — LangGraph was
ruled out (Python sidecar in an all-.NET stack).

| # | Phase | Status | Summary |
|---|-------|--------|---------|
| MA-1 | Sequential fan-out | ✅ Done | Three specialized agents (Security, Bug, Syntax) run sequentially |
| MA-2 | Harden model path | ✅ Done | Execution settings (temperature, max output tokens) + context-window guidance |
| MA-3 | MAF bridge | ⬜ Not started | Back `IReviewAgent` with MAF/MEAI; structured output |
| MA-4 | Roadmap (model-gated) | ⬜ Not started | Filters, function-calling/plugins, RAG, OpenTelemetry |

### MA-1 — Sequential fan-out ✅ Done

- [x] Add `IReviewAgent` seam (`Name` + `ReviewAsync`) — the SK→MAF migration boundary
- [x] Add `ChatReviewAgent` (SK chat-backed implementation)
- [x] Add `security.txt`, `bug.txt`, `syntax.txt` prompts (same JSON contract → reuse parser/models)
- [x] Roasting tone baked into all prompts; remove unused `reviewer.txt` / `roaster.txt`
- [x] Enrich prompts: input-format guidance + cross-file awareness; manifest prepended for multi-file submissions
- [x] `ReviewCodeAsync` runs agents sequentially, tags comments (`[Security] …`), merges into one `ReviewerResult`
- [x] Failure tolerance: fail only if **all three** agents fail to parse
- [x] DI wiring in `ReviewerExtensions`; `AddAiReviewer()` signature unchanged (no API/consumer changes)
- [x] `ReplyToThreadAsync` left untouched; reply test constructor updated
- [x] Docs updated (CLAUDE.md, BACKEND.md, README.md); full suite green

### MA-2 — Harden model path ✅ Done

Verified against `1.74.0-alpha`: `OllamaPromptExecutionSettings` exposes only
`Temperature`, `TopP`, `TopK`, `NumPredict`, `Stop`. **No** `Seed`, `NumCtx`, or
`Format` — so structured output is deferred to MA-3 (needs `IChatClient`).

- [x] `OllamaOptions`: add `Temperature` (default `0.3`) and nullable `MaxOutputTokens` (MEAI naming; default unset). Values held as provider-neutral primitives so they carry over to MEAI `ChatOptions`
- [x] `ChatReviewAgent`: accept `PromptExecutionSettings?` (base type), pass to `GetChatMessageContentsAsync`
- [x] `ReviewerExtensions`: build `OllamaPromptExecutionSettings` (`Temperature`, `NumPredict`) from options at the DI seam, inject into the agents
- [x] Document `Ollama:Temperature` / `MaxOutputTokens` in appsettings + BACKEND.md
- [x] Reply path unchanged (avoids reply-test churn); all 41 tests green
- [x] Document `OLLAMA_CONTEXT_LENGTH=8192` on the Ollama host in RUNNING.md (default 4096; SK can't set `num_ctx`)
- Note: `Seed` / `NumCtx` are Ollama server/model level, not reviewer code; Ollama runs on the host here (not in compose)

### MA-3 — MAF bridge ⬜ Not started

- [ ] Add `MafReviewAgent : IReviewAgent` over MAF/MEAI `IChatClient`; swap construction in `ReviewerExtensions`
- [ ] Adopt structured output via `IChatClient` `ResponseFormat` / JSON schema (retire regex-fallback path)
- [ ] Orchestration, prompts, parser, consumer remain untouched

### MA-4 — Roadmap (model-gated) ⬜ Not started

- [ ] Filters (`IFunctionInvocationFilter` / `IPromptRenderFilter`) for cross-cutting logging/metrics
- [ ] Function-calling / plugins (Roslyn analyzer, CWE lookup) — **gated on a larger model**; 3B unreliable at tool-calling
- [ ] RAG over team coding standards (vector store + embedding model)
- [ ] OpenTelemetry on model calls to enrich `ReviewMetrics`

---

## Source-Agnostic / PR Integration Track

Making the system source-agnostic so GitHub and Azure DevOps PR reviews can be added
as adapters without breaking the web editor. Full plan:
[Extend-System-Plan.md](Documentations/Plans/Extend-System-Plan.md).

| # | Phase | Status | Summary |
|---|-------|--------|---------|
| ES-1 | Source-aware pipeline | ⬜ Not started | Normalize request + carry `SourceType`/`SourceRef`; `IReviewDelivery` (Web) — editor unchanged |
| ES-2 | Diff-aware review unit | ⬜ Not started | `CodeUnit` carries hunks + diff-position mapping |
| ES-3 | GitHub adapter | ⬜ Not started | GitHub App webhook ingestion + inline PR review delivery |
| ES-4 | Azure DevOps adapter | ⬜ Not started | Service-hook ingestion + PR-thread delivery |
| ES-5 | Scale & hardening | ⬜ Not started | Chunk large PRs, horizontal workers, idempotency, model upgrade |

The chunking strategy in ES-5 — preserving cross-file relationships while chunking for
scale — is detailed in its own optimization plan:
[Chunking-Optimization-Plan.md](Documentations/Plans/Chunking-Optimization-Plan.md) (phases CO-1…CO-6).

Compression.