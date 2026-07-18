using InstagramStoryArchiver.Application.Abstractions;
using InstagramStoryArchiver.Application.Options;
using InstagramStoryArchiver.Application.Utilities;
using InstagramStoryArchiver.Domain.Entities;
using InstagramStoryArchiver.Domain.Enums;
using InstagramStoryArchiver.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InstagramStoryArchiver.Application.Services;

public sealed class StoryCheckOrchestrator : IStoryCheckOrchestrator
{
    private readonly IMonitoredUserRepository _userRepository;
    private readonly IArchivedStoryRepository _storyRepository;
    private readonly IInstagramSessionService _sessionService;
    private readonly IInstagramBrowserService _browserService;
    private readonly IInstagramStoryDetector _storyDetector;
    private readonly IStoryDownloadService _downloadService;
    private readonly IClock _clock;
    private readonly InstagramOptions _options;
    private readonly ILogger<StoryCheckOrchestrator> _logger;
    private readonly Random _random = new();

    public StoryCheckOrchestrator(
        IMonitoredUserRepository userRepository,
        IArchivedStoryRepository storyRepository,
        IInstagramSessionService sessionService,
        IInstagramBrowserService browserService,
        IInstagramStoryDetector storyDetector,
        IStoryDownloadService downloadService,
        IClock clock,
        IOptions<InstagramOptions> options,
        ILogger<StoryCheckOrchestrator> logger)
    {
        _userRepository = userRepository;
        _storyRepository = storyRepository;
        _sessionService = sessionService;
        _browserService = browserService;
        _storyDetector = storyDetector;
        _downloadService = downloadService;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<StoryCheckCycleResult> RunDueUsersAsync(CancellationToken cancellationToken)
    {
        if (!_sessionService.StorageStateExists())
        {
            throw new InstagramSessionExpiredException(
                $"Storage state file not found at '{_options.StorageStatePath}'. Run: dotnet run -- login");
        }

        await _browserService.InitializeAsync(cancellationToken);
        await _sessionService.EnsureSessionValidAsync(cancellationToken);

        var now = _clock.UtcNow;
        var dueUsers = await _userRepository.GetDueUsersAsync(now, cancellationToken);
        _logger.LogInformation("Found {Count} due users to check at {Now}", dueUsers.Count, now);

        var usersChecked = 0;
        var storiesDownloaded = 0;
        var storiesSkipped = 0;

        for (var i = 0; i < dueUsers.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var user = dueUsers[i];

            try
            {
                var result = await CheckMonitoredUserAsync(user, cancellationToken);
                usersChecked++;
                storiesDownloaded += result.Downloaded;
                storiesSkipped += result.SkippedDuplicates;
            }
            catch (Exception ex) when (ex is InstagramSessionExpiredException or InstagramChallengeDetectedException)
            {
                _logger.LogCritical(ex,
                    "Global Instagram session/challenge issue detected while checking {Username}. Stopping cycle. Re-run login manually.",
                    user.Username);
                return new StoryCheckCycleResult(usersChecked, storiesDownloaded, storiesSkipped, StoppedDueToSessionIssue: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed checking user {Username}. Continuing with remaining users.", user.Username);
                await RecordFailureAsync(user, ex.Message, cancellationToken);
            }

            if (i < dueUsers.Count - 1)
            {
                var delaySeconds = _random.Next(
                    _options.MinimumDelayBetweenUsersSeconds,
                    _options.MaximumDelayBetweenUsersSeconds + 1);
                _logger.LogInformation("Waiting {DelaySeconds}s before next user", delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }
        }

        return new StoryCheckCycleResult(usersChecked, storiesDownloaded, storiesSkipped, StoppedDueToSessionIssue: false);
    }

    public async Task<StoryCheckUserResult> CheckUserAsync(string username, CancellationToken cancellationToken)
    {
        var normalized = UsernameNormalizer.Normalize(username);
        var user = await _userRepository.GetByUsernameAsync(normalized, cancellationToken)
            ?? throw new InvalidOperationException($"User '{normalized}' is not monitored.");

        if (!_sessionService.StorageStateExists())
        {
            throw new InstagramSessionExpiredException(
                $"Storage state file not found at '{_options.StorageStatePath}'. Run: dotnet run -- login");
        }

        await _browserService.InitializeAsync(cancellationToken);
        await _sessionService.EnsureSessionValidAsync(cancellationToken);
        return await CheckMonitoredUserAsync(user, cancellationToken);
    }

    private async Task<StoryCheckUserResult> CheckMonitoredUserAsync(
        MonitoredInstagramUser user,
        CancellationToken cancellationToken)
    {
        var startedAt = _clock.UtcNow;
        _logger.LogInformation("Starting check for {Username} at {StartedAt}", user.Username, startedAt);

        try
        {
            var mediaItems = await _storyDetector.DetectStoriesAsync(user.Username, cancellationToken);
            var hadActiveStories = mediaItems.Count > 0;
            _logger.LogInformation(
                "User {Username}: active stories={HadStories}, detected items={Count}",
                user.Username,
                hadActiveStories,
                mediaItems.Count);

            var downloaded = 0;
            var skipped = 0;
            var failed = 0;

            foreach (var media in mediaItems)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var storyKey = StoryFingerprint.Create(
                    media.InstagramStoryId ?? media.StoryId,
                    media.Username,
                    media.MediaUrl,
                    media.PublishedAt);

                if (await _storyRepository.ExistsAsync(user.Username, storyKey, cancellationToken))
                {
                    skipped++;
                    _logger.LogInformation(
                        "Duplicate story skipped for {Username}, StoryKey={StoryKey}",
                        user.Username,
                        storyKey);
                    continue;
                }

                var story = new ArchivedInstagramStory
                {
                    Id = Guid.NewGuid(),
                    MonitoredUserId = user.Id,
                    Username = user.Username,
                    StoryKey = storyKey,
                    InstagramStoryId = media.InstagramStoryId ?? media.StoryId,
                    MediaType = media.MediaType,
                    OriginalMediaUrl = media.MediaUrl,
                    PublishedAt = media.PublishedAt,
                    DownloadedAt = _clock.UtcNow,
                    Status = StoryDownloadStatus.Pending
                };

                try
                {
                    var download = await _downloadService.DownloadAsync(media, cancellationToken);
                    story.StoredRelativePath = download.RelativePath;
                    story.FileSize = download.FileSize;
                    story.Sha256 = download.Sha256;
                    story.Status = StoryDownloadStatus.Downloaded;
                    story.DownloadedAt = _clock.UtcNow;

                    await _storyRepository.AddAsync(story, cancellationToken);
                    await _storyRepository.SaveChangesAsync(cancellationToken);
                    downloaded++;

                    _logger.LogInformation(
                        "Downloaded story for {Username}: Path={Path}, Size={Size}, Sha256={Sha256}",
                        user.Username,
                        download.RelativePath,
                        download.FileSize,
                        download.Sha256);
                }
                catch (DuplicateStoryException)
                {
                    skipped++;
                    _logger.LogInformation(
                        "Duplicate story caught by DB constraint for {Username}, StoryKey={StoryKey}",
                        user.Username,
                        storyKey);
                }
                catch (Exception ex)
                {
                    failed++;
                    story.Status = StoryDownloadStatus.Failed;
                    story.ErrorMessage = Truncate(ex.Message, 1000);
                    try
                    {
                        await _storyRepository.AddAsync(story, cancellationToken);
                        await _storyRepository.SaveChangesAsync(cancellationToken);
                    }
                    catch (DuplicateStoryException)
                    {
                        _logger.LogWarning("Could not persist failed story record for {Username}/{StoryKey}", user.Username, storyKey);
                    }

                    _logger.LogError(ex,
                        "Failed to download story for {Username}, Url={Url}",
                        user.Username,
                        UrlSanitizer.SanitizeForLog(media.MediaUrl));
                }
            }

            await RecordSuccessAsync(user, cancellationToken);

            var finishedAt = _clock.UtcNow;
            _logger.LogInformation(
                "Finished check for {Username} at {FinishedAt}. New={New}, Downloaded={Downloaded}, Skipped={Skipped}, Failed={Failed}, NextCheckAt={NextCheckAt}",
                user.Username,
                finishedAt,
                mediaItems.Count,
                downloaded,
                skipped,
                failed,
                user.NextCheckAt);

            return new StoryCheckUserResult(
                user.Username,
                hadActiveStories,
                mediaItems.Count,
                downloaded,
                skipped,
                failed);
        }
        catch (Exception ex) when (ex is InstagramSessionExpiredException or InstagramChallengeDetectedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await RecordFailureAsync(user, ex.Message, cancellationToken);
            throw;
        }
    }

    private async Task RecordSuccessAsync(MonitoredInstagramUser user, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var jitterSeconds = _random.Next(0, Math.Max(1, _options.SuccessJitterSeconds + 1));
        user.LastCheckedAt = now;
        user.NextCheckAt = BackoffCalculator.CalculateNextCheckAfterSuccess(
            now,
            TimeSpan.FromMinutes(_options.PollingIntervalMinutes),
            TimeSpan.FromSeconds(jitterSeconds));
        user.ConsecutiveFailureCount = 0;
        user.LastError = null;
        user.UpdatedAt = now;
        await _userRepository.UpdateAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task RecordFailureAsync(MonitoredInstagramUser user, string error, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        user.LastCheckedAt = now;
        user.ConsecutiveFailureCount += 1;
        user.LastError = Truncate(error, 1000);
        user.NextCheckAt = BackoffCalculator.CalculateNextCheckAfterFailure(now, user.ConsecutiveFailureCount);
        user.UpdatedAt = now;
        await _userRepository.UpdateAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Recorded failure for {Username}. ConsecutiveFailures={Count}, NextCheckAt={NextCheckAt}, Error={Error}",
            user.Username,
            user.ConsecutiveFailureCount,
            user.NextCheckAt,
            user.LastError);
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
