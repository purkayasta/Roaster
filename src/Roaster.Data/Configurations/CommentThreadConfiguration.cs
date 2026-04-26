using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roaster.Data.Models;

namespace Roaster.Data.Configurations;

public class CommentThreadConfiguration : IEntityTypeConfiguration<CommentThread>
{
    public void Configure(EntityTypeBuilder<CommentThread> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).UseIdentityByDefaultColumn();
        builder.Property(t => t.LineNumber).IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();

        builder.HasMany(t => t.Replies)
               .WithOne(r => r.Thread)
               .HasForeignKey(r => r.ThreadId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("CommentThreads");
    }
}
