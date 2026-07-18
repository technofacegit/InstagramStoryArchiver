using InstagramStoryArchiver.Application.Abstractions;
using InstagramStoryArchiver.Application.Options;
using InstagramStoryArchiver.Domain.Exceptions;
using Microsoft.Extensions.Options;

namespace InstagramStoryArchiver.Worker;

public sealed class StoryArchiverWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IInstanceLock _instanceLock;
    private readonly InstagramOptions _options;
    private readonly ILogger<StoryArchiverWorker> _logger;

    public StoryArchiverWorker(
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime lifetime,
        IInstanceLock instanceLock,
        IOptions<InstagramOptions> options,
        ILogger<StoryArchiverWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _lifetime = lifetime;
        _instanceLock = instanceLock;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!await _instanceLock.TryAcquireAsync(stoppingToken))
        {
            _logger.LogCritical("Another worker instance is already running. Exiting.");
            Environment.ExitCode = 1;
            _lifetime.StopApplication();
            return;
        }

        _logger.LogInformation(
            "Story archiver worker started. Polling every {Minutes} minutes.",
            _options.PollingIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var session = scope.ServiceProvider.GetRequiredService<IInstagramSessionService>();
                if (!session.StorageStateExists())
                {
                    _logger.LogCritical(
                        "Storage state file is missing at {Path}. Worker will stop. Run: dotnet run --project src/InstagramStoryArchiver.Worker -- login",
                        _options.StorageStatePath);
                    _lifetime.StopApplication();
                    Environment.ExitCode = 2;
                    return;
                }

                var orchestrator = scope.ServiceProvider.GetRequiredService<IStoryCheckOrchestrator>();
                var result = await orchestrator.RunDueUsersAsync(stoppingToken);

                _logger.LogInformation(
                    "Check cycle finished. Users={Users}, Downloaded={Downloaded}, Skipped={Skipped}, SessionStopped={SessionStopped}",
                    result.UsersChecked,
                    result.StoriesDownloaded,
                    result.StoriesSkipped,
                    result.StoppedDueToSessionIssue);

                if (result.StoppedDueToSessionIssue)
                {
                    _logger.LogCritical(
                        "Stopping worker due to Instagram session/challenge issue. Manual login required. Exit code 2 prevents Docker restart loops when configured with on-failure or similar policies that ignore code 2.");
                    Environment.ExitCode = 2;
                    _lifetime.StopApplication();
                    return;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is InstagramSessionExpiredException or InstagramChallengeDetectedException)
            {
                _logger.LogCritical(ex, "Fatal Instagram session/challenge error. Stopping worker with exit code 2.");
                Environment.ExitCode = 2;
                _lifetime.StopApplication();
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during check cycle. Will retry after polling interval.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(_options.PollingIntervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Story archiver worker stopping.");
        await _instanceLock.DisposeAsync();
    }
}
