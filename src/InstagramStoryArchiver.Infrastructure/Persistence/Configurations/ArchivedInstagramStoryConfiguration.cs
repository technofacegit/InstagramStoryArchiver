using InstagramStoryArchiver.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstagramStoryArchiver.Infrastructure.Persistence.Configurations;

public sealed class ArchivedInstagramStoryConfiguration : IEntityTypeConfiguration<ArchivedInstagramStory>
{
    public void Configure(EntityTypeBuilder<ArchivedInstagramStory> builder)
    {
        builder.ToTable("ArchivedInstagramStories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Username).HasMaxLength(64).IsRequired();
        builder.Property(x => x.StoryKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.InstagramStoryId).HasMaxLength(128);
        builder.Property(x => x.OriginalMediaUrl).HasMaxLength(2000);
        builder.Property(x => x.StoredRelativePath).HasMaxLength(500);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.Property(x => x.Sha256).HasMaxLength(64);
        builder.HasIndex(x => new { x.Username, x.StoryKey }).IsUnique();
        builder.HasOne(x => x.MonitoredUser)
            .WithMany(x => x.Stories)
            .HasForeignKey(x => x.MonitoredUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
