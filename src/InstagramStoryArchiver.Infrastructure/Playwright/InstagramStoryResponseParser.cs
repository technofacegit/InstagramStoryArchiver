using System.Text.Json;
using InstagramStoryArchiver.Application.Abstractions;
using InstagramStoryArchiver.Application.Utilities;
using InstagramStoryArchiver.Domain.Enums;
using InstagramStoryArchiver.Domain.Models;
using Microsoft.Extensions.Logging;

namespace InstagramStoryArchiver.Infrastructure.Playwright;

public sealed class InstagramStoryResponseParser : IInstagramStoryResponseParser
{
    private readonly ILogger<InstagramStoryResponseParser> _logger;

    public InstagramStoryResponseParser(ILogger<InstagramStoryResponseParser> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<InstagramStoryMedia> Parse(string json, string expectedUsername)
    {
        var results = new List<InstagramStoryMedia>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            using var document = JsonDocument.Parse(json);
            Walk(document.RootElement, expectedUsername, results, seen, depth: 0);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Instagram JSON response. Continuing.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error while scanning Instagram JSON response.");
        }

        return results;
    }

    private void Walk(
        JsonElement element,
        string expectedUsername,
        List<InstagramStoryMedia> results,
        HashSet<string> seen,
        int depth)
    {
        if (depth > 40)
        {
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                TryExtractMedia(element, expectedUsername, results, seen);
                foreach (var property in element.EnumerateObject())
                {
                    Walk(property.Value, expectedUsername, results, seen, depth + 1);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item, expectedUsername, results, seen, depth + 1);
                }
                break;
        }
    }

    private void TryExtractMedia(
        JsonElement element,
        string expectedUsername,
        List<InstagramStoryMedia> results,
        HashSet<string> seen)
    {
        try
        {
            // Require a media container signal to avoid treating CDN candidate nodes as stories.
            var hasMediaContainer =
                HasProperty(element, "video_versions")
                || HasProperty(element, "video_url")
                || HasProperty(element, "image_versions2")
                || HasProperty(element, "image_versions")
                || HasProperty(element, "media_type");

            if (!hasMediaContainer)
            {
                return;
            }

            var videoUrl = FindString(element, "video_url", "video_versions");
            var imageUrl = FindBestImageUrl(element);
            var mediaUrl = videoUrl ?? imageUrl;
            if (string.IsNullOrWhiteSpace(mediaUrl))
            {
                return;
            }

            if (!LooksLikeMediaUrl(mediaUrl))
            {
                return;
            }

            var ownerUsername = FindOwnerUsername(element);
            if (!string.IsNullOrWhiteSpace(ownerUsername)
                && !string.Equals(ownerUsername, expectedUsername, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Skipping media owned by {Owner}, expected {Expected}",
                    ownerUsername,
                    expectedUsername);
                return;
            }

            var username = string.IsNullOrWhiteSpace(ownerUsername)
                ? expectedUsername
                : ownerUsername.ToLowerInvariant();

            var storyId = FindStringScalar(element, "pk", "id", "strong_id__");
            var publishedAt = FindTimestamp(element);
            var mediaType = !string.IsNullOrWhiteSpace(videoUrl)
                ? StoryMediaType.Video
                : StoryMediaType.Image;

            if (string.IsNullOrWhiteSpace(storyId))
            {
                storyId = StoryFingerprint.Create(null, username, mediaUrl, publishedAt);
            }

            if (!seen.Add(storyId))
            {
                return;
            }

            results.Add(new InstagramStoryMedia
            {
                StoryId = storyId,
                InstagramStoryId = storyId,
                Username = username,
                MediaType = mediaType,
                MediaUrl = mediaUrl,
                PublishedAt = publishedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Skipped a JSON node during story media extraction.");
        }
    }

    private static bool HasProperty(JsonElement element, string name)
        => element.TryGetProperty(name, out _);

    private static string? FindOwnerUsername(JsonElement element)
    {
        if (element.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object)
        {
            if (user.TryGetProperty("username", out var username) && username.ValueKind == JsonValueKind.String)
            {
                return username.GetString();
            }
        }

        if (element.TryGetProperty("owner", out var owner) && owner.ValueKind == JsonValueKind.Object)
        {
            if (owner.TryGetProperty("username", out var username) && username.ValueKind == JsonValueKind.String)
            {
                return username.GetString();
            }
        }

        if (element.TryGetProperty("username", out var direct) && direct.ValueKind == JsonValueKind.String)
        {
            return direct.GetString();
        }

        return null;
    }

    private static string? FindBestImageUrl(JsonElement element)
    {
        foreach (var key in new[] { "image_versions2", "image_versions" })
        {
            if (!element.TryGetProperty(key, out var versions))
            {
                continue;
            }

            if (versions.ValueKind == JsonValueKind.Object
                && versions.TryGetProperty("candidates", out var candidates)
                && candidates.ValueKind == JsonValueKind.Array)
            {
                var best = SelectBestCandidateUrl(candidates);
                if (!string.IsNullOrWhiteSpace(best))
                {
                    return best;
                }
            }

            if (versions.ValueKind == JsonValueKind.Array)
            {
                var best = SelectBestCandidateUrl(versions);
                if (!string.IsNullOrWhiteSpace(best))
                {
                    return best;
                }
            }
        }

        // Avoid treating arbitrary "url" properties on nested candidate nodes as story media.
        return FindStringScalar(element, "display_url", "image_url");
    }

    private static string? FindString(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                var best = SelectBestCandidateUrl(value);
                if (!string.IsNullOrWhiteSpace(best))
                {
                    return best;
                }
            }
        }

        return null;
    }

    private static string? SelectBestCandidateUrl(JsonElement array)
    {
        string? bestUrl = null;
        var bestArea = -1;

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!item.TryGetProperty("url", out var urlProp) || urlProp.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var url = urlProp.GetString();
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            var width = item.TryGetProperty("width", out var w) && w.TryGetInt32(out var wi) ? wi : 0;
            var height = item.TryGetProperty("height", out var h) && h.TryGetInt32(out var hi) ? hi : 0;
            var area = width * height;
            if (area >= bestArea)
            {
                bestArea = area;
                bestUrl = url;
            }
        }

        return bestUrl;
    }

    private static string? FindStringScalar(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var s = value.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    return s;
                }
            }

            if (value.ValueKind == JsonValueKind.Number)
            {
                return value.ToString();
            }
        }

        return null;
    }

    private static DateTimeOffset? FindTimestamp(JsonElement element)
    {
        foreach (var key in new[] { "taken_at", "expiring_at", "device_timestamp", "timestamp" })
        {
            if (!element.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var unix))
            {
                if (unix > 1_000_000_000_000)
                {
                    unix /= 1000;
                }

                return DateTimeOffset.FromUnixTimeSeconds(unix);
            }

            if (value.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static bool LooksLikeMediaUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        var path = uri.AbsolutePath.ToLowerInvariant();
        return path.Contains(".jpg", StringComparison.Ordinal)
            || path.Contains(".jpeg", StringComparison.Ordinal)
            || path.Contains(".png", StringComparison.Ordinal)
            || path.Contains(".webp", StringComparison.Ordinal)
            || path.Contains(".mp4", StringComparison.Ordinal)
            || path.Contains("/v/", StringComparison.Ordinal)
            || path.Contains("/t51.", StringComparison.Ordinal)
            || path.Contains("/t50.", StringComparison.Ordinal)
            || uri.Host.Contains("cdninstagram", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Contains("fbcdn", StringComparison.OrdinalIgnoreCase);
    }
}
