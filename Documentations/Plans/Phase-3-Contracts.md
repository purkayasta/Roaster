# Phase 3 — Roaster.Contracts

## Pre-checklist

- [ ] Add `Roaster.Contracts` project to `Roaster.slnx` under solution folder `Backend`

---

## Overview

A plain class library that contains all shared message bus events and API response DTOs. No external NuGet dependencies — only pure C# POCOs. Both `Roaster.Api` and `Roaster.Consumer` reference this project.

**SDK:** `Microsoft.NET.Sdk` (net10.0, class library)

**NuGet packages:** none

---

## Folder Structure

```
src/Roaster.Contracts/
  Events/
    CodeSubmittedEvent.cs
    UserThreadReplyEvent.cs
    ReviewCompletedEvent.cs
  Dtos/
    SubmissionDto.cs
    ReviewDto.cs
    ReviewCommentDto.cs
    ThreadReplyDto.cs
```

---

## Events (`Events/`)

Events carry **IDs only** — never the code payload or large strings. Consumers fetch what they need from the database directly. This prevents stale data on the message bus and keeps message sizes small.

### CodeSubmittedEvent.cs

Published by `Roaster.Api` when a submission is accepted. Consumed by `Roaster.Consumer` to trigger the AI review.

```csharp
public record CodeSubmittedEvent(
    long SubmissionId,
    Guid CorrelationId,
    DateTime SubmittedAt);
```

### UserThreadReplyEvent.cs

Published by `Roaster.Api` when a user posts a reply to a review comment thread. Consumed by `Roaster.Consumer` to generate the AI reply.

```csharp
public record UserThreadReplyEvent(
    long ThreadId,
    long SubmissionId,
    int LineNumber,
    Guid CorrelationId,
    DateTime SubmittedAt);
```

### ReviewCompletedEvent.cs

Published by `Roaster.Consumer` after a review is fully saved. Can be consumed in the future by the API or a notification service.

```csharp
public record ReviewCompletedEvent(
    long SubmissionId,
    long ReviewId,
    Guid CorrelationId,
    DateTime CompletedAt);
```

---

## Response DTOs (`Dtos/`)

Used as response bodies in the API. Defined here so both the API and any future client libraries share the same shapes.

### SubmissionDto.cs

Returned by `GET /api/submissions/{id}`. The `Payload` (code content) is intentionally excluded to keep polling responses lightweight.

```csharp
public record SubmissionDto(
    long Id,
    string? Language,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);
```

### ReviewDto.cs

Returned by `GET /api/submissions/{id}/review`.

```csharp
public record ReviewDto(
    long Id,
    long SubmissionId,
    string? Summary,
    DateTime CreatedAt,
    List<ReviewCommentDto> Comments);
```

### ReviewCommentDto.cs

Each entry maps to one item in the `Reviews."Comments"` JSONB array. `ThreadId` is populated if a `CommentThread` exists for that line.

```csharp
public record ReviewCommentDto(
    int LineNumber,
    string? Comment,
    string Severity,
    long? ThreadId);
```

### ThreadReplyDto.cs

Returned by `GET /api/threads/{threadId}/replies`. `IsAiReply` distinguishes between user messages and AI responses.

```csharp
public record ThreadReplyDto(
    long Id,
    string? Content,
    bool IsAiReply,
    DateTime CreatedAt);
```