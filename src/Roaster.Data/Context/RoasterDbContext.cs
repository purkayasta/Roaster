using Microsoft.EntityFrameworkCore;
using Roaster.Data.Models;

namespace Roaster.Data.Context;

public class RoasterDbContext : DbContext
{
    public RoasterDbContext(DbContextOptions<RoasterDbContext> options) : base(options) { }

    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<SubmissionHistory> SubmissionHistory => Set<SubmissionHistory>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<CommentThread> CommentThreads => Set<CommentThread>();
    public DbSet<ThreadReply> ThreadReplies => Set<ThreadReply>();
    public DbSet<SubmissionEvent> SubmissionEvents => Set<SubmissionEvent>();
    public DbSet<ReviewMetrics> ReviewMetrics => Set<ReviewMetrics>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(RoasterDbContext).Assembly);

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        CaptureSubmissionHistory();
        return await base.SaveChangesAsync(ct);
    }

    private void CaptureSubmissionHistory()
    {
        var modifiedSubmissions = ChangeTracker.Entries<Submission>()
            .Where(e => e.State == EntityState.Modified)
            .ToList();

        if (modifiedSubmissions.Count == 0) return;

        foreach (var entry in modifiedSubmissions)
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;

            var payloadEntry = entry.References
                .FirstOrDefault(r => r.Metadata.Name == nameof(Submission.Payload))
                ?.TargetEntry;

            if (payloadEntry == null || !payloadEntry.Properties.Any(p => p.IsModified)) continue;

            var originalContent  = (string)payloadEntry.OriginalValues[nameof(CodePayload.Content)]!;
            var originalLanguage = (string)payloadEntry.OriginalValues[nameof(CodePayload.Language)]!;

            SubmissionHistory.Add(new SubmissionHistory
            {
                SubmissionId = entry.Entity.Id,
                SessionId    = entry.Entity.SessionId,
                Payload      = new CodePayload { Content = originalContent, Language = originalLanguage },
                ChangedAt    = DateTime.UtcNow
            });
        }
    }
}
