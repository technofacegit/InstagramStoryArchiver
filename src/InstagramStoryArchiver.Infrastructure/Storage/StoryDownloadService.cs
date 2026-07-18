using System.Security.Cryptography;
using InstagramStoryArchiver.Application.Abstractions;
using InstagramStoryArchiver.Application.Options;
using InstagramStoryArchiver.Application.Utilities;
using InstagramStoryArchiver.Domain.Exceptions;
using InstagramStoryArchiver.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Polly;
using Polly.Retry;
using IAppClock = InstagramStoryArchiver.Application.Abstractions.IClock;

namespace InstagramStoryArchiver.Infrastructure.Storage;

public sealed class StoryDownloadService : IStoryDownloadService
{
    private readonly IInstagramBrowserService _browserService;
    private readonly IArchiveStorageService _archiveStorage;
    private readonly IAppClock _clock;
    private readonly InstagramOptions _options;
    private readonly ILogger<StoryDownloadService> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public StoryDownloadService(
        IInstagramBrowserService browserService,
        IArchiveStorageService archiveStorage,
        IAppClock clock,
        IOptions<InstagramOptions> options,
        ILogger<StoryDownloadService> logger)
    {
        _browserService = browserService;
        _archiveStorage = archiveStorage;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
        _retryPolicy = Policy
            .Handle<PlaywrightException>()
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(
                2,
                attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                (ex, delay, attempt, _) =>
                {
                    _logger.LogWarning(
                        ex,
                        "Transient download failure (attempt {Attempt}). Retrying in {Delay}.",
                        attempt,
                        delay);
                });
    }

    public async Task<StoryDownloadResult> DownloadAsync(InstagramStoryMedia media, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _browserService.InitializeAsync(cancellationToken);

        var maxBytes = _options.MaximumDownloadSizeMb * 1024L * 1024L;
        IAPIResponse response;

        try
        {
            response = await _retryPolicy.ExecuteAsync(async () =>
                await _browserService.RequestContext.GetAsync(media.MediaUrl, new APIRequestContextOptions
                {
                    Headers = new Dictionary<string, string>
                    {
                        ["Referer"] = "https://www.instagram.com/",
                        ["Accept"] = "*/*"
                    }
                }));
        }
        catch (Exception ex)
        {
            throw new InstagramMediaDownloadException(
                $"Failed to download media from {UrlSanitizer.SanitizeForLog(media.MediaUrl)}.",
                ex);
        }

        if (!response.Ok)
        {
            throw new InstagramMediaDownloadException(
                $"Media download returned HTTP {response.Status} for {UrlSanitizer.SanitizeForLog(media.MediaUrl)}.");
        }

        var contentType = response.Headers.TryGetValue("content-type", out var ct)
            ? ct
            : string.Empty;

        if (!IsAllowedContentType(contentType))
        {
            throw new InstagramMediaDownloadException(
                $"Disallowed content type '{contentType}' for {UrlSanitizer.SanitizeForLog(media.MediaUrl)}.");
        }

        var body = await response.BodyAsync();
        if (body.Length == 0)
        {
            throw new InstagramMediaDownloadException("Downloaded media body was empty.");
        }

        if (body.Length > maxBytes)
        {
            throw new InstagramMediaDownloadException(
                $"Downloaded media exceeds maximum size of {_options.MaximumDownloadSizeMb} MB.");
        }

        if (LooksLikeHtml(body, contentType))
        {
            throw new InstagramMediaDownloadException(
                "Download response looks like an HTML error page, not media.");
        }

        var extension = _archiveStorage.ResolveExtension(media.MediaType, contentType, media.MediaUrl);
        var publishedAt = media.PublishedAt ?? _clock.UtcNow;
        var storyKey = StoryFingerprint.Create(
            media.InstagramStoryId ?? media.StoryId,
            media.Username,
            media.MediaUrl,
            media.PublishedAt);

        var relativePath = _archiveStorage.BuildRelativePath(media.Username, publishedAt, storyKey, extension);
        var tempPath = _archiveStorage.CreateTemporaryFilePath(extension);

        try
        {
            await File.WriteAllBytesAsync(tempPath, body, cancellationToken);

            await using (var stream = File.OpenRead(tempPath))
            {
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
                var fileSize = stream.Length;

                await _archiveStorage.CommitTempFileAsync(tempPath, relativePath, cancellationToken);

                _logger.LogInformation(
                    "Stored media at {Path} ({Size} bytes)",
                    relativePath,
                    fileSize);

                return new StoryDownloadResult(relativePath, fileSize, hash, extension);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up temp file {TempPath}", tempPath);
                }
            }
        }
    }

    private static bool IsAllowedContentType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var normalized = contentType.Split(';')[0].Trim().ToLowerInvariant();
        return normalized.StartsWith("image/", StringComparison.Ordinal)
            || normalized.StartsWith("video/", StringComparison.Ordinal)
            || normalized == "application/octet-stream";
    }

    private static bool LooksLikeHtml(byte[] body, string contentType)
    {
        if (contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var sampleLength = Math.Min(body.Length, 64);
        var sample = System.Text.Encoding.UTF8.GetString(body, 0, sampleLength).TrimStart().ToLowerInvariant();
        return sample.StartsWith("<!doctype html", StringComparison.Ordinal)
            || sample.StartsWith("<html", StringComparison.Ordinal);
    }
}
