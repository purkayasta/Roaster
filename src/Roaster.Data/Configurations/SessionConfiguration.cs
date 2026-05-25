/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roaster.Data.Models;

namespace Roaster.Data.Configurations;

public sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasValueGenerator<GuidV7Generator>();
        builder.Property(s => s.Name).HasMaxLength(255).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.IsActive).HasDefaultValue(true).IsRequired();

        builder.ToTable("Sessions");
    }
}
