using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roaster.Data.Models;

namespace Roaster.Data.Configurations;

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

        builder.HasOne(s => s.Metrics)
               .WithOne(m => m.Submission)
               .HasForeignKey<ReviewMetrics>(m => m.SubmissionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Events)
               .WithOne(e => e.Submission)
               .HasForeignKey(e => e.SubmissionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("Submissions");
    }
}
