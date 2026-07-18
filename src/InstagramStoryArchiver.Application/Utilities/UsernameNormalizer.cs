using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace InstagramStoryArchiver.Application.Utilities;

public static partial class UsernameNormalizer
{
    public static string Normalize(string username)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        var normalized = username.Trim();
        if (normalized.StartsWith('@'))
        {
            normalized = normalized[1..];
        }

        normalized = normalized.ToLowerInvariant();

        if (!UsernameRegex().IsMatch(normalized))
        {
            throw new ArgumentException($"Invalid Instagram username: '{username}'.", nameof(username));
        }

        return normalized;
    }

    [GeneratedRegex("^[a-z0-9._]{1,30}$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernameRegex();
}
