# Phase 6 — DbUp Migrations

Create the `Roaster.Migration` console app that owns all schema creation. Runs once as a Docker init container before the API and Consumer start.

---

## Pre-checklist

- [ ] Add `Roaster.Migration` to `Roaster.slnx` under solution folder `Backend`

---

## Overview

- No EF migrations, no `EnsureCreated`
- All SQL is handwritten — full control over column types, indexes, constraints
- DbUp tracks applied scripts in a `schemaversions` table; re-running is safe
- Tables before indexes — enforced by two explicit script passes in `Program.cs`

**SDK:** `Microsoft.NET.Sdk` (net10.0, console app)

**NuGet:**
```xml
<PackageReference Include="dbup-postgresql" Version="5.0.1" />
```

---

## Folder Structure

```
src/Roaster.Migration/
  Scripts/
    Tables/
      0001_Submissions.sql
      0002_Reviews.sql
      0003_CommentThreads.sql
      0004_ThreadReplies.sql
      0005_SubmissionEvents.sql
      0006_ReviewMetrics.sql
    Indexes/
      0001_Submissions.sql
      0002_Reviews.sql
      0003_CommentThreads.sql
      0004_ThreadReplies.sql
      0005_SubmissionEvents.sql
      0006_ReviewMetrics.sql
  Program.cs
  Roaster.Migration.csproj
```

Mark all SQL files as embedded resources:

```xml
<ItemGroup>
  <EmbeddedResource Include="Scripts/**/*.sql" />
</ItemGroup>
```

---

## Program.cs

```csharp
var connectionString = args.FirstOrDefault()
    ?? Environment.GetEnvironmentVariable("MIGRATION_CONNECTION_STRING")
    ?? throw new InvalidOperationException("No connection string provided.");

EnsureDatabase.For.PostgresqlDatabase(connectionString);

var upgrader = DeployChanges.To
    .PostgresqlDatabase(connectionString)
    .WithScriptsEmbeddedInAssembly(
        Assembly.GetExecutingAssembly(),
        s => s.Contains("Tables"))      // Tables first
    .WithScriptsEmbeddedInAssembly(
        Assembly.GetExecutingAssembly(),
        s => s.Contains("Indexes"))     // Indexes after all tables exist
    .WithTransaction()
    .LogToConsole()
    .Build();

var result = upgrader.PerformUpgrade();
return result.Successful ? 0 : 1;
```

---

## SQL Scripts

Table names use quoted PascalCase to match EF configuration.

### Tables/0001_Submissions.sql

```sql
CREATE TABLE "Submissions" (
    "Id"         bigserial    PRIMARY KEY,
    "Payload"    jsonb        NOT NULL,
    "Status"     smallint     NOT NULL DEFAULT 10,
    "CreatedAt"  timestamptz  NOT NULL DEFAULT now(),
    "UpdatedAt"  timestamptz  NOT NULL DEFAULT now()
);
```

### Tables/0002_Reviews.sql

```sql
CREATE TABLE "Reviews" (
    "Id"           bigserial    PRIMARY KEY,
    "SubmissionId" bigint       NOT NULL REFERENCES "Submissions"("Id") ON DELETE CASCADE,
    "Summary"      text,
    "Comments"     jsonb        NOT NULL DEFAULT '[]',
    "CreatedAt"    timestamptz  NOT NULL DEFAULT now()
);
```

### Tables/0003_CommentThreads.sql

```sql
CREATE TABLE "CommentThreads" (
    "Id"          bigserial    PRIMARY KEY,
    "ReviewId"    bigint       NOT NULL REFERENCES "Reviews"("Id") ON DELETE CASCADE,
    "LineNumber"  int          NOT NULL,
    "CreatedAt"   timestamptz  NOT NULL DEFAULT now(),
    UNIQUE ("ReviewId", "LineNumber")
);
```

### Tables/0004_ThreadReplies.sql

```sql
CREATE TABLE "ThreadReplies" (
    "Id"          bigserial    PRIMARY KEY,
    "ThreadId"    bigint       NOT NULL REFERENCES "CommentThreads"("Id") ON DELETE CASCADE,
    "Content"     text,
    "IsAiReply"   boolean      NOT NULL DEFAULT false,
    "CreatedAt"   timestamptz  NOT NULL DEFAULT now()
);
```

### Tables/0005_SubmissionEvents.sql

```sql
CREATE TABLE "SubmissionEvents" (
    "Id"            bigserial    PRIMARY KEY,
    "SubmissionId"  bigint       NOT NULL REFERENCES "Submissions"("Id") ON DELETE CASCADE,
    "EventType"     smallint     NOT NULL,
    "CorrelationId" uuid         NOT NULL,
    "Metadata"      jsonb,
    "OccurredAt"    timestamptz  NOT NULL DEFAULT now()
);
```

### Tables/0006_ReviewMetrics.sql

```sql
CREATE TABLE "ReviewMetrics" (
    "Id"                bigserial    PRIMARY KEY,
    "SubmissionId"      bigint       NOT NULL REFERENCES "Submissions"("Id") ON DELETE CASCADE,
    "ModelId"           varchar(100),
    "QueueWaitMs"       int          NOT NULL DEFAULT 0,
    "ReviewDurationMs"  int          NOT NULL DEFAULT 0,
    "TotalDurationMs"   int          NOT NULL DEFAULT 0,
    "CommentCount"      int          NOT NULL DEFAULT 0,
    "CodeLengthChars"   int          NOT NULL DEFAULT 0,
    "CreatedAt"         timestamptz  NOT NULL DEFAULT now()
);
```

### Indexes/0001_Submissions.sql

```sql
CREATE INDEX idx_submissions_status     ON "Submissions"("Status");
CREATE INDEX idx_submissions_created_at ON "Submissions"("CreatedAt" DESC);
```

### Indexes/0002_Reviews.sql

```sql
CREATE INDEX idx_reviews_submission_id ON "Reviews"("SubmissionId");
CREATE INDEX idx_reviews_comments_gin  ON "Reviews" USING gin("Comments");
```

### Indexes/0003_CommentThreads.sql

```sql
CREATE INDEX idx_comment_threads_review ON "CommentThreads"("ReviewId");
```

### Indexes/0004_ThreadReplies.sql

```sql
CREATE INDEX idx_thread_replies_thread ON "ThreadReplies"("ThreadId");
CREATE INDEX idx_thread_replies_time   ON "ThreadReplies"("CreatedAt");
```

### Indexes/0005_SubmissionEvents.sql

```sql
CREATE INDEX idx_sub_events_submission  ON "SubmissionEvents"("SubmissionId");
CREATE INDEX idx_sub_events_correlation ON "SubmissionEvents"("CorrelationId");
```

### Indexes/0006_ReviewMetrics.sql

```sql
CREATE INDEX idx_review_metrics_submission ON "ReviewMetrics"("SubmissionId");
```