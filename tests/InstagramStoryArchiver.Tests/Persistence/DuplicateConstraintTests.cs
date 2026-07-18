using InstagramStoryArchiver.Domain.Entities;
using InstagramStoryArchiver.Domain.Enums;
using InstagramStoryArchiver.Domain.Exceptions;
using InstagramStoryArchiver.Infrastructure.Persistence;
using InstagramStoryArchiver.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InstagramStoryArchiver.Tests.Persistence;

public class DuplicateConstraintTests
{
    [Fact]
    public async Task SaveChanges_ThrowsDuplicateStoryException_OnUniqueViolation()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();

        var user = new MonitoredInstagramUser
        {
            Id = Guid.NewGuid(),
            Username = "demo",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.MonitoredUsers.Add(user);
        await db.SaveChangesAsync();

        var repo = new ArchivedStoryRepository(db);
        var story1 = CreateStory(user.Id, "demo", "key-1");
        await repo.AddAsync(story1, CancellationToken.None);
        await repo.SaveChangesAsync(CancellationToken.None);

        var story2 = CreateStory(user.Id, "demo", "key-1");
        await repo.AddAsync(story2, CancellationToken.None);
        await Assert.ThrowsAsync<DuplicateStoryException>(() => repo.SaveChangesAsync(CancellationToken.None));
    }

    private static ArchivedInstagramStory CreateStory(Guid userId, string username, string key)
        => new()
        {
            Id = Guid.NewGuid(),
            MonitoredUserId = userId,
            Username = username,
            StoryKey = key,
            MediaType = StoryMediaType.Image,
            DownloadedAt = DateTimeOffset.UtcNow,
            Status = StoryDownloadStatus.Downloaded
        };
}
