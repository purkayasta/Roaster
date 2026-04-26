using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roaster.Data.Models;

namespace Roaster.Data.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).UseIdentityByDefaultColumn();
        builder.Property(r => r.Summary);
        builder.Property(r => r.CreatedAt).IsRequired();

        builder.OwnsMany(r => r.Comments, b => b.ToJson("Comments"));

        builder.HasMany(r => r.Threads)
               .WithOne(t => t.Review)
               .HasForeignKey(t => t.ReviewId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("Reviews");
    }
}
