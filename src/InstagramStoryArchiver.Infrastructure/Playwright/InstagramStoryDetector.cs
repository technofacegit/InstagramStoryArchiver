using InstagramStoryArchiver.Application.Abstractions;
using InstagramStoryArchiver.Application.Options;
using InstagramStoryArchiver.Application.Utilities;
using InstagramStoryArchiver.Domain.Enums;
using InstagramStoryArchiver.Domain.Exceptions;
using InstagramStoryArchiver.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using IAppClock = InstagramStoryArchiver.Application.Abstractions.IClock;

namespace InstagramStoryArchiver.Infrastructure.Playwright;

public sealed class InstagramStoryDetector : IInstagramStoryDetector
{
    private readonly IInstagramBrowserService _browserService;
    private readonly IInstagramSessionService _sessionService;
    private readonly IInstagramStoryResponseParser _parser;
    private readonly IAppClock _clock;
    private readonly InstagramOptions _options;
    private readonly ILogger<InstagramStoryDetector> _logger;

    public InstagramStoryDetector(
        IInstagramBrowserService browserService,
        IInstagramSessionService sessionService,
        IInstagramStoryResponseParser parser,
        IAppClock clock,
        IOptions<InstagramOptions> options,
        ILogger<InstagramStoryDetector> logger)
    {
        _browserService = browserService;
        _sessionService = sessionService;
        _parser = parser;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<InstagramStoryMedia>> DetectStoriesAsync(
        string username,
        CancellationToken cancellationToken)
    {
        var normalized = UsernameNormalizer.Normalize(username);
        var collected = new Dictionary<string, InstagramStoryMedia>(StringComparer.Ordinal);
        var captureEnabled = false;
        IPage? page = null;
        EventHandler<IResponse>? responseHandler = null;

        void Ingest(IEnumerable<InstagramStoryMedia> items)
        {
            if (!captureEnabled)
            {
                return;
            }

            foreach (var item in items)
            {
                if (!string.IsNullOrWhiteSpace(item.Username)
                    && !string.Equals(item.Username, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "Ignoring story media for unexpected username {Actual} (expected {Expected})",
                        item.Username,
                        normalized);
                    continue;
                }

                if (!IsPlausibleActiveStory(item))
                {
                    _logger.LogInformation(
                        "Skipping non-story/expired media for {Username}, StoryId={StoryId}, PublishedAt={PublishedAt}",
                        normalized,
                        item.StoryId,
                        item.PublishedAt);
                    continue;
                }

                var normalizedItem = new InstagramStoryMedia
                {
                    StoryId = item.StoryId,
                    InstagramStoryId = item.InstagramStoryId,
                    Username = normalized,
                    MediaType = item.MediaType,
                    MediaUrl = item.MediaUrl,
                    PublishedAt = item.PublishedAt
                };
                collected[normalizedItem.StoryId] = normalizedItem;
            }
        }

        try
        {
            page = await _browserService.CreatePageAsync(cancellationToken);

            responseHandler = async (_, response) =>
            {
                try
                {
                    if (!captureEnabled)
                    {
                        return;
                    }

                    var contentType = response.Headers.TryGetValue("content-type", out var ct) ? ct : string.Empty;
                    var url = response.Url;

                    if (!LooksLikeStoryApiResponse(url, contentType))
                    {
                        return;
                    }

                    if (response.Status is < 200 or >= 300)
                    {
                        return;
                    }

                    string body;
                    try
                    {
                        body = await response.TextAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Could not read response body from {Url}", UrlSanitizer.SanitizeForLog(url));
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(body) || body.Length < 20)
                    {
                        return;
                    }

                    Ingest(_parser.Parse(body, normalized));
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error while processing network response during story detection.");
                }
            };

            page.Response += responseHandler;

            var profileUrl = $"{_options.BaseUrl.TrimEnd('/')}/{normalized}/";
            _logger.LogInformation("Opening profile {Url}", profileUrl);

            IResponse? navigationResponse;
            try
            {
                navigationResponse = await page.GotoAsync(
                    profileUrl,
                    new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            }
            catch (TimeoutException ex)
            {
                throw new InstagramNavigationException($"Navigation timeout opening profile for {normalized}.", ex);
            }

            await _sessionService.DetectSessionProblemsAsync(page.Url, cancellationToken);

            if (navigationResponse is not null && navigationResponse.Status == 404)
            {
                throw new InstagramStoryDetectionException($"Profile not found for '{normalized}'.");
            }

            // Allow SPA hydration. Do NOT capture profile feed/post GraphQL here.
            await page.WaitForTimeoutAsync(2500);

            // Enable capture only for the story-open phase (not profile grid posts).
            collected.Clear();
            captureEnabled = true;

            var opened = await TryOpenStoryViewerAsync(page, normalized, cancellationToken);
            if (!opened)
            {
                _logger.LogInformation(
                    "DOM story ring not found for {Username}. Trying direct stories URL.",
                    normalized);
                opened = await TryOpenStoriesByDirectUrlAsync(page, normalized, cancellationToken);
            }

            if (!opened)
            {
                captureEnabled = false;
                collected.Clear();
                _logger.LogInformation("No active story detected for {Username}", normalized);
                return Array.Empty<InstagramStoryMedia>();
            }

            _logger.LogInformation("Story viewer open for {Username}; collecting story media only.", normalized);

            await _sessionService.DetectSessionProblemsAsync(page.Url, cancellationToken);
            await page.WaitForTimeoutAsync(3000);

            if (page.Url.Contains($"/stories/{normalized}", StringComparison.OrdinalIgnoreCase))
            {
                Ingest(await ExtractMediaFromStoryViewerDomAsync(page, normalized));
            }

            await AdvanceThroughStoriesAsync(page, normalized, cancellationToken);

            if (page.Url.Contains($"/stories/{normalized}", StringComparison.OrdinalIgnoreCase))
            {
                Ingest(await ExtractMediaFromStoryViewerDomAsync(page, normalized));
            }

            await page.WaitForTimeoutAsync(1500);

            try
            {
                await page.Keyboard.PressAsync("Escape");
            }
            catch (PlaywrightException ex)
            {
                _logger.LogDebug(ex, "Could not close story viewer with Escape.");
            }

            _logger.LogInformation(
                "Collected {Count} story media items for {Username}",
                collected.Count,
                normalized);

            return collected.Values.ToList();
        }
        catch (Exception ex) when (ex is InstagramSessionExpiredException or InstagramChallengeDetectedException)
        {
            throw;
        }
        catch (PlaywrightException ex)
        {
            throw new InstagramStoryDetectionException($"Story detection failed for '{normalized}'.", ex);
        }
        finally
        {
            if (page is not null && responseHandler is not null)
            {
                page.Response -= responseHandler;
            }

            if (page is not null)
            {
                await page.CloseAsync();
            }
        }
    }

    private async Task<bool> TryOpenStoryViewerAsync(IPage page, string username, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 1) Explicit story href (older UI).
        var storyHref = page.Locator($"a[href*='/stories/{username}']").First;
        if (await storyHref.CountAsync() > 0 && await storyHref.IsVisibleAsync())
        {
            _logger.LogInformation("Active story link found for {Username}. Clicking.", username);
            await storyHref.ClickAsync();
            return await WaitForStoryViewerAsync(page, username);
        }

        // 2) Any /stories/ link that belongs to this user.
        var anyStoryLinks = page.Locator(InstagramLocators.ProfileStoryLinkSelector);
        var linkCount = await anyStoryLinks.CountAsync();
        for (var i = 0; i < linkCount; i++)
        {
            var href = await anyStoryLinks.Nth(i).GetAttributeAsync("href");
            if (href is not null && href.Contains($"/stories/{username}", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Active story href found for {Username}. Clicking.", username);
                await anyStoryLinks.Nth(i).ClickAsync();
                return await WaitForStoryViewerAsync(page, username);
            }
        }

        // 3) Modern UI: canvas story ring in header — click the nearest button/parent.
        var canvas = page.Locator(InstagramLocators.ProfileHeaderCanvasSelector).First;
        if (await canvas.CountAsync() > 0 && await canvas.IsVisibleAsync())
        {
            _logger.LogInformation("Story ring canvas found for {Username}. Clicking ring.", username);
            try
            {
                var clickable = canvas.Locator("xpath=ancestor::*[@role='button'][1]");
                if (await clickable.CountAsync() > 0)
                {
                    await clickable.ClickAsync();
                }
                else
                {
                    await canvas.ClickAsync();
                }

                if (await WaitForStoryViewerAsync(page, username))
                {
                    return true;
                }
            }
            catch (PlaywrightException ex)
            {
                _logger.LogDebug(ex, "Canvas click did not open story viewer.");
            }
        }

        // 4) Click profile photo in header (often opens story when ring is active).
        var photo = page.Locator(InstagramLocators.ProfileHeaderPhotoButtonSelector).First;
        if (await photo.CountAsync() > 0 && await photo.IsVisibleAsync())
        {
            _logger.LogInformation("Clicking profile photo for {Username} to open story.", username);
            try
            {
                var button = photo.Locator("xpath=ancestor::*[@role='button'][1]");
                if (await button.CountAsync() > 0)
                {
                    await button.ClickAsync();
                }
                else
                {
                    await photo.ClickAsync();
                }

                if (await WaitForStoryViewerAsync(page, username))
                {
                    return true;
                }
            }
            catch (PlaywrightException ex)
            {
                _logger.LogDebug(ex, "Profile photo click did not open story viewer.");
            }
        }

        return false;
    }

    private async Task<bool> TryOpenStoriesByDirectUrlAsync(IPage page, string username, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var storiesUrl = $"{_options.BaseUrl.TrimEnd('/')}/stories/{username}/";
        _logger.LogInformation("Navigating directly to {Url}", storiesUrl);

        try
        {
            await page.GotoAsync(storiesUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "Timeout opening direct stories URL for {Username}", username);
            return false;
        }

        await page.WaitForTimeoutAsync(2000);
        await _sessionService.DetectSessionProblemsAsync(page.Url, cancellationToken);

        if (page.Url.Contains($"/stories/{username}", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Direct stories URL stayed on viewer for {Username}", username);
            return true;
        }

        // Instagram may redirect to profile when there is no active story.
        _logger.LogInformation(
            "Direct stories URL redirected away for {Username} (url={Url}). Treating as no active story.",
            username,
            page.Url);
        return false;
    }

    private async Task<bool> WaitForStoryViewerAsync(IPage page, string username)
    {
        try
        {
            await page.WaitForURLAsync(
                url => url.Contains("/stories/", StringComparison.OrdinalIgnoreCase),
                new PageWaitForURLOptions { Timeout = _options.StoryLoadTimeoutSeconds * 1000 });
        }
        catch (TimeoutException)
        {
            _logger.LogDebug("Story viewer URL did not appear for {Username} within timeout.", username);
        }

        if (page.Url.Contains($"/stories/{username}", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Some builds open a dialog without changing URL immediately.
        var media = page.Locator(InstagramLocators.StoryViewerMediaSelector);
        if (await media.CountAsync() > 0)
        {
            return true;
        }

        return false;
    }

    private async Task AdvanceThroughStoriesAsync(IPage page, string username, CancellationToken cancellationToken)
    {
        for (var i = 0; i < 15; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (page.Url.Contains("/stories/", StringComparison.OrdinalIgnoreCase)
                && !page.Url.Contains($"/stories/{username}", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Story viewer moved away from {Username}. Stopping advancement.", username);
                break;
            }

            // Prefer keyboard right-arrow; more reliable than aria-labelled Next across locales.
            try
            {
                await page.Keyboard.PressAsync("ArrowRight");
                await page.WaitForTimeoutAsync(900);
            }
            catch (PlaywrightException)
            {
                break;
            }

            var nextButton = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = InstagramLocators.NextStoryButtonAria });
            if (await nextButton.CountAsync() > 0)
            {
                try
                {
                    await nextButton.First.ClickAsync(new LocatorClickOptions { Timeout = 1500 });
                    await page.WaitForTimeoutAsync(700);
                }
                catch (PlaywrightException)
                {
                    // Keyboard may already have advanced.
                }
            }
        }
    }

    private async Task<IReadOnlyList<InstagramStoryMedia>> ExtractMediaFromStoryViewerDomAsync(IPage page, string username)
    {
        var results = new List<InstagramStoryMedia>();
        try
        {
            var videos = page.Locator("video[src], video source[src]");
            var videoCount = await videos.CountAsync();
            for (var i = 0; i < videoCount; i++)
            {
                var src = await videos.Nth(i).GetAttributeAsync("src");
                if (string.IsNullOrWhiteSpace(src)
                    || src.StartsWith("blob:", StringComparison.OrdinalIgnoreCase)
                    || !LooksLikeCdnMedia(src))
                {
                    continue;
                }

                var key = StoryFingerprint.Create(null, username, src, publishedAt: null);
                results.Add(new InstagramStoryMedia
                {
                    StoryId = key,
                    InstagramStoryId = null,
                    Username = username,
                    MediaType = StoryMediaType.Video,
                    MediaUrl = src,
                    PublishedAt = null
                });
            }

            var images = page.Locator(InstagramLocators.StoryViewerMediaSelector);
            var imageCount = await images.CountAsync();
            for (var i = 0; i < imageCount; i++)
            {
                var tag = await images.Nth(i).EvaluateAsync<string>("el => el.tagName.toLowerCase()");
                if (tag == "video")
                {
                    continue;
                }

                var src = await images.Nth(i).GetAttributeAsync("src");
                if (string.IsNullOrWhiteSpace(src))
                {
                    // Prefer largest candidate from srcset when src is missing.
                    var srcset = await images.Nth(i).GetAttributeAsync("srcset");
                    src = PickBestFromSrcSet(srcset);
                }

                if (string.IsNullOrWhiteSpace(src)
                    || src.StartsWith("blob:", StringComparison.OrdinalIgnoreCase)
                    || !LooksLikeCdnMedia(src))
                {
                    continue;
                }

                // Skip tiny UI icons / profile thumbs.
                if (src.Contains("s150x150", StringComparison.OrdinalIgnoreCase)
                    || src.Contains("s50x50", StringComparison.OrdinalIgnoreCase)
                    || src.Contains("s320x320", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var key = StoryFingerprint.Create(null, username, src, publishedAt: null);
                results.Add(new InstagramStoryMedia
                {
                    StoryId = key,
                    InstagramStoryId = null,
                    Username = username,
                    MediaType = StoryMediaType.Image,
                    MediaUrl = src,
                    PublishedAt = null
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "DOM media extraction failed for {Username}", username);
        }

        return results;
    }

    private static string? PickBestFromSrcSet(string? srcset)
    {
        if (string.IsNullOrWhiteSpace(srcset))
        {
            return null;
        }

        string? best = null;
        var bestWidth = -1;
        foreach (var part in srcset.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var bits = part.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (bits.Length == 0)
            {
                continue;
            }

            var url = bits[0];
            var width = 0;
            if (bits.Length > 1 && bits[1].EndsWith('w') && int.TryParse(bits[1][..^1], out var parsed))
            {
                width = parsed;
            }

            if (width >= bestWidth)
            {
                bestWidth = width;
                best = url;
            }
        }

        return best;
    }

    private bool IsPlausibleActiveStory(InstagramStoryMedia item)
    {
        // Stories expire ~24h after publish. Reject older feed posts mistaken as stories.
        if (item.PublishedAt is null)
        {
            return true;
        }

        var age = _clock.UtcNow - item.PublishedAt.Value;
        return age <= TimeSpan.FromHours(36) && age >= TimeSpan.FromHours(-2);
    }

    private static bool LooksLikeCdnMedia(string url)
        => url.Contains("cdninstagram", StringComparison.OrdinalIgnoreCase)
           || url.Contains("fbcdn", StringComparison.OrdinalIgnoreCase)
           || url.Contains(".mp4", StringComparison.OrdinalIgnoreCase)
           || url.Contains(".jpg", StringComparison.OrdinalIgnoreCase)
           || url.Contains(".webp", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeStoryApiResponse(string url, string contentType)
    {
        if (!url.Contains("instagram.com", StringComparison.OrdinalIgnoreCase)
            && !url.Contains("facebook.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (url.Contains(".js", StringComparison.OrdinalIgnoreCase)
            && !url.Contains("graphql", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Exclude common feed/post endpoints that are not stories.
        if (url.Contains("user_timeline", StringComparison.OrdinalIgnoreCase)
            || url.Contains("feed/timeline", StringComparison.OrdinalIgnoreCase)
            || url.Contains("media_info", StringComparison.OrdinalIgnoreCase)
            || url.Contains("/p/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var isStoryPath =
            url.Contains("/stories/", StringComparison.OrdinalIgnoreCase)
            || url.Contains("reels_media", StringComparison.OrdinalIgnoreCase)
            || url.Contains("user_story", StringComparison.OrdinalIgnoreCase)
            || url.Contains("story_tray", StringComparison.OrdinalIgnoreCase)
            || url.Contains("graphql", StringComparison.OrdinalIgnoreCase)
            || url.Contains("/api/", StringComparison.OrdinalIgnoreCase);

        return isStoryPath;
    }
}
