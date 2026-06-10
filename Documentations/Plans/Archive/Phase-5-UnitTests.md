# Phase 5 — Unit Tests

Add test coverage across the core business logic. Focus on things that can break silently: validation, AI output parsing, consumer orchestration, and endpoint behaviour.

> Scope to be refined once Phase 1–4 are implemented. This plan describes the intent and test areas — exact test project names and structure follow the actual code.

---

## Pre-checklist

- [ ] Add test projects to `Roaster.slnx` under solution folder `Tests`

---

## Test Projects

| Project | What it tests |
|---------|--------------|
| `Roaster.Data.Tests` | EF configurations, JSONB mappings |
| `Roaster.Api.Tests` | Endpoint validation, request sanitization |
| `Roaster.Reviewer.Tests` | AI JSON parsing, fallback chain |
| `Roaster.Consumer.Tests` | Consumer orchestration with mocked AI |

**NuGet (all test projects):**
```xml
<PackageReference Include="xunit" Version="2.9.0" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
<PackageReference Include="Moq" Version="4.20.72" />
<PackageReference Include="FluentAssertions" Version="6.12.1" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />  <!-- API tests only -->
```

---

## Roaster.Api.Tests

### Validation tests

- `POST /api/submissions` with empty code → `400`
- `POST /api/submissions` with code over 50,000 chars → `400`
- `POST /api/submissions` with unsupported language → `400`
- `POST /api/submissions` with valid request → `202`
- `POST .../threads` with empty content → `400`
- `POST .../threads` with content over 5,000 chars → `400`

### Sanitization tests

- Code containing control characters is stripped before save
- Sanitized code is what gets persisted in `Payload.Content`

### Endpoint logic tests

- `GET /api/submissions/{id}` returns `404` for unknown ID
- `GET /api/submissions/{id}/review` returns `204` when status is Pending/Processing
- `GET /api/submissions/{id}/review` returns `200 ReviewDto` when Completed
- Review comments from JSONB are correctly mapped to `ReviewCommentDto` list
- `ThreadId` is populated on `ReviewCommentDto` when a thread exists for that line

---

## Roaster.Reviewer.Tests

### JSON parsing tests (`ParseReviewResponse`)

- Valid JSON returned directly → parsed correctly
- JSON wrapped in ` ```json ... ``` ` → stripped and parsed
- JSON preceded by prose text → `{...}` extracted and parsed
- Malformed JSON → returns `AiReviewResult(null, [])` without throwing
- Empty response → returns `AiReviewResult(null, [])` without throwing
- Comment with `severity = 30` → maps to `ReviewSeverity.Brutal`
- Comment with `lineNumber = 0` → excluded from result

### Thread reply tests

- History is replayed in chronological order
- User messages mapped to `"user"` role, AI messages to `"assistant"` role

---

## Roaster.Consumer.Tests

Mock `IAiReviewService` to avoid Ollama dependency in tests.

### CodeSubmittedConsumer tests

- Submission not found → consumer returns without throwing
- Happy path: Status cycles Pending → Processing → Completed
- `Review` and `ReviewMetrics` are saved
- `SubmissionEvents` rows: ProcessingStarted + ReviewCompleted written
- `ReviewCompletedEvent` is published
- AI throws → Status set to Failed, SubmissionEvent (Failed) written, exception re-thrown

### UserThreadReplyConsumer tests

- Thread not found → returns without throwing
- Happy path: AI reply saved as `ThreadReply` (IsAiReply = true)
- `SubmissionEvent` (AiReplied) written
- Conversation history built in correct chronological order

---

## Roaster.Data.Tests

- `Submission` with `required CodePayload` cannot be instantiated with null Payload
- `SubmissionConfiguration` maps `Status` as int, not string
- `ReviewConfiguration` maps `Comments` to `jsonb` column type
- `SubmissionEventConfiguration` maps `Metadata` to `jsonb`
- `SaveChangesAsync` auto-updates `UpdatedAt` on modified Submission entries