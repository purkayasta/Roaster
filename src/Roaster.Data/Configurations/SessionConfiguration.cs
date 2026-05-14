using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roaster.Data.Models;

namespace Roaster.Data.Configurations;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedOnAdd();
        builder.Property(s => s.Name).HasMaxLength(255).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.IsActive).HasDefaultValue(true).IsRequired();

        builder.HasOne(s => s.Submission)
               .WithOne(sub => sub.Session)
               .HasForeignKey<Submission>(sub => sub.SessionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("Sessions");
    }
}
