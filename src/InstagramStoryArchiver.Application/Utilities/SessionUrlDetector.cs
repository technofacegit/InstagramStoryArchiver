using System.Text.RegularExpressions;

namespace InstagramStoryArchiver.Application.Utilities;

public static partial class SessionUrlDetector
{
    public static bool IsLoginUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        return LoginUrlRegex().IsMatch(url);
    }

    public static bool IsChallengeOrCheckpointUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        return ChallengeUrlRegex().IsMatch(url);
    }

    [GeneratedRegex(@"instagram\.com/(accounts/login|accounts/onetap|challenge|auth_platform)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LoginUrlRegex();

    [GeneratedRegex(@"instagram\.com/(challenge|accounts/suspended|consent|auth_platform/challenge|/checkpoint/)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ChallengeUrlRegex();
}
