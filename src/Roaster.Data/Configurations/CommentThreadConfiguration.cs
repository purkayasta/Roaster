/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roaster.Data.Models;

namespace Roaster.Data.Configurations;

public sealed class CommentThreadConfiguration : IEntityTypeConfiguration<CommentThread>
{
    public void Configure(EntityTypeBuilder<CommentThread> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).UseIdentityByDefaultColumn();
        builder.Property(t => t.LineNumber).IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.Status).IsRequired().HasColumnType("smallint");

        builder.HasMany(t => t.Replies)
               .WithOne(r => r.Thread)
               .HasForeignKey(r => r.ThreadId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("CommentThreads");
    }
}
