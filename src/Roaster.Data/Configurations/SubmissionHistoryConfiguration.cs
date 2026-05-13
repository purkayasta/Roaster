using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roaster.Data.Models;

namespace Roaster.Data.Configurations;

public class SubmissionHistoryConfiguration : IEntityTypeConfiguration<SubmissionHistory>
{
    public void Configure(EntityTypeBuilder<SubmissionHistory> builder)
    {
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).UseIdentityByDefaultColumn();

        builder.OwnsOne(h => h.Payload, b => b.ToJson("Payload"));

        builder.Property(h => h.ChangedAt).IsRequired();

        builder.HasOne(h => h.Submission)
               .WithMany(s => s.History)
               .HasForeignKey(h => h.SubmissionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.Session)
               .WithMany()
               .HasForeignKey(h => h.SessionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("SubmissionHistory");
    }
}
