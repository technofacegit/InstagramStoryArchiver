using InstagramStoryArchiver.Domain.Enums;

namespace InstagramStoryArchiver.Application.Abstractions;

public interface IArchiveStorageService
{
    string BuildRelativePath(string username, DateTimeOffset publishedAt, string storyKey, string extension);
    string GetAbsolutePath(string relativePath);
    string CreateTemporaryFilePath(string extension);
    Task CommitTempFileAsync(string tempFilePath, string relativePath, CancellationToken cancellationToken);
    string ResolveExtension(StoryMediaType mediaType, string? contentType, string mediaUrl);
    string SanitizeFileName(string fileName);
}
