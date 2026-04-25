# Phase 8 — Frontend

Wire the existing Blazor WASM frontend to the real API. Replace `MockRoastApiService` with an HTTP implementation that uses the polling model.

> To be planned in detail once Phases 1–7 are stable. This file describes the known interface changes needed.

---

## What Needs to Change

### 1. Update `IRoastApiService` interface

Current signature:
```csharp
Task<bool> ReplyToThreadAsync(int lineNumber, string userReply);
```

New signature (uses `threadId` from `ReviewCommentDto` instead of `lineNumber`):
```csharp
Task<bool> ReplyToThreadAsync(long threadId, string content);
```

**File:** [Editor/Services/IRoastApiService.cs](../../Editor/Services/IRoastApiService.cs)

---

### 2. Create `HttpRoasterService`

New class implementing `IRoastApiService` using `HttpClient`.

**`SubmitCodeAsync` flow:**
1. `POST /api/submissions` → receive `SubmissionDto` with submission ID
2. Poll `GET /api/submissions/{id}` until `Status == "Completed"` or `"Failed"` (on page refresh or timer)
3. `GET /api/submissions/{id}/review` → receive `ReviewDto`
4. Map `ReviewDto.Comments` (each `ReviewCommentDto`) → `List<RoastComment>`

**Mapping:**
```csharp
new RoastComment
{
    LineNumber   = c.LineNumber,
    CommentText  = c.Comment ?? string.Empty,
    Severity     = c.Severity,
    ThreadId     = c.ThreadId?.ToString()
}
```

**`ReplyToThreadAsync` flow:**
1. `POST /api/submissions/{submissionId}/reviews/{reviewId}/lines/{lineNumber}/threads`
   with body `{ "content": userReply }`
2. Returns `202` with `{ threadId, replyId }`

---

### 3. Update `RoastMyCode.razor`

- Store `ReviewCommentDto` list (with `ThreadId`) alongside `RoastComment` list
- Pass `threadId` (long) instead of `lineNumber` when calling reply endpoint
- Poll for AI replies: `GET /api/threads/{threadId}/replies` periodically after a user reply

**File:** [Editor/Pages/RoastMyCode.razor](../../Editor/Pages/RoastMyCode.razor)

---

### 4. Swap service registration in `Program.cs`

```csharp
// Replace:
builder.Services.AddScoped<IRoastApiService, MockRoastApiService>();

// With:
builder.Services.AddScoped<IRoastApiService, HttpRoasterService>();
builder.Services.AddScoped(sp =>
    new HttpClient { BaseAddress = new Uri("http://localhost:8080") });
```

**File:** [Editor/Program.cs](../../Editor/Program.cs)