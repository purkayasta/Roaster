# Phase 1 — API

Build the core data layer and API. No events, no AI, no messaging — just the schema, EF setup, and REST endpoints working end-to-end.

---

## 1.A — Models

**Project:** `Roaster.Data`

**Pre-checklist:**
- [ ] Add `Roaster.Data` to `Roaster.slnx` under solution folder `Backend`

**SDK:** `Microsoft.NET.Sdk` (net10.0, class library)

**NuGet:**
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.0" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
```

**Folder structure:**
```
src/Roaster.Data/
  Models/
    Enums.cs
    CodePayload.cs
    Submission.cs
    Review.cs
    ReviewLineComment.cs
    CommentThread.cs
    ThreadReply.cs
  Configurations/
    SubmissionConfiguration.cs
    ReviewConfiguration.cs
    CommentThreadConfiguration.cs
    ThreadReplyConfiguration.cs
  Context/
    RoasterDbContext.cs
```

### Rules

- IDs are `long` — maps to `bigserial` in PostgreSQL
- `init` for properties that are set once; `set` only for `Status` and `UpdatedAt`
- No `= string.Empty` — use `required` for mandatory strings, `string?` for optional
- Enum backing values are explicit non-sequential ints (10/20/30) to allow future insertions without renumbering
- Navigation properties use `set` — EF Core sets them during materialisation

### Enums.cs

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
```

### CodePayload.cs

Stored as JSONB inside `Submissions` — not a separate table.

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
    public required CodePayload Payload { get; init; }  // JSONB
    public SubmissionStatus Status { get; set; } = SubmissionStatus.Pending;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; set; }

    public Review? Review { get; set; }
}
```

### Review.cs

```csharp
public class Review
{
    public long Id { get; init; }
    public long SubmissionId { get; init; }
    public string? Summary { get; init; }
    public List<ReviewLineComment> Comments { get; init; } = [];  // JSONB
    public DateTime CreatedAt { get; init; }

    public Submission Submission { get; set; } = null!;
    public ICollection<CommentThread> Threads { get; set; } = [];
}
```

### ReviewLineComment.cs

Not a table — lives inside the `Reviews."Comments"` JSONB column.

```csharp
public record ReviewLineComment
{
    public int LineNumber { get; init; }
    public string? Comment { get; init; }
    public ReviewSeverity Severity { get; init; }
}
```

### CommentThread.cs

References a line in `Review.Comments` via `(ReviewId, LineNumber)` — no per-comment FK needed.

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

---

## 1.B — DbContext and Configurations

### EF Configuration Rules

- `UseIdentityByDefaultColumn()` for all `long` PKs
- `HasConversion<int>()` for all enums
- `OwnsOne(...).ToJson(...)` for JSONB owned types
- `OwnsMany(...).ToJson(...)` for JSONB owned collections
- Cascade deletes on all FK relationships
- Table names: PascalCase plurals matching entity name exactly
- **No index declarations** — all indexes live in SQL migration scripts (Phase 6)

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

### RoasterDbContext.cs

```csharp
public class RoasterDbContext : DbContext
{
    public RoasterDbContext(DbContextOptions<RoasterDbContext> options) : base(options) { }

    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<CommentThread> CommentThreads => Set<CommentThread>();
    public DbSet<ThreadReply> ThreadReplies => Set<ThreadReply>();

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

---

## 1.C — Contracts

**Project:** `Roaster.Contracts`

**Pre-checklist:**
- [ ] Add `Roaster.Contracts` to `Roaster.slnx` under solution folder `Backend`

**SDK:** `Microsoft.NET.Sdk` (net10.0, class library). No NuGet deps.

Only response DTOs in this phase — no events yet (those come in Phase 2).

```
src/Roaster.Contracts/
  Dtos/
    SubmissionDto.cs
    ReviewDto.cs
    ReviewCommentDto.cs
    ThreadReplyDto.cs
```

```csharp
// SubmissionDto.cs — Payload/code excluded to keep polling responses lightweight
public record SubmissionDto(
    long Id,
    string? Language,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);

// ReviewDto.cs
public record ReviewDto(
    long Id,
    long SubmissionId,
    string? Summary,
    DateTime CreatedAt,
    List<ReviewCommentDto> Comments);

// ReviewCommentDto.cs — ThreadId populated if a thread exists for this line
public record ReviewCommentDto(
    int LineNumber,
    string? Comment,
    string Severity,
    long? ThreadId);

// ThreadReplyDto.cs
public record ThreadReplyDto(
    long Id,
    string? Content,
    bool IsAiReply,
    DateTime CreatedAt);
```

---

## 1.D — API

**Project:** `Roaster.Api`

**Pre-checklist:**
- [ ] Add `Roaster.Api` to `Roaster.slnx` under solution folder `Backend`

**SDK:** `Microsoft.NET.Sdk.Web` (net10.0)

**NuGet:**
```xml
<PackageReference Include="Serilog.AspNetCore" Version="8.0.3" />
<PackageReference Include="Serilog.Sinks.Console" Version="5.0.0" />
<PackageReference Include="Scalar.AspNetCore" Version="2.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.NpgSql" Version="8.0.2" />
```

**Project references:** `Roaster.Data`, `Roaster.Contracts`

**Folder structure:**
```
src/Roaster.Api/
  Endpoints/
    SubmissionEndpoints.cs
    ReviewEndpoints.cs
    ThreadEndpoints.cs
  Models/
    SubmissionRequest.cs
    ReplyRequest.cs
  Program.cs
  appsettings.json
  appsettings.Development.json
```

### Program.cs

```csharp
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

builder.Services.AddDbContext<RoasterDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(p =>
        p.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>()!)
         .AllowAnyHeader()
         .AllowAnyMethod()));

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Postgres")!);

builder.Services.AddOpenApi();

var app = builder.Build();
app.UseCors();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
    app.MapScalarApiReference();

app.MapOpenApi();
app.MapSubmissionEndpoints();
app.MapReviewEndpoints();
app.MapThreadEndpoints();
app.MapHealthChecks("/health");

await app.RunAsync();
```

### Request Models

Validation via data annotations — no inline `if` guards in handlers.

```csharp
// Models/SubmissionRequest.cs
public class SubmissionRequest
{
    [Required]
    [StringLength(50000, MinimumLength = 1)]
    public string Code { get; init; } = string.Empty;

    [Required]
    [AllowedValues("csharp","javascript","typescript","python","java","go","rust","cpp","sql")]
    public string Language { get; init; } = string.Empty;
}

// Models/ReplyRequest.cs
public class ReplyRequest
{
    [Required]
    [StringLength(5000, MinimumLength = 1)]
    public string Content { get; init; } = string.Empty;
}
```

### Endpoints

Each file in `Endpoints/` is a static class with a `MapXxxEndpoints(this IEndpointRouteBuilder)` extension. All route and handler code in that one file.

#### SubmissionEndpoints.cs

**POST /api/submissions**
1. Validate via annotations → `400` on failure
2. Sanitize: `Regex.Replace(code, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "")`
3. Save `Submission` with `Payload = new CodePayload { Content = sanitized, Language }`, `Status = Pending`
4. Return `202 Accepted`, `SubmissionDto`, `Location: /api/submissions/{id}`

> In this phase, Status stays `Pending` permanently — no AI consumer yet. That changes in Phase 4.

**GET /api/submissions/{id}**
- Returns `SubmissionDto` (Payload excluded)
- `404` if not found

**GET /api/submissions**
- Paged list ordered by `CreatedAt DESC`
- Query params: `?page=1&pageSize=20`

#### ReviewEndpoints.cs

**GET /api/submissions/{id}/review**
- `404` if submission not found
- `204 No Content` if `Status ≠ Completed`
- Load `Review` with `Threads`, map JSONB comments → `List<ReviewCommentDto>` with thread IDs joined by `LineNumber`
- Return `200 ReviewDto`

#### ThreadEndpoints.cs

**POST /api/submissions/{submissionId}/reviews/{reviewId}/lines/{lineNumber}/threads**
1. Validate `reviewId` belongs to `submissionId`
2. Validate `lineNumber` exists in `Review.Comments` JSONB
3. Upsert `CommentThread (ReviewId, LineNumber)`
4. Save user `ThreadReply` (IsAiReply = false)
5. Return `202 Accepted` with `{ "threadId": ..., "replyId": ... }`

**GET /api/threads/{threadId}/replies**
- Returns `List<ThreadReplyDto>` ordered by `CreatedAt ASC`
- `404` if not found

### API Endpoints Summary

| Method | Path | Response |
|--------|------|----------|
| POST | `/api/submissions` | `202 SubmissionDto` |
| GET | `/api/submissions/{id}` | `200 SubmissionDto` / `404` |
| GET | `/api/submissions` | `200 SubmissionDto[]` paged |
| GET | `/api/submissions/{id}/review` | `200 ReviewDto` / `204` / `404` |
| POST | `/api/submissions/{id}/reviews/{reviewId}/lines/{line}/threads` | `202 { threadId, replyId }` |
| GET | `/api/threads/{threadId}/replies` | `200 ThreadReplyDto[]` / `404` |
| GET | `/health` | health JSON |
| GET | `/scalar/v1` | Scalar API docs (dev only) |