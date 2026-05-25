/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using Microsoft.EntityFrameworkCore;
using Roaster.Data.Models;

namespace Roaster.Data.Context;

public sealed class RoasterDbContext : DbContext
{
    public RoasterDbContext(DbContextOptions<RoasterDbContext> options) : base(options) { }

    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<CommentThread> CommentThreads => Set<CommentThread>();
    public DbSet<ThreadReply> ThreadReplies => Set<ThreadReply>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<ReviewMetrics> ReviewMetrics => Set<ReviewMetrics>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(RoasterDbContext).Assembly);

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<Submission>().Where(e => e.State == EntityState.Modified))
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        return await base.SaveChangesAsync(ct);
    }
}
