/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roaster.Data.Models;

namespace Roaster.Data.Configurations;

public sealed class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).UseIdentityByDefaultColumn();

        builder.Property(s => s.SessionId).IsRequired();
        builder.HasIndex(s => s.SessionId);

        builder.HasOne(s => s.Session)
               .WithMany(ses => ses.Submissions)
               .HasForeignKey(s => s.SessionId)
               .OnDelete(DeleteBehavior.Cascade);

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

        builder.ToTable("Submissions");
    }
}
