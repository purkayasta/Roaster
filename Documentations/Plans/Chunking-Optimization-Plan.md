# Chunking-Optimization-Plan

A **performance/scale optimization** for reviewing large submissions and PRs without
losing the cross-file relationships that MA-1's cross-file awareness depends on.

> **Scope:** This is pure optimization. It changes *how much* and *how* code reaches
> the reviewer — not the review contract, not the editor behaviour, not the API.
> It is the detailed design behind **ES-5 → "chunk large PRs (map-reduce)"** in the
> [Extend-System-Plan](Extend-System-Plan.md), and overlaps with **MA-4** (a Roslyn
> analyzer as a plugin) and **MA-3** (a bigger model for the holistic pass).

---

## 1. The problem

Today `AiReviewService` sends **every file, in full, to every agent** in one message.
That works for a single editor submission but breaks down as input grows:

- **Context ceiling.** A large PR blows past Ollama's `num_ctx` (default 4096); the oldest
  tokens (instructions + early files) are silently dropped.
- **Small-model attention decay.** Even if it fits, a 3B model reasons worse over long
  inputs ("lost in the middle") — more tokens ≠ better review.
- **Throughput.** Three sequential calls over the *whole* input, with `PrefetchCount=1`,
  serialises everything. Many PRs queue badly.

The obvious fix — **chunk the input into independent jobs** — introduces a new failure:

- **Relationship loss.** A chunk reviewed in isolation can't see that it calls a function
  defined in another file, shares a type, or breaks a caller's assumption. This is exactly
  what MA-1 cross-file awareness was built to catch. Naive chunking throws it away.

This plan resolves that tension.

---

## 2. Core principle

> **Separate _what you review_ (attention) from _what you can see_ (context).**

Chunk the **unit of attention** — the code a given job is responsible for commenting on —
but give each job **context** about everything it connects to, expressed compactly
(signatures, call edges, type defs) rather than as full source text.

Two supporting truths:

1. **Cross-file _structure_ is better extracted by tools than inferred by an LLM.** A
   compiler/parser knows call graphs and type relationships precisely; an LLM guesses.
   So we compute relationships deterministically and *feed* them to the model.
2. **On a small model, smart chunking can beat whole-repo stuffing**, because focused
   context preserves attention quality. This optimization is not just about cost.

---

## 3. The pipeline

```
        ┌──────────────┐   ┌────────────────────┐   ┌──────────────────┐   ┌─────────────────┐   ┌──────────────────┐
 input  │ A. Structural │   │ B. Relationship-   │   │ C. Context        │   │ D. Per-chunk     │   │ E. Integration + │
 ──────►│   map (no LLM)│──►│   aware chunking    │──►│   injection        │──►│   review (agents) │──►│   synthesis/merge │──► Review
        └──────────────┘   └────────────────────┘   └──────────────────┘   └─────────────────┘   └──────────────────┘
            Roslyn /            graph-guided            chunk + skeleton        Security/Bug/        dependency-aware
            tree-sitter         splitting               of neighbours           Syntax × chunk       pass + dedupe
```

### Stage A — Structural map (deterministic, no model)

Build a **code graph** over the submission/PR using static analysis. No LLM, no cost
beyond parsing.

- **C# (first-class):** Roslyn — resolves symbols, call graph, type usage, inheritance.
- **Polyglot (fallback):** tree-sitter per language — gives a syntactic graph (defs,
  references, imports) without full semantic resolution.
- **Unknown language (last resort):** filename/import-heuristic grouping only.

Suggested model:

```csharp
sealed record CodeSymbol(
    string Id,            // stable key, e.g. "Roaster.Api.Services.SubmissionService.SubmitAsync"
    SymbolKind Kind,      // Type, Method, Field, ...
    string FileName,
    int StartLine, int EndLine,
    string Signature);    // compact form for context injection (no body)

sealed record CodeEdge(
    string FromId, string ToId,
    EdgeKind Kind);       // Calls, Implements, Inherits, Uses, Imports

sealed record CodeGraph(
    IReadOnlyList<CodeSymbol> Symbols,
    IReadOnlyList<CodeEdge> Edges);
```

#### Stage A — graph lifecycle & caching (per-source)

**When is the graph built?** Not "every request" and not "once forever" — it is **keyed by
commit, built incrementally, and cached per repo.** The strategy differs by source.

A hard constraint drives this: for an accurate C# graph, **Roslyn needs the whole project
(a compilation), not just the changed files** — call edges and type resolution can't be
computed from isolated diff hunks because the callee often lives in an unchanged file.

| | Editor submission | PR (GitHub / Azure DevOps) |
|---|---|---|
| Strategy | Build **per-request, in-memory** over the submitted files | **Per-repo persistent index, incremental, keyed by commit** |
| Why | No repo; self-contained; tiny; often skips chunking entirely | Same repo gets many PRs/pushes — rebuilding all of it each time is the waste |
| Persistence | None | Cached/stored; invalidated by commit/content hash |

**Why not the two naive options:**
- **Diff-only graph** — cheap, but can't resolve cross-file edges into unchanged files →
  misses the very relationships we want. Defeats the purpose.
- **Whole-repo rebuild every push** — accurate, but reparses ~99% unchanged code every
  time. Doesn't scale.

**Recommended PR approach — base index + diff overlay:**
1. Maintain a **base index for the default branch**, advanced incrementally (re-parse only
   changed files, patch the graph).
2. For a PR, **overlay the diff** onto the base: re-parse only the PR's changed files
   against the base compilation → PR-head graph at the cost of *the change*, not the repo.
3. **Cache keyed by commit SHA / content hash.** A re-review of the same commit reuses the
   graph; a new push re-parses only its delta. (So a PR with 5 pushes pays 5 *deltas*, not
   5 full builds.)

**Rollout (don't over-engineer first):**
- **CO-2 first cut:** parse the PR's checked-out snapshot **per commit, cache by SHA.**
  Simple, correct, no incremental machinery — fine while repos/volume are modest.
- **Later (scale):** move to base-branch index + diff overlay + incremental updates **only
  when** reparse cost or PR volume actually hurts. Measure first (§7).

**Prior art — consider reusing, not building:** this is a code-intelligence index. SCIP/LSIF,
Sourcegraph, or even ctags may serve before we hand-roll an indexer.

**Caveats:**
- **Staleness is worse than absence.** Bad cache invalidation injects *wrong* context →
  worse than no context. Invalidate strictly by commit/content hash.
- **Branches/forks.** The graph is relative to a commit; fork PRs and long-lived branches
  complicate the "base" assumption. Base+overlay covers most cases — document the edges.
- **Polyglot.** tree-sitter is per-file and needs no compilation, so it's cheaper to build
  per-request — but syntactic only, so cross-file resolution is weaker. The accuracy/cost
  tradeoff differs per language.

### Stage B — Relationship-aware chunking

Do **not** split arbitrarily. Use the graph to keep related code together and to bound size.

Rules, in priority order:
1. **Never split a function/method** across chunks (use symbol spans, not byte offsets).
2. **Co-locate tightly-coupled symbols** in one chunk when they fit (a type + the DTOs it
   returns; a handler + its validator) — driven by edge density.
3. **Sliding-window overlap** for anything that must still be cut, so a boundary symbol is
   visible in both neighbouring chunks (configurable margin, e.g. ±20 lines).
4. **Token budget per chunk** sized to leave room for prompt + injected context + response
   within the configured `num_ctx` (see §6 sizing math).

```csharp
sealed record ReviewChunk(
    string Id,
    IReadOnlyList<CodeUnitSlice> Target,   // the lines this chunk is responsible for
    IReadOnlyList<string> MemberSymbolIds);
```

### Stage C — Context injection

Each chunk is reviewed **with a compact "neighbourhood skeleton"** — not full source of
other files.

For chunk *C*, collect the **1-hop neighbourhood** from the graph (symbols *C* calls,
is called by, implements, or whose types it uses) and render them as signatures + a
one-line role, plus the cross-file edges. Example injected block:

```
RELATED CONTEXT (signatures only — do not review these, use them to understand the target):
- Service.cs  →  Task<Result> SubmitAsync(SubmissionRequest req)        [called by target at line 14]
- Models.cs   →  record SubmissionRequest(Guid SessionId, List<File> Files)
- target calls Service.SubmitAsync; Service.SubmitAsync may return a failed Result
```

This restores MA-1 cross-file awareness at a fraction of the tokens. The prompt instructs:
*"Review only the TARGET lines; use RELATED CONTEXT to understand them — never comment on
context-only code."*

### Stage D — Per-chunk review (reuse existing agents)

Run the existing Security/Bug/Syntax `IReviewAgent`s over each `(chunk + context)`.
- **Parallelism axis opens up here:** chunks are independent → horizontal workers / parallel
  Ollama (see ES-5) instead of one serial pass.
- Comments are tagged (`[Security] …`) and carry the chunk/file origin, as today.

### Stage E — Integration + synthesis pass

A dedicated pass that the per-chunk reviews can't do:
1. **Integration agent** — fed the *graph* + the *per-chunk findings* (not full bodies);
   its only job is cross-cutting/architectural issues that span chunks (a contract changed
   in one chunk that breaks a caller in another, duplicated logic across files, a leaky
   abstraction).
2. **Merge/dedupe** — collapse overlapping comments produced by overlapping windows, group
   by `(file, line)` (the consumer already does part of this), and reconcile severities.

---

## 4. Phases

| # | Phase | Status | Summary |
|---|-------|--------|---------|
| CO-1 | Token budgeting + naive line chunking | ⬜ Not started | Split safely by size, no graph yet; proves the pipeline + measures baseline relationship loss |
| CO-2 | C# structural map (Roslyn) | ⬜ Not started | Build `CodeGraph` for C#; cache it |
| CO-3 | Relationship-aware chunking + context injection | ⬜ Not started | Graph-guided splitting + 1-hop skeleton context |
| CO-4 | Integration/synthesis pass | ⬜ Not started | Cross-chunk agent + merge/dedupe |
| CO-5 | Polyglot via tree-sitter + degradation | ⬜ Not started | Extend beyond C#; graceful fallback for unknown languages |
| CO-6 | Parallel/horizontal execution | ⬜ Not started | Chunks fan out across workers / parallel Ollama (ties to ES-5) |

### CO-1 — Token budgeting + naive chunking (baseline)
- [ ] Add a token estimator (chars/4 heuristic is fine to start) and a configurable per-chunk budget
- [ ] Split along **whole-line / whole-file** boundaries only; never mid-line
- [ ] Map-reduce wiring: review chunks, concatenate + dedupe results (reuse existing merge)
- [ ] **Measure** baseline: how many cross-file findings are lost vs the current whole-input review (see §7)
- Note: deliberately dumb — establishes the harness and the loss metric before investing in the graph

### CO-2 — C# structural map
- [ ] Roslyn-based extractor producing `CodeGraph` (symbols, signatures, edges) for C# files
- [ ] Cache the graph keyed by content hash; for PRs, incremental (only changed files re-parsed)
- [ ] No review behaviour change yet — graph is built and logged/validated

### CO-3 — Relationship-aware chunking + context injection
- [ ] Chunk by symbol spans with edge-density co-location + overlap margin
- [ ] Build 1-hop neighbourhood skeleton per chunk; render the RELATED CONTEXT block
- [ ] Prompt update: "review TARGET only; RELATED CONTEXT is read-only"
- [ ] **Measure** cross-file finding recall vs CO-1 baseline and vs whole-input review

### CO-4 — Integration/synthesis pass
- [ ] Integration agent fed graph + per-chunk findings (signatures, not bodies)
- [ ] Merge/dedupe across overlapping windows; severity reconciliation
- [ ] Ensure idempotent comment identity so re-reviews don't double-post (ties to ES-5)

### CO-5 — Polyglot + degradation
- [ ] tree-sitter extractors for the other supported languages (js/ts/python/java/go/rust/cpp/sql)
- [ ] Degradation ladder: Roslyn (C#) → tree-sitter (known) → filename/import grouping (unknown) → CO-1 naive
- [ ] Per-language capability flag surfaced in logs/metrics

### CO-6 — Parallel/horizontal execution
- [ ] Chunks dispatched concurrently (parallel Ollama with `OLLAMA_NUM_PARALLEL`, and/or multiple consumer instances)
- [ ] Backpressure + ordering: results re-assembled deterministically before delivery

---

## 5. Degradation ladder (must never hard-fail)

Each rung falls back to the next if unavailable, so the reviewer always produces *something*:

```
Roslyn semantic graph (C#)
   ↓ (non-C# / Roslyn fails)
tree-sitter syntactic graph (known language)
   ↓ (unknown language / parse fails)
filename + import-heuristic grouping
   ↓ (still too big)
CO-1 naive size-based chunking
   ↓ (single small submission)
current whole-input review (unchanged editor path)
```

A small editor submission that fits in one chunk should take the **last** rung — i.e. it
behaves exactly as today. This optimization only engages when size demands it.

---

## 6. Sizing math (per chunk)

```
num_ctx  ≥  system_prompt + related_context_skeleton + target_chunk + expected_response
```

- Start from the configured `OLLAMA_CONTEXT_LENGTH` (e.g. 8192).
- Reserve: prompt (~500 tok) + response headroom (~1500 tok) + context skeleton (~budgeted).
- Remaining budget → target chunk size. Keep a safety margin (e.g. target ≤ 60% of `num_ctx`)
  so the skeleton + response never get squeezed.
- Token estimate: `chars / 4` is good enough for budgeting; refine later if needed.

This math is the contract between this plan and `OLLAMA_CONTEXT_LENGTH` (RUNNING.md) and
the parallel/context-split note (BACKEND.md).

---

## 7. How we measure success (don't optimise blind)

Define metrics **before** CO-1 so each phase is judged, not assumed:

- **Cross-file finding recall** — on a fixed test corpus with known cross-file issues, what
  fraction does each strategy catch? (whole-input = the target to match; CO-1 naive = the
  floor; CO-3 should approach whole-input.)
- **Token-per-review** and **wall-clock-per-review** — the cost side.
- **Throughput** — reviews/min under a queue of N PRs.
- **False-positive rate on context** — does the model wrongly comment on RELATED CONTEXT
  (it shouldn't)? A guard metric for the prompt.

Keep a small **golden set** of multi-file submissions with hand-labelled expected findings.

---

## 8. Tradeoffs & risks

- **Engineering cost is real and language-specific.** Roslyn is excellent for C#; every
  other language needs a tree-sitter extractor. Start C#-only.
- **More passes = more calls.** Latency/cost rise; each call is smaller/sharper (good for
  small models). Net win depends on parallelism (CO-6).
- **Residual loss.** No chunking reproduces 100% of a single infinite-context, large-model
  review. The aim is to recover *most* cross-file value cheaply and push the deterministic
  parts onto tools. The fully-holistic pass is gated on a bigger model (MA-3).
- **Graph staleness for PRs.** Incremental re-parse must be correct or context is wrong;
  cache invalidation by content hash is mandatory.
- **Overlap double-counting.** Sliding windows can duplicate findings; CO-4 dedupe is not
  optional.

---

## 9. Open decisions

1. **C#-only first, or invest in tree-sitter immediately?** (Recommend C#-only for CO-2/3;
   the corpus is .NET-heavy.)
2. **Where does the graph live** — in `Roaster.Reviewer`, or a new `Roaster.Analysis`
   project? (Keeping analysis separate from inference keeps `Reviewer` clean.)
3. **Is the integration pass a 4th `IReviewAgent`, or a distinct stage?** (Leaning distinct
   stage — it consumes findings + graph, not raw code.)
4. **Do we chunk the editor path at all,** or only PR ingestion? (Recommend: editor stays
   whole-input unless it exceeds budget — see degradation ladder.)

---

## 10. Non-goals / guardrails

- Do **not** change the review JSON contract, the parser, or the consumer's merge — chunking
  feeds the *same* agents and produces the *same* `ReviewerResult` shape.
- Do **not** regress the small-editor path: single small submissions must behave as today.
- Do **not** rely on the LLM for cross-file *structure* — extract it deterministically and
  inject it.
- Do **not** ship any chunking phase without the §7 recall metric showing it doesn't lose
  more than an agreed threshold of cross-file findings.
