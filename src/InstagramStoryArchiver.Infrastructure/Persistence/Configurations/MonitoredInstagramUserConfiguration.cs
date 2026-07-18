using InstagramStoryArchiver.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstagramStoryArchiver.Infrastructure.Persistence.Configurations;

public sealed class MonitoredInstagramUserConfiguration : IEntityTypeConfiguration<MonitoredInstagramUser>
{
    public void Configure(EntityTypeBuilder<MonitoredInstagramUser> builder)
    {
        builder.ToTable("MonitoredInstagramUsers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Username).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.Username).IsUnique();
        builder.Property(x => x.LastError).HasMaxLength(1000);
        builder.HasIndex(x => new { x.IsActive, x.NextCheckAt });
    }
}
