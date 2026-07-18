using InstagramStoryArchiver.Application.Abstractions;
using InstagramStoryArchiver.Application.Options;
using InstagramStoryArchiver.Application.Utilities;
using InstagramStoryArchiver.Domain.Exceptions;
using InstagramStoryArchiver.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace InstagramStoryArchiver.Infrastructure.Playwright;

public sealed class InstagramStoryDetector : IInstagramStoryDetector
{
    private readonly IInstagramBrowserService _browserService;
    private readonly IInstagramSessionService _sessionService;
    private readonly IInstagramStoryResponseParser _parser;
    private readonly InstagramOptions _options;
    private readonly ILogger<InstagramStoryDetector> _logger;

    public InstagramStoryDetector(
        IInstagramBrowserService browserService,
        IInstagramSessionService sessionService,
        IInstagramStoryResponseParser parser,
        IOptions<InstagramOptions> options,
        ILogger<InstagramStoryDetector> logger)
    {
        _browserService = browserService;
        _sessionService = sessionService;
        _parser = parser;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<InstagramStoryMedia>> DetectStoriesAsync(
        string username,
        CancellationToken cancellationToken)
    {
        var normalized = UsernameNormalizer.Normalize(username);
        var collected = new Dictionary<string, InstagramStoryMedia>(StringComparer.Ordinal);
        IPage? page = null;
        EventHandler<IResponse>? responseHandler = null;

        try
        {
            page = await _browserService.CreatePageAsync(cancellationToken);

            responseHandler = async (_, response) =>
            {
                try
                {
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

                    var parsed = _parser.Parse(body, normalized);
                    foreach (var item in parsed)
                    {
                        if (!string.Equals(item.Username, normalized, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogWarning(
                                "Ignoring story media for unexpected username {Actual} (expected {Expected})",
                                item.Username,
                                normalized);
                            continue;
                        }

                        collected[item.StoryId] = item;
                    }
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

            var storyLink = page.Locator($"a[href*='/stories/{normalized}']").First;
            var hasStoryLink = await storyLink.CountAsync() > 0;

            if (!hasStoryLink)
            {
                var genericStoryLinks = page.Locator(InstagramLocators.ProfileStoryRingSelector);
                var count = await genericStoryLinks.CountAsync();
                for (var i = 0; i < count; i++)
                {
                    var href = await genericStoryLinks.Nth(i).GetAttributeAsync("href");
                    if (href is not null
                        && href.Contains($"/stories/{normalized}", StringComparison.OrdinalIgnoreCase))
                    {
                        storyLink = genericStoryLinks.Nth(i);
                        hasStoryLink = true;
                        break;
                    }
                }
            }

            if (!hasStoryLink)
            {
                _logger.LogInformation("No active story detected via DOM for {Username}", normalized);
                return Array.Empty<InstagramStoryMedia>();
            }

            _logger.LogInformation("Active story ring found for {Username}. Opening story viewer.", normalized);
            await storyLink.ClickAsync();

            try
            {
                await page.WaitForURLAsync(
                    url => url.Contains("/stories/", StringComparison.OrdinalIgnoreCase),
                    new PageWaitForURLOptions { Timeout = _options.StoryLoadTimeoutSeconds * 1000 });
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Story viewer URL did not appear for {Username} within timeout.", normalized);
            }

            await _sessionService.DetectSessionProblemsAsync(page.Url, cancellationToken);
            await page.WaitForTimeoutAsync(2000);

            // Advance through stories belonging to this user to capture network payloads.
            for (var i = 0; i < 15; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!page.Url.Contains($"/stories/{normalized}", StringComparison.OrdinalIgnoreCase)
                    && page.Url.Contains("/stories/", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Story viewer moved away from {Username}. Stopping advancement.", normalized);
                    break;
                }

                var nextButton = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = InstagramLocators.NextStoryButtonAria });
                if (await nextButton.CountAsync() == 0)
                {
                    break;
                }

                try
                {
                    await nextButton.First.ClickAsync(new LocatorClickOptions { Timeout = 2000 });
                    await page.WaitForTimeoutAsync(800);
                }
                catch (PlaywrightException)
                {
                    break;
                }
            }

            try
            {
                await page.Keyboard.PressAsync("Escape");
            }
            catch (PlaywrightException ex)
            {
                _logger.LogDebug(ex, "Could not close story viewer with Escape.");
            }

            _logger.LogInformation(
                "Collected {Count} story media items for {Username} from network responses",
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

    private static bool LooksLikeStoryApiResponse(string url, string contentType)
    {
        if (!url.Contains("instagram.com", StringComparison.OrdinalIgnoreCase)
            && !url.Contains("facebook.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Ignore static JS bundles; they are not story media payloads.
        if (url.Contains(".js", StringComparison.OrdinalIgnoreCase)
            && !url.Contains("graphql", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var isJsonContent = contentType.Contains("json", StringComparison.OrdinalIgnoreCase);
        var isApiPath =
            url.Contains("/api/", StringComparison.OrdinalIgnoreCase)
            || url.Contains("graphql", StringComparison.OrdinalIgnoreCase)
            || url.Contains("/stories/", StringComparison.OrdinalIgnoreCase)
            || url.Contains("reel", StringComparison.OrdinalIgnoreCase)
            || url.Contains("feed", StringComparison.OrdinalIgnoreCase)
            || url.Contains("media", StringComparison.OrdinalIgnoreCase);

        return isJsonContent || isApiPath;
    }
}
