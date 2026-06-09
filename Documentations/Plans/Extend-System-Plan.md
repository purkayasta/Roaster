# Extend-System-Plan

Make Roaster **source-agnostic** so GitHub and Azure DevOps PR reviews can be added
later as adapters — **without breaking the existing web editor**. The web editor
becomes the first of several "review sources," not a special case.

---

## Guiding principle — Ports & Adapters

The review engine should not know *where* code came from or *where* results go.

```
                 ┌─────────────────── Core (source-agnostic) ───────────────────┐
 Inbound         │                                                              │  Outbound
 adapters        │   ReviewRequest ──► queue ──► reviewer (3 agents) ──►         │  adapters
 (ingestion)     │                                            Review + event    │  (delivery)
 ─────────────   │                                                              │  ─────────────
 Web editor  ───►│  IReviewIngestion.Submit(ReviewRequest)                      │──► Web (store; browser polls)
 GitHub App  ───►│                                            ReviewCompleted ──┼──► GitHub (inline PR review)
 Azure DevOps──►│                                                   dispatch    │──► Azure DevOps (PR threads)
                 └──────────────────────────────────────────────────────────────┘
```

**What is already agnostic:** `IAiReviewService.ReviewCodeAsync(IReadOnlyList<CodeFileInput>)`
takes `(FileName, Content, Language)` — no source coupling. The reviewer needs no
changes to support PRs.

**What is editor-coupled today (the seams to introduce):**
- **Ingestion** — `Session` + `Submission` (1:1, soft-delete, 50k cap) and `POST /api/submissions` are editor concepts.
- **Delivery** — the browser polls `GET /api/submissions/{id}/review`. PRs need results *pushed back* to the platform.
- **Identity** — there is no auth/tenancy; PR platforms require per-install tokens, webhook-secret validation, and rate-limit handling.

---

## Core abstractions to introduce

| Abstraction | Role | Web editor | GitHub / ADO |
|---|---|---|---|
| `ReviewRequest` (normalized) | What the engine reviews: `SourceType`, `SourceRef`, `CodeUnit[]`, optional `Diff` | built from a submission | built from a PR webhook |
| `CodeUnit` | One file (+ optional hunks/diff position map); superset of `CodeFileInput` | full-file, no diff | changed files + hunks |
| `SourceRef` | Opaque pointer back to origin (sessionId, or repo+PR+commit) | sessionId | repo + PR number + commit SHA |
| `IReviewDelivery` (per `SourceType`) | Where results go after `ReviewCompletedEvent` | store only (no-op; browser polls) | post inline review/threads |
| `IReviewIngestion` | Inbound port: accept a `ReviewRequest`, enqueue it | editor adapter calls it | webhook adapters call it |

`SourceType` (enum: `WebEditor`, `GitHub`, `AzureDevOps`) flows on the request **and**
on `ReviewCompletedEvent`, so the delivery dispatcher routes results to the right adapter.

---

## Phases

| # | Phase | Status | Summary |
|---|-------|--------|---------|
| ES-1 | Source-aware pipeline | ⬜ Not started | Normalize request + carry `SourceType`/`SourceRef`; add `IReviewDelivery` (Web impl) — editor behaviour unchanged |
| ES-2 | Diff-aware review unit | ⬜ Not started | `CodeUnit` carries optional hunks + diff-position mapping; reviewer constrains comments to changed lines when present |
| ES-3 | GitHub adapter | ⬜ Not started | GitHub App, webhook ingestion, fetch diff, `GitHubReviewDelivery` posts inline PR review |
| ES-4 | Azure DevOps adapter | ⬜ Not started | Service-hook ingestion + PR-thread delivery, same shape as ES-3 |
| ES-5 | Scale & hardening | ⬜ Not started | Chunk large PRs (map-reduce), horizontal workers, rate limiting, re-review/idempotency, model upgrade |

### ES-1 — Source-aware pipeline (enabling refactor; **no new platform**)

The whole point: introduce the seams while the editor keeps working byte-for-byte.

- [ ] Define `ReviewRequest`, `CodeUnit`, `SourceRef`, `SourceType` in `Roaster.Contracts` (no behaviour yet)
- [ ] Add `SourceType` + `SourceRef` to the queue event(s) and to `ReviewCompletedEvent`
- [ ] Introduce `IReviewIngestion`; refactor `SubmissionService` to build a `ReviewRequest` with `SourceType = WebEditor` and call it (DB model unchanged — `Submission` is the WebEditor projection)
- [ ] Introduce `IReviewDelivery`; add `WebReviewDelivery` (store-only/no-op — browser still polls). Dispatch on `ReviewCompletedEvent` by `SourceType`
- [ ] Tests: editor flow unchanged (existing 41 green); add a dispatcher test for `SourceType` routing
- Note: pure refactor — no endpoints removed, no DB migration

### ES-2 — Diff-aware review unit

- [ ] Extend `CodeUnit` with optional `Hunks` / changed-line ranges + a diff-position map
- [ ] Reviewer: when diff present, **only emit comments on changed lines** and map line→diff position; when absent (editor), behave as today
- [ ] Prompt note: "review the changed lines; use surrounding context only to understand them"
- [ ] Tests: full-file (editor) vs diff (PR) comment-anchoring

### ES-3 — GitHub adapter

- [ ] GitHub App: installation tokens, webhook endpoint, **webhook-secret validation**, fast ACK → enqueue
- [ ] Fetch PR diff via GitHub API → build `ReviewRequest` (`SourceType = GitHub`, `SourceRef = repo+PR+SHA`)
- [ ] `GitHubReviewDelivery`: on `ReviewCompletedEvent`, post a **single batched review** with inline comments (not N comment calls); respect API rate limits
- [ ] Per-install tenancy + secret storage; map comment severity → review tone

### ES-4 — Azure DevOps adapter

- [ ] Service hooks (PR created/updated) → webhook ingestion, same normalized `ReviewRequest`
- [ ] `AzureDevOpsReviewDelivery`: post PR **comment threads** on changed lines
- [ ] OAuth/PAT auth + org/project tenancy

### ES-5 — Scale & hardening

- [ ] **Chunk large PRs** into per-file/per-hunk review jobs (map-reduce); merge before delivery — keeps each model call small (helps small models, see context note in BACKEND/RUNNING docs)
- [ ] **Horizontal workers**: raise consumer concurrency + run multiple instances (queue already supports it; today `PrefetchCount=1`/`ConcurrentMessageLimit=1` serialises everything)
- [ ] **Re-review/idempotency**: new commits supersede in-flight reviews; dedupe; cancel stale
- [ ] **Rate limiting** against platform APIs; backoff
- [ ] **Model ceiling**: 3B is not credible for production PR review — step up the model (trivial via the `IReviewAgent` / MA-3 `IChatClient` seam)

---

## Cross-cutting concerns

- **Auth & multi-tenancy** — the biggest new surface. Decide SaaS-multi-tenant vs single-org early (ES-3 prerequisite).
- **Idempotency / re-review** — PRs change; the engine must handle supersession, not just first-submit.
- **Observability** — correlation IDs already exist; extend to carry `SourceRef` end-to-end for tracing a PR through the pipeline.
- **Backpressure** — many repos × frequent pushes can flood the queue; ES-5 scaling is not optional at real volume.

---

## Open decisions (resolve before ES-3)

1. **Multi-tenant SaaS** (many orgs install the GitHub App) **or single-org/self-hosted?** Drives auth, tenancy, secret management.
2. **Self-hosted model or hosted API?** At PR scale the 3B almost certainly won't hold; this may make MA-3 (model swap) a prerequisite of ES-3.
3. **Store full PR files or diff-only?** Affects the data model and the 50k-char assumptions.
4. **Delivery granularity** — one batched review per PR (preferred; rate-limit friendly) vs per-comment calls.

---

## Non-goals / guardrails

- Do **not** bake GitHub/ADO specifics into the core or the reviewer — they live only in adapters.
- Do **not** break or remove the web-editor endpoints; the editor is the `WebEditor` adapter and must stay green throughout.
- Keep the reviewer track (MA-1…MA-4) independent; ES leans on MA-3 only for the model swap.
