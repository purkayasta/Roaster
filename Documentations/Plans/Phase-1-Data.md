# Phase 1 — Roaster.Data

## Pre-checklist

- [ ] Add `Roaster.Data` project to `Roaster.slnx` under solution folder `Backend`

---

## Overview

Shared class library referenced by both `Roaster.Api` and `Roaster.Consumer`. Contains all EF Core entity models, Fluent API configurations, and the `DbContext`. No migrations live here — schema is managed entirely by `Roaster.Migration`.

**SDK:** `Microsoft.NET.Sdk` (net10.0)

**NuGet packages:**
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.0" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
```

---

## Folder Structure

```
src/Roaster.Data/
  Models/
    CodePayload.cs
    Submission.cs
    Review.cs
    ReviewLineComment.cs
    CommentThread.cs
    ThreadReply.cs
    SubmissionEvent.cs
    ReviewMetrics.cs
    Enums.cs
  Configurations/
    SubmissionConfiguration.cs
    ReviewConfiguration.cs
    CommentThreadConfiguration.cs
    ThreadReplyConfiguration.cs
    SubmissionEventConfiguration.cs
    ReviewMetricsConfiguration.cs
  Context/
    RoasterDbContext.cs
```

---

## Model Rules

- IDs are `long` — maps to `bigserial` in PostgreSQL (better sequential insert perf than UUID)
- Use `init` for all properties that are set once and never mutate; `set` only for `Status` and `UpdatedAt`
- No `= string.Empty` defaults — use `required` for mandatory strings; nullable (`string?`) for optional
- Enum backing values are explicit non-sequential integers (10 / 20 / 30 / 40...) so future values can be inserted between existing ones without renumbering
- Navigation properties use `set` — EF Core must set these during materialisation

---

## Enums (`Models/Enums.cs`)

```csharp
public enum SubmissionStatus
{
    Pending    = 10,
    Processing = 20,
    Completed  = 30,
    Failed     = 40
}

public enum ReviewSeverity
{
    Gentle = 10,
    Medium = 20,
    Brutal = 30
}

public enum SubmissionEventType
{
    Submitted         = 10,
    ProcessingStarted = 20,
    ReviewCompleted   = 30,
    Failed            = 40,
    UserReplied       = 50,
    AiReplied         = 60
}
```

---

## Models

### CodePayload.cs

Stored as JSONB inside the `Submissions` table — not a separate table.

```csharp
public record CodePayload
{
    public required string Content  { get; init; }
    public required string Language { get; init; }
}
```

### Submission.cs

```csharp
public class Submission
{
    public long Id { get; init; }
    public required CodePayload Payload { get; init; }  // JSONB: { content, language }
    public SubmissionStatus Status { get; set; } = SubmissionStatus.Pending;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; set; }

    public Review? Review { get; set; }
    public ICollection<SubmissionEvent> Events { get; set; } = [];
    public ReviewMetrics? Metrics { get; set; }
}
```

### Review.cs

```csharp
public class Review
{
    public long Id { get; init; }
    public long SubmissionId { get; init; }
    public string? Summary { get; init; }
    public List<ReviewLineComment> Comments { get; init; } = [];  // JSONB array
    public DateTime CreatedAt { get; init; }

    public Submission Submission { get; set; } = null!;
    public ICollection<CommentThread> Threads { get; set; } = [];
}
```

### ReviewLineComment.cs

Not a table — lives entirely inside the `Reviews."Comments"` JSONB column.

```csharp
public record ReviewLineComment
{
    public int LineNumber { get; init; }
    public string? Comment { get; init; }
    public ReviewSeverity Severity { get; init; }
}
```

### CommentThread.cs

References a specific line in `Review.Comments` via the `(ReviewId, LineNumber)` unique constraint instead of a per-comment FK.

```csharp
public class CommentThread
{
    public long Id { get; init; }
    public long ReviewId { get; init; }
    public int LineNumber { get; init; }
    public DateTime CreatedAt { get; init; }

    public Review Review { get; set; } = null!;
    public ICollection<ThreadReply> Replies { get; set; } = [];
}
```

### ThreadReply.cs

```csharp
public class ThreadReply
{
    public long Id { get; init; }
    public long ThreadId { get; init; }
    public string? Content { get; init; }
    public bool IsAiReply { get; init; }
    public DateTime CreatedAt { get; init; }

    public CommentThread Thread { get; set; } = null!;
}
```

### SubmissionEvent.cs

One row per lifecycle transition. Used for tracing and the CorrelationId audit trail.

```csharp
public class SubmissionEvent
{
    public long Id { get; init; }
    public long SubmissionId { get; init; }
    public SubmissionEventType EventType { get; init; }
    public Guid CorrelationId { get; init; }
    public string? Metadata { get; init; }  // JSONB — free-form extra data per event
    public DateTime OccurredAt { get; init; }

    public Submission Submission { get; set; } = null!;
}
```

### ReviewMetrics.cs

Written once after a review completes. Provides aggregate-friendly performance data without parsing JSONB.

```csharp
public class ReviewMetrics
{
    public long Id { get; init; }
    public long SubmissionId { get; init; }
    public string? ModelId { get; init; }           // e.g. "deepseek-coder:6.7b"
    public int QueueWaitMs { get; init; }           // Submitted → ProcessingStarted
    public int ReviewDurationMs { get; init; }      // ProcessingStarted → ReviewCompleted
    public int TotalDurationMs { get; init; }       // Submitted → ReviewCompleted
    public int CommentCount { get; init; }
    public int CodeLengthChars { get; init; }
    public DateTime CreatedAt { get; init; }

    public Submission Submission { get; set; } = null!;
}
```

---

## EF Configurations (`Configurations/`)

Rules applied to every configuration class:
- `UseIdentityByDefaultColumn()` for all `long` PKs
- `HasConversion<int>()` for all enum properties
- `ToJson(...)` for JSONB-mapped owned types
- Cascade deletes on all FK relationships
- Table name is explicit PascalCase plural matching entity name
- **No index declarations** — all indexes live in SQL migration scripts

### SubmissionConfiguration.cs

```csharp
public class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).UseIdentityByDefaultColumn();

        builder.OwnsOne(s => s.Payload, b => b.ToJson("Payload"));

        builder.Property(s => s.Status).HasConversion<int>().IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();

        builder.HasOne(s => s.Review)
               .WithOne(r => r.Submission)
               .HasForeignKey<Review>(r => r.SubmissionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Events)
               .WithOne(e => e.Submission)
               .HasForeignKey(e => e.SubmissionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Metrics)
               .WithOne(m => m.Submission)
               .HasForeignKey<ReviewMetrics>(m => m.SubmissionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("Submissions");
    }
}
```

### ReviewConfiguration.cs

```csharp
public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).UseIdentityByDefaultColumn();
        builder.Property(r => r.Summary);
        builder.Property(r => r.CreatedAt).IsRequired();

        builder.OwnsMany(r => r.Comments, b => b.ToJson("Comments"));

        builder.HasMany(r => r.Threads)
               .WithOne(t => t.Review)
               .HasForeignKey(t => t.ReviewId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("Reviews");
    }
}
```

### CommentThreadConfiguration.cs

```csharp
public class CommentThreadConfiguration : IEntityTypeConfiguration<CommentThread>
{
    public void Configure(EntityTypeBuilder<CommentThread> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).UseIdentityByDefaultColumn();
        builder.Property(t => t.LineNumber).IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();

        builder.HasMany(t => t.Replies)
               .WithOne(r => r.Thread)
               .HasForeignKey(r => r.ThreadId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("CommentThreads");
    }
}
```

### ThreadReplyConfiguration.cs

```csharp
public class ThreadReplyConfiguration : IEntityTypeConfiguration<ThreadReply>
{
    public void Configure(EntityTypeBuilder<ThreadReply> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).UseIdentityByDefaultColumn();
        builder.Property(r => r.IsAiReply).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.ToTable("ThreadReplies");
    }
}
```

### SubmissionEventConfiguration.cs

```csharp
public class SubmissionEventConfiguration : IEntityTypeConfiguration<SubmissionEvent>
{
    public void Configure(EntityTypeBuilder<SubmissionEvent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityByDefaultColumn();
        builder.Property(e => e.EventType).HasConversion<int>().IsRequired();
        builder.Property(e => e.CorrelationId).IsRequired();
        builder.Property(e => e.Metadata).HasColumnType("jsonb");
        builder.Property(e => e.OccurredAt).IsRequired();
        builder.ToTable("SubmissionEvents");
    }
}
```

### ReviewMetricsConfiguration.cs

```csharp
public class ReviewMetricsConfiguration : IEntityTypeConfiguration<ReviewMetrics>
{
    public void Configure(EntityTypeBuilder<ReviewMetrics> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).UseIdentityByDefaultColumn();
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.ToTable("ReviewMetrics");
    }
}
```

---

## DbContext (`Context/RoasterDbContext.cs`)

```csharp
public class RoasterDbContext : DbContext
{
    public RoasterDbContext(DbContextOptions<RoasterDbContext> options) : base(options) { }

    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<CommentThread> CommentThreads => Set<CommentThread>();
    public DbSet<ThreadReply> ThreadReplies => Set<ThreadReply>();
    public DbSet<SubmissionEvent> SubmissionEvents => Set<SubmissionEvent>();
    public DbSet<ReviewMetrics> ReviewMetrics => Set<ReviewMetrics>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(RoasterDbContext).Assembly);

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<Submission>()
            .Where(e => e.State == EntityState.Modified))
            entry.Entity.UpdatedAt = DateTime.UtcNow;

        return base.SaveChangesAsync(ct);
    }
}
```