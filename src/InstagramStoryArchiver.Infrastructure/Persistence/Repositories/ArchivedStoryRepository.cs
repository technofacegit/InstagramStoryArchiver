using InstagramStoryArchiver.Application.Abstractions;
using InstagramStoryArchiver.Domain.Entities;
using InstagramStoryArchiver.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InstagramStoryArchiver.Infrastructure.Persistence.Repositories;

public sealed class ArchivedStoryRepository : IArchivedStoryRepository
{
    private readonly AppDbContext _dbContext;

    public ArchivedStoryRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ExistsAsync(string username, string storyKey, CancellationToken cancellationToken)
    {
        return _dbContext.ArchivedStories.AnyAsync(
            x => x.Username == username && x.StoryKey == storyKey,
            cancellationToken);
    }

    public async Task AddAsync(ArchivedInstagramStory story, CancellationToken cancellationToken)
    {
        await _dbContext.ArchivedStories.AddAsync(story, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            var entry = _dbContext.ChangeTracker.Entries<ArchivedInstagramStory>()
                .FirstOrDefault(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            if (entry is not null)
            {
                entry.State = EntityState.Detached;
                throw new DuplicateStoryException(entry.Entity.Username, entry.Entity.StoryKey);
            }

            throw new DuplicateStoryException("unknown", "unknown");
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase);
    }
}
