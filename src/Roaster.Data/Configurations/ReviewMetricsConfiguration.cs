using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roaster.Data.Models;

namespace Roaster.Data.Configurations;

public class ReviewMetricsConfiguration : IEntityTypeConfiguration<ReviewMetrics>
{
    public void Configure(EntityTypeBuilder<ReviewMetrics> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).UseIdentityByDefaultColumn();
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.ToTable("ReviewMetrics");
    }
}
