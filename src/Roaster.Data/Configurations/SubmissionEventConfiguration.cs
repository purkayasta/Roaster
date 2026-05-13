using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roaster.Data.Models;

namespace Roaster.Data.Configurations;

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
