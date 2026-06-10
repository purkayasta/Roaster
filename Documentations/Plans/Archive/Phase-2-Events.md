# Phase 2 — Events

Add the messaging layer on top of the working API. Introduces RabbitMQ via MassTransit, event contracts, the Consumer worker, and the `SubmissionEvent` tracking table. The API endpoints are updated to publish events after saving to DB.

---

## 2.A — Event Models

**Project:** `Roaster.Data` (additive changes)

Add `SubmissionEvent` entity and its enum to the existing data project.

### New enum value in `Enums.cs`

```csharp
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

### New model: SubmissionEvent.cs

```csharp
public class SubmissionEvent
{
    public long Id { get; init; }
    public long SubmissionId { get; init; }
    public SubmissionEventType EventType { get; init; }
    public Guid CorrelationId { get; init; }
    public string? Metadata { get; init; }  // JSONB — free-form extra data
    public DateTime OccurredAt { get; init; }

    public Submission Submission { get; set; } = null!;
}
```

---

## 2.B — Update DbContext and Configurations

**Project:** `Roaster.Data` (additive changes)

### New: SubmissionEventConfiguration.cs

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

### Update `Submission.cs` — add Events nav property

```csharp
// Add to Submission class:
public ICollection<SubmissionEvent> Events { get; set; } = [];
```

### Update `SubmissionConfiguration.cs` — wire the new nav property

```csharp
// Add to Configure():
builder.HasMany(s => s.Events)
       .WithOne(e => e.Submission)
       .HasForeignKey(e => e.SubmissionId)
       .OnDelete(DeleteBehavior.Cascade);
```

### Update `RoasterDbContext.cs` — add DbSet

```csharp
public DbSet<SubmissionEvent> SubmissionEvents => Set<SubmissionEvent>();
```

---

## 2.C — Event Contracts

**Project:** `Roaster.Contracts` (additive changes)

Add event POCOs under a new `Events/` folder. Events carry **IDs only** — never code or large strings. Consumers fetch what they need from DB directly.

```
src/Roaster.Contracts/
  Events/                   ← new folder
    CodeSubmittedEvent.cs
    UserThreadReplyEvent.cs
    ReviewCompletedEvent.cs
  Dtos/                     ← unchanged from Phase 1
    ...
```

```csharp
// Events/CodeSubmittedEvent.cs
public record CodeSubmittedEvent(
    long SubmissionId,
    Guid CorrelationId,
    DateTime SubmittedAt);

// Events/UserThreadReplyEvent.cs
public record UserThreadReplyEvent(
    long ThreadId,
    long SubmissionId,
    int LineNumber,
    Guid CorrelationId,
    DateTime SubmittedAt);

// Events/ReviewCompletedEvent.cs
public record ReviewCompletedEvent(
    long SubmissionId,
    long ReviewId,
    Guid CorrelationId,
    DateTime CompletedAt);
```

---

## 2.D — Consumer Project

**Project:** `Roaster.Consumer` (new project)

**Pre-checklist:**
- [ ] Add `Roaster.Consumer` to `Roaster.slnx` under solution folder `Backend`

**SDK:** `Microsoft.NET.Sdk.Worker` (net10.0)

**NuGet:**
```xml
<PackageReference Include="MassTransit" Version="8.3.6" />
<PackageReference Include="MassTransit.RabbitMQ" Version="8.3.6" />
<PackageReference Include="Serilog.Extensions.Hosting" Version="8.0.0" />
<PackageReference Include="Serilog.Sinks.Console" Version="5.0.0" />
```

**Project references:** `Roaster.Data`, `Roaster.Contracts`

**Folder structure:**
```
src/Roaster.Consumer/
  Consumers/
    CodeSubmittedConsumer.cs
    UserThreadReplyConsumer.cs
  Program.cs
  appsettings.json
```

### Program.cs

```csharp
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

builder.Services.AddDbContext<RoasterDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<CodeSubmittedConsumer>();
    x.AddConsumer<UserThreadReplyConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Uri"]);

        cfg.ReceiveEndpoint("code-submitted", e =>
        {
            e.ConfigureConsumer<CodeSubmittedConsumer>(ctx);
            e.UseMessageRetry(r => r.Exponential(5,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(60),
                TimeSpan.FromSeconds(5)));
        });

        cfg.ReceiveEndpoint("user-thread-reply", e =>
        {
            e.ConfigureConsumer<UserThreadReplyConsumer>(ctx);
            e.UseMessageRetry(r => r.Intervals(
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)));
        });
    });
});

await builder.Build().RunAsync();
```

### CodeSubmittedConsumer.cs

At this phase, the consumer receives the event, loads the submission, updates its status, records a `SubmissionEvent`, and publishes `ReviewCompletedEvent`. The AI review is a stub — it will be filled in during Phase 4.

```csharp
public class CodeSubmittedConsumer(RoasterDbContext db, IPublishEndpoint publisher,
    ILogger<CodeSubmittedConsumer> logger) : IConsumer<CodeSubmittedEvent>
{
    public async Task Consume(ConsumeContext<CodeSubmittedEvent> context)
    {
        var msg = context.Message;

        var submission = await db.Submissions.FindAsync(msg.SubmissionId);
        if (submission is null)
        {
            logger.LogWarning("Submission {Id} not found", msg.SubmissionId);
            return;
        }

        db.SubmissionEvents.Add(new SubmissionEvent
        {
            SubmissionId  = submission.Id,
            EventType     = SubmissionEventType.ProcessingStarted,
            CorrelationId = msg.CorrelationId,
            OccurredAt    = DateTime.UtcNow
        });

        submission.Status = SubmissionStatus.Processing;
        await db.SaveChangesAsync();

        // TODO Phase 4: call AI reviewer here and save Review + ReviewMetrics

        submission.Status = SubmissionStatus.Completed;
        db.SubmissionEvents.Add(new SubmissionEvent
        {
            SubmissionId  = submission.Id,
            EventType     = SubmissionEventType.ReviewCompleted,
            CorrelationId = msg.CorrelationId,
            OccurredAt    = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        await publisher.Publish(new ReviewCompletedEvent(
            submission.Id, 0, msg.CorrelationId, DateTime.UtcNow));
    }
}
```

### UserThreadReplyConsumer.cs

Same pattern — stub for now, AI reply filled in Phase 4.

```csharp
public class UserThreadReplyConsumer(RoasterDbContext db,
    ILogger<UserThreadReplyConsumer> logger) : IConsumer<UserThreadReplyEvent>
{
    public async Task Consume(ConsumeContext<UserThreadReplyEvent> context)
    {
        var msg = context.Message;

        var thread = await db.CommentThreads.FindAsync(msg.ThreadId);
        if (thread is null)
        {
            logger.LogWarning("Thread {Id} not found", msg.ThreadId);
            return;
        }

        // TODO Phase 4: call AI reviewer and save ThreadReply

        db.SubmissionEvents.Add(new SubmissionEvent
        {
            SubmissionId  = msg.SubmissionId,
            EventType     = SubmissionEventType.AiReplied,
            CorrelationId = msg.CorrelationId,
            OccurredAt    = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}
```

---

## 2.E — Update API to Publish Events

**Project:** `Roaster.Api` (additive changes)

Add MassTransit + RabbitMQ to the API so it can publish events.

### New NuGet packages for `Roaster.Api`:

```xml
<PackageReference Include="MassTransit" Version="8.3.6" />
<PackageReference Include="MassTransit.RabbitMQ" Version="8.3.6" />
<PackageReference Include="AspNetCore.HealthChecks.Rabbitmq" Version="8.0.2" />
```

### Update Program.cs — add MassTransit registration:

```csharp
builder.Services.AddMassTransit(x =>
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Uri"]);
        cfg.ConfigureEndpoints(ctx);
    }));

// Add RabbitMQ health check alongside the existing NpgSql check
builder.Services.AddHealthChecks()
    .AddNpgSql(...)
    .AddRabbitMQ(rabbitConnectionString: builder.Configuration["RabbitMQ:Uri"]!);
```

### Update `SubmissionEndpoints.cs` — publish after save:

```csharp
// Inject IPublishEndpoint into the endpoint handler
// After SaveChangesAsync:
await publisher.Publish(new CodeSubmittedEvent(
    submission.Id,
    Guid.NewGuid(),       // CorrelationId — Phase 3 will wire this properly
    submission.CreatedAt));
```

### Update `ThreadEndpoints.cs` — publish after reply save:

```csharp
await publisher.Publish(new UserThreadReplyEvent(
    thread.Id,
    submissionId,
    lineNumber,
    Guid.NewGuid(),       // CorrelationId — Phase 3 will wire this properly
    DateTime.UtcNow));
```