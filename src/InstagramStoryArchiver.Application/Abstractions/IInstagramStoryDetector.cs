using InstagramStoryArchiver.Domain.Models;

namespace InstagramStoryArchiver.Application.Abstractions;

public interface IInstagramStoryDetector
{
    Task<IReadOnlyList<InstagramStoryMedia>> DetectStoriesAsync(string username, CancellationToken cancellationToken);
}
