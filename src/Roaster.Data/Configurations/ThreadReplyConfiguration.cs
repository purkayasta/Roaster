using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roaster.Data.Models;

namespace Roaster.Data.Configurations;

public class ThreadReplyConfiguration : IEntityTypeConfiguration<ThreadReply>
{
    public void Configure(EntityTypeBuilder<ThreadReply> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).UseIdentityByDefaultColumn();
        builder.Property(r => r.IsAiReply).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.ToTable("ThreadReplies");
    }
}
