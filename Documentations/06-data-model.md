# 6. Data Model

## Entity Relationship Diagram

```mermaid
erDiagram
    SESSION ||--o| SUBMISSION : "1:1 (unique FK)"
    SUBMISSION ||--o| REVIEW : "1:1"
    REVIEW ||--o{ COMMENTTHREAD : "1:N"
    COMMENTTHREAD ||--o{ THREADREPLY : "1:N"
    SUBMISSION ||--o{ EVENT : "1:N"
    REVIEW ||--o| REVIEWMETRICS : "1:1"

    SESSION {
        uuid Id PK
        varchar Name
        timestamp CreatedAt
        bool IsActive
    }
    SUBMISSION {
        bigint Id PK
        uuid SessionId FK
        jsonb Payloads
        smallint Status
    }
    REVIEW {
        bigint Id PK
        bigint SubmissionId FK
        text Summary
        jsonb Comments
    }
    COMMENTTHREAD {
        bigint Id PK
        bigint ReviewId FK
        int LineNumber
        smallint Status
    }
    THREADREPLY {
        bigint Id PK
        bigint ThreadId FK
        text Content
        bool IsAiReply
    }
    EVENT {
        bigint Id PK
        bigint SubmissionId FK
        uuid SessionId
        smallint EventType
        uuid CorrelationId
        jsonb Metadata
    }
    REVIEWMETRICS {
        bigint Id PK
        bigint ReviewId FK
        varchar ModelId
        int QueueWaitMs
        int ReviewDurationMs
    }
```

## Enum Values (all `smallint`)

| Enum | Values |
|---|---|
| `SubmissionStatus` | 10=Submitted, 20=Processing, 30=Completed, 40=Failed |
| `ThreadStatus` | 10=Unresolved (default), 20=Resolved |
| `ReviewSeverity` | 10=Gentle, 20=Medium, 30=Brutal |
| `EventType` | 10=Submitted, 20=ProcessingStarted, 30=ReviewCompleted, 40=Failed, 50=UserReplied, 60=AiReplied |

## JSONB Owned Types

- `Submission.Payloads` → `List<Payload{FileName, Content, Language}>` — one entry per file.
- `Review.Comments` → `List<ReviewLineComment{FileName, LineNumber, Comment, Severity}>`.
- `Event.Metadata` → arbitrary JSON — token usage on success, real parser/exception error on
  failure. Rendered in the UI via `JsonViewer.razor`.
