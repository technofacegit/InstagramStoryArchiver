using InstagramStoryArchiver.Domain.Entities;

namespace InstagramStoryArchiver.Application.Abstractions;

public interface IArchivedStoryRepository
{
    Task<bool> ExistsAsync(string username, string storyKey, CancellationToken cancellationToken);
    Task AddAsync(ArchivedInstagramStory story, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
