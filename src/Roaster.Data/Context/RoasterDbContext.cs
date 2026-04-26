using Microsoft.EntityFrameworkCore;
using Roaster.Data.Models;

namespace Roaster.Data.Context;

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
