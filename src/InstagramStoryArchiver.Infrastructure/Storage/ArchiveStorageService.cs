using InstagramStoryArchiver.Application.Abstractions;
using InstagramStoryArchiver.Application.Options;
using InstagramStoryArchiver.Application.Utilities;
using InstagramStoryArchiver.Domain.Enums;
using Microsoft.Extensions.Options;

namespace InstagramStoryArchiver.Infrastructure.Storage;

public sealed class ArchiveStorageService : IArchiveStorageService
{
    private readonly StorageOptions _options;

    public ArchiveStorageService(IOptions<StorageOptions> options)
    {
        _options = options.Value;
        Directory.CreateDirectory(Path.GetFullPath(_options.ArchiveRootPath));
        Directory.CreateDirectory(Path.GetFullPath(_options.TemporaryRootPath));
    }

    public string BuildRelativePath(string username, DateTimeOffset publishedAt, string storyKey, string extension)
        => ArchivePathBuilder.BuildRelativePath(username, publishedAt, storyKey, extension);

    public string GetAbsolutePath(string relativePath)
        => Path.GetFullPath(Path.Combine(_options.ArchiveRootPath, relativePath));

    public string CreateTemporaryFilePath(string extension)
    {
        var ext = extension.StartsWith('.') ? extension : "." + extension;
        var fileName = $"{Guid.NewGuid():N}{ext}.tmp";
        return Path.Combine(Path.GetFullPath(_options.TemporaryRootPath), fileName);
    }

    public async Task CommitTempFileAsync(string tempFilePath, string relativePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var absolutePath = GetAbsolutePath(relativePath);
        var directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        // Prefer atomic move; fall back to copy+delete across volumes.
        try
        {
            File.Move(tempFilePath, absolutePath);
        }
        catch (IOException)
        {
            await using (var source = File.OpenRead(tempFilePath))
            await using (var destination = File.Create(absolutePath))
            {
                await source.CopyToAsync(destination, cancellationToken);
            }

            File.Delete(tempFilePath);
        }
    }

    public string ResolveExtension(StoryMediaType mediaType, string? contentType, string mediaUrl)
        => ArchivePathBuilder.ResolveExtension(mediaType, contentType, mediaUrl);

    public string SanitizeFileName(string fileName)
        => ArchivePathBuilder.SanitizeFileName(fileName);
}
