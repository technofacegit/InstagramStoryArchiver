using System.Text.RegularExpressions;

namespace InstagramStoryArchiver.Application.Utilities;

public static partial class UrlSanitizer
{
    public static string SanitizeForLog(string? url, int maxLength = 80)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var sanitized = QueryStringRegex().Replace(url, string.Empty);
        if (sanitized.Length <= maxLength)
        {
            return sanitized;
        }

        return sanitized[..maxLength] + "...";
    }

    [GeneratedRegex(@"\?.*$", RegexOptions.CultureInvariant)]
    private static partial Regex QueryStringRegex();
}
