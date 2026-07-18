using InstagramStoryArchiver.Domain.Enums;

namespace InstagramStoryArchiver.Application.Utilities;

public static class ArchivePathBuilder
{
    private static readonly char[] ExtraInvalidChars = [' ', ':', '*', '?', '"', '<', '>', '|', '/', '\\'];

    public static string BuildRelativePath(
        string username,
        DateTimeOffset publishedAt,
        string storyKey,
        string extension)
    {
        var safeUsername = SanitizeFileName(username.ToLowerInvariant());
        var safeKey = SanitizeFileName(storyKey);
        var safeExtension = extension.StartsWith('.') ? extension[1..] : extension;
        safeExtension = SanitizeFileName(safeExtension);

        var fileName = $"{safeUsername}_{publishedAt:HHmmss}_{safeKey}.{safeExtension}";
        return Path.Combine(
            safeUsername,
            publishedAt.Year.ToString("D4"),
            publishedAt.Month.ToString("D2"),
            publishedAt.Day.ToString("D2"),
            fileName);
    }

    public static string SanitizeFileName(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var sanitized = fileName.Trim();
        foreach (var c in Path.GetInvalidFileNameChars().Concat(ExtraInvalidChars).Distinct())
        {
            sanitized = sanitized.Replace(c, '_');
        }

        return sanitized;
    }

    public static string ResolveExtension(StoryMediaType mediaType, string? contentType, string mediaUrl)
    {
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            var normalized = contentType.Split(';')[0].Trim().ToLowerInvariant();
            var fromContentType = normalized switch
            {
                "image/jpeg" => ".jpg",
                "image/jpg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                "video/mp4" => ".mp4",
                "video/webm" => ".webm",
                "video/quicktime" => ".mov",
                _ => null
            };

            if (fromContentType is not null)
            {
                return fromContentType;
            }
        }

        if (mediaType == StoryMediaType.Video)
        {
            return ".mp4";
        }

        if (mediaType == StoryMediaType.Image)
        {
            return GuessExtensionFromUrl(mediaUrl) ?? ".jpg";
        }

        return GuessExtensionFromUrl(mediaUrl) ?? ".bin";
    }

    private static string? GuessExtensionFromUrl(string mediaUrl)
    {
        try
        {
            var path = new Uri(mediaUrl).AbsolutePath;
            var ext = Path.GetExtension(path);
            if (string.IsNullOrWhiteSpace(ext))
            {
                return null;
            }

            return ext.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or ".mp4" or ".webm" or ".mov" => ext.ToLowerInvariant(),
                _ => null
            };
        }
        catch (UriFormatException)
        {
            return null;
        }
    }
}
