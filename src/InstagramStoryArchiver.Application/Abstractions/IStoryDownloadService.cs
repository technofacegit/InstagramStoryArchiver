using InstagramStoryArchiver.Domain.Models;

namespace InstagramStoryArchiver.Application.Abstractions;

public sealed record StoryDownloadResult(
    string RelativePath,
    long FileSize,
    string Sha256,
    string Extension);

public interface IStoryDownloadService
{
    Task<StoryDownloadResult> DownloadAsync(InstagramStoryMedia media, CancellationToken cancellationToken);
}
