# Platform Integration Plan — GitHub & Azure DevOps PR Review

Detailed design for adding GitHub and Azure DevOps pull-request review as **platform
adapters**, without changing the review engine or breaking the web editor.

This is the **design depth** for the `ES-1…ES-4` phases tracked at roadmap level in
[Extend-System-Plan.md](Extend-System-Plan.md). Phase status lives there; this doc owns
the *how*. Large-PR scaling (ES-5) is detailed separately in
[Chunking-Optimization-Plan.md](Chunking-Optimization-Plan.md).

---

## Guiding principle — the platform "wrapper" is two adapters

Doing the `git diff`, preparing file name/content, and talking to the platform API or
webhook is **not the review engine's job**. Each platform is wrapped by a pair of adapters;
the core stays platform-blind.

```
                        ┌──────────── Core (platform-agnostic, unchanged) ────────────┐
 Inbound adapters       │                                                             │  Outbound adapters
 (ingestion)            │  IReviewIngestion ─► queue ─► reviewer (3 agents) ─►         │  (delivery)
 ───────────────        │                                       Review + ReviewDone   │  ───────────────
 Web editor       ─────►│  ReviewRequest{SourceType,SourceRef,CodeUnit[]}              │─► Web (store; browser polls)
 GitHub App webhook ───►│                                       dispatch by SourceType │─► GitHub (batched inline review)
 Azure DevOps hook  ───►│                                                              │─► Azure DevOps (PR threads)
                        └─────────────────────────────────────────────────────────────┘
```

| | Inbound (ingestion) adapter | Outbound (delivery) adapter |
|---|---|---|
| Trigger | Platform **webhook** (PR opened/updated) | Core's `ReviewCompletedEvent` whose `SourceType` matches |
| Job | Validate secret → call platform **API** to fetch the diff → build files + changed-line ranges → produce a `ReviewRequest` → `IReviewIngestion.Submit` | Format comments → call platform **API** to post inline review / threads |
| Owns | git diff, file/name/content prep, changed-line ranges | severity→tone, **position translation**, batching, rate limits |

**What is already agnostic:** `IAiReviewService.ReviewCodeAsync(IReadOnlyList<CodeFileInput>)`
takes `(FileName, Content, Language)` with no source coupling — the reviewer needs **zero
changes** for PRs. Only adapters import Octokit / the Azure DevOps SDK.

---

## Current pipeline (the "before")

```
SubmissionService.CreateAsync (API)
  └─ writes Submission + Payloads to DB
  └─ publishes SubmissionCreatedEvent(SubmissionId)        ← carries only a DB id
CodeReviewService.ProcessAsync (Consumer)
  └─ loads Submission by id, maps Payloads → CodeFileInput[]
  └─ ai.ReviewCodeAsync(files)                             ← already source-agnostic
  └─ saves Review/Events; publishes ReviewCompletedEvent(SubmissionId)
Browser polls GET /api/submissions/{id}/review            ← delivery is implicit (pull)
```

The pipeline is keyed on `Submission`, a WebEditor concept; the event carries a `long
SubmissionId` and the consumer re-loads the code from the DB. There is no notion of origin
or destination — that is what ES-1 introduces.

---

## Architecture decision — in-process adapters vs separate service

| | In-process adapters (**recommended to start**) | Separate per-platform service |
|---|---|---|
| Shape | `Roaster.Integrations.GitHub` library; webhook endpoints in `Roaster.Api`, delivery in `Roaster.Consumer` | `Roaster.GitHub` deployable, talks to core via queue/HTTP |
| Pros | Reuses host, DI, MassTransit, DB; fastest path | Independent deploy/scale, secret isolation, blast-radius isolation |
| Cons | Shared deploy + secret surface | More ops: another service, pipeline, cross-service contract |

**Decision:** start with **in-process adapters behind the ports**, mirroring the Reviewer's
own "library now, promote to a service later" philosophy ([Phase-5-Reviewer.md](Archive/Phase-5-Reviewer.md)).
Promote a platform to its own service only when tenant scale or rate-limit isolation forces
it — the ports make that a deployment change, not a redesign.

---

## Core abstractions (introduced in ES-1, in `Roaster.Contracts`)

```csharp
enum SourceType { WebEditor, GitHub, AzureDevOps }

// Opaque pointer back to origin: sessionId (editor) or repo+PR+SHA (PR platforms).
record SourceRef(string Value);

// Superset of CodeFileInput; Hunks/changed-line ranges are added in ES-2 (null for editor).
record CodeUnit(string FileName, string Content, string Language, IReadOnlyList<LineRange>? ChangedRanges = null);

record ReviewRequest(SourceType SourceType, SourceRef SourceRef, IReadOnlyList<CodeUnit> Units);

// Inbound port — adapters call this to enqueue work.
interface IReviewIngestion { Task SubmitAsync(ReviewRequest request, CancellationToken ct); }

// Outbound port — one impl per SourceType; dispatcher routes ReviewCompletedEvent by SourceType.
interface IReviewDelivery { SourceType SourceType { get; } Task DeliverAsync(ReviewCompletedEvent done, CancellationToken ct); }
```

`SourceType` + `SourceRef` also flow on `SubmissionCreatedEvent` and `ReviewCompletedEvent`
so the delivery dispatcher can route results to the correct adapter.

### Responsibility split (the key design rule)

| Concern | Lives in | Why |
|---|---|---|
| What lines are reviewable (changed) | **Core** (`CodeUnit.ChangedRanges`) | engine must constrain comments to changed lines |
| Comment shape `(filePath, fileLineNumber, severity)` | **Core** | platform-neutral |
| `fileLine` → platform position (GitHub diff position vs ADO line+offset) | **Delivery adapter** | GitHub and ADO address diffs differently; keep the core neutral |
| Fetching diff / building `CodeUnit`s | **Ingestion adapter** | not the engine's job |

> **Refinement of ES-2:** the original plan put a "diff-position map" on `CodeUnit`. Keep only
> the *changed-line set* in the core; push *position translation* into the delivery adapter,
> because the addressing schemes are platform-specific. This keeps the engine platform-blind.

---

## Phases

Status is tracked in [Extend-System-Plan.md](Extend-System-Plan.md); IDs are shared.

### ES-1 — Source-aware pipeline (pure refactor, **no new platform**)

Introduce the seams while the editor behaves byte-for-byte identically.

- [ ] Define `ReviewRequest`, `CodeUnit`, `SourceRef`, `SourceType` in `Roaster.Contracts` (no behaviour)
- [ ] Add `SourceType` + `SourceRef` to `SubmissionCreatedEvent` and `ReviewCompletedEvent`
- [ ] Introduce `IReviewIngestion`; refactor `SubmissionService.CreateAsync` to build a `ReviewRequest{SourceType=WebEditor}` and call it (the `Submission` row stays — it is the WebEditor projection)
- [ ] Introduce `IReviewDelivery`; add `WebReviewDelivery` (store-only / no-op — browser still polls). Dispatch on `ReviewCompletedEvent` by `SourceType`
- [ ] **Verify:** existing editor flow unchanged (full suite green); add a dispatcher test for `SourceType` routing
- Note: no endpoint removed, no DB migration

### ES-2 — Diff-aware review unit

- [ ] Extend `CodeUnit` with optional `ChangedRanges` (changed-line ranges)
- [ ] Reviewer: when `ChangedRanges` present, **only emit comments on changed lines**; when absent (editor), behave as today
- [ ] Prompt note: "review the changed lines; use surrounding context only to understand them"
- [ ] Position translation deferred to delivery adapters (see responsibility split)
- [ ] **Verify:** full-file (editor) vs changed-line (PR) comment anchoring tests

### ES-3 — GitHub adapter

1. [ ] **Auth — GitHub App:** registration, private key, per-installation tokens (JWT → installation access token), webhook secret
2. [ ] **Webhook ingestion:** endpoint for `pull_request` (`opened`, `synchronize`); **validate `X-Hub-Signature-256` HMAC**; **ACK within ~10s** → enqueue only (never review inline)
3. [ ] **Fetch diff:** with head SHA, call PR files/compare API → per-file hunks; build `CodeUnit`s (content + `ChangedRanges`)
4. [ ] **Normalize:** `ReviewRequest{SourceType=GitHub, SourceRef=repo+PR+headSha+installationId}`
5. [ ] **Delivery `GitHubReviewDelivery`:** post **one batched review** (`POST /repos/{o}/{r}/pulls/{n}/reviews`) with all inline comments — **not N calls**; translate `(path,line)` → GitHub **diff position**; map severity → tone
6. [ ] **Rate limits:** 5,000 req/hr per installation + secondary limits; backoff/retry
7. [ ] **Tenancy:** store installation→repo mapping + per-install tokens/secrets
8. [ ] **Idempotency:** `synchronize` fires on every push — supersede in-flight reviews per `repo+PR`, keep latest `headSha`
- [ ] **Verify:** end-to-end against a test repo; signature-validation test; batched-review (single API call) test

### ES-4 — Azure DevOps adapter (same shape, different mechanics)

1. [ ] **Auth:** PAT or OAuth (Azure AD app) / service principal, scoped per org/project
2. [ ] **Ingestion:** Service Hooks (PR created/updated) → webhook; validate via shared-secret/basic-auth
3. [ ] **Fetch diff:** ADO REST — PR **iterations** + **changes** + file content → hunks (iteration model differs from GitHub's compare; adapter hides it)
4. [ ] **Delivery `AzureDevOpsReviewDelivery`:** post PR **comment threads** (`POST .../pullRequests/{id}/threads`) with `threadContext` = `filePath` + `rightFileStart/End {line, offset}` (**1-based line + char offset**, unlike GitHub's diff position — proof the translation belongs in the adapter)
5. [ ] **Tenancy:** org/project mapping + PAT/OAuth storage
- [ ] **Verify:** end-to-end against a test ADO project; thread anchored to correct changed line

### ES-5 — Scale & hardening

Tracked separately — see [Chunking-Optimization-Plan.md](Chunking-Optimization-Plan.md)
(large-PR map-reduce chunking, horizontal workers, idempotency, rate limiting, model ceiling).

---

## Dependencies & cross-cutting

- **Model ceiling (MA-3):** local 3B/7B is not credible for production PR review. The clean
  swap to a hosted model is the **MEAI `IChatClient`** move (MA-3 in [plan.md](../../plan.md)),
  making MA-3 a soft prerequisite of *good* ES-3 — not of the plumbing. Target MEAI
  `IChatClient`, **not** full Microsoft Agent Framework: the static Security/Bug/Syntax
  fan-out is a chat workload, not an agent-orchestration workload.
- **Auth & multi-tenancy** — the biggest new surface; decide SaaS-multi-tenant vs
  single-org/self-hosted **before ES-3** (drives token storage + data model).
- **Idempotency / re-review** — PRs mutate; handle supersession, not just first-submit.
- **Observability** — extend existing correlation IDs to carry `SourceRef` end-to-end.
- **Backpressure** — today `PrefetchCount=1`/`ConcurrentMessageLimit=1` serialises everything;
  real volume needs ES-5 scaling.

## Open decisions (resolve before ES-3)

1. **Multi-tenant SaaS or single-org/self-hosted?** — drives auth, tenancy, secrets.
2. **Self-hosted model or hosted API?** — at PR scale the small model won't hold; may make
   MA-3 a prerequisite of ES-3.
3. **Store full PR files or diff-only?** — affects data model and the 50k-char assumption.
4. **Delivery granularity** — one batched review per PR (preferred) vs per-comment calls.

---

## Recommended sequence

1. **ES-1** — source-aware ports (editor stays green; no platform, no model work)
2. **ES-2** — changed-line constraint in core; position translation deferred to adapters
3. **MA-3** — MEAI `IChatClient` + hosted model (if credible review is wanted before exposing PRs)
4. **ES-3 GitHub** — first real adapter; validates the whole port design end-to-end
5. **ES-4 Azure DevOps** — mostly "fill in the adapter" once the seams are proven
6. **ES-5** — scale / idempotency / chunking at real volume

## Proposed layout (in-process start)

```
src/
  Roaster.Contracts/        + ReviewRequest, CodeUnit, SourceRef, SourceType
  Roaster.Api/              + webhook endpoints (thin: validate + enqueue)
  Roaster.Consumer/         + delivery dispatcher (routes ReviewCompletedEvent by SourceType)
  Roaster.Integrations.GitHub/        inbound + outbound GitHub adapter (Octokit)
  Roaster.Integrations.AzureDevOps/   inbound + outbound ADO adapter
  Roaster.Reviewer/         unchanged
```

## Non-goals / guardrails

- Do **not** bake GitHub/ADO specifics into the core or the reviewer — they live only in adapters.
- Do **not** break or remove the web-editor endpoints; the editor is the `WebEditor` adapter and stays green throughout.
- Keep the reviewer track (MA-1…MA-4) independent; this track leans on MA-3 only for the model swap.
