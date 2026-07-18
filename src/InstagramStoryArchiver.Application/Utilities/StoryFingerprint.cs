using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace InstagramStoryArchiver.Application.Utilities;

public static partial class StoryFingerprint
{
    public static string Create(
        string? instagramStoryId,
        string username,
        string mediaUrl,
        DateTimeOffset? publishedAt)
    {
        if (!string.IsNullOrWhiteSpace(instagramStoryId))
        {
            return instagramStoryId.Trim();
        }

        var stableMediaId = ExtractStableMediaId(mediaUrl);
        var publishedPart = publishedAt?.ToUnixTimeSeconds().ToString() ?? "unknown";
        var raw = $"{username.ToLowerInvariant()}|{stableMediaId}|{publishedPart}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string ExtractStableMediaId(string mediaUrl)
    {
        if (string.IsNullOrWhiteSpace(mediaUrl))
        {
            return "unknown";
        }

        var match = MediaIdRegex().Match(mediaUrl);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        try
        {
            var uri = new Uri(mediaUrl);
            var fileName = Path.GetFileNameWithoutExtension(uri.AbsolutePath);
            return string.IsNullOrWhiteSpace(fileName) ? mediaUrl : fileName;
        }
        catch (UriFormatException)
        {
            return mediaUrl;
        }
    }

    [GeneratedRegex(@"/(?:v/)?([A-Za-z0-9_-]{8,})", RegexOptions.CultureInvariant)]
    private static partial Regex MediaIdRegex();
}
