using InstagramStoryArchiver.Application.Abstractions;
using InstagramStoryArchiver.Application.Options;
using InstagramStoryArchiver.Application.Utilities;
using InstagramStoryArchiver.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace InstagramStoryArchiver.Infrastructure.Playwright;

public sealed class InstagramSessionService : IInstagramSessionService
{
    private readonly InstagramOptions _options;
    private readonly IInstagramBrowserService _browserService;
    private readonly ILogger<InstagramSessionService> _logger;

    public InstagramSessionService(
        IOptions<InstagramOptions> options,
        IInstagramBrowserService browserService,
        ILogger<InstagramSessionService> logger)
    {
        _options = options.Value;
        _browserService = browserService;
        _logger = logger;
    }

    public bool StorageStateExists()
        => File.Exists(Path.GetFullPath(_options.StorageStatePath));

    public bool IsLoginUrl(string url) => SessionUrlDetector.IsLoginUrl(url);

    public bool IsChallengeOrCheckpointUrl(string url) => SessionUrlDetector.IsChallengeOrCheckpointUrl(url);

    public async Task EnsureSessionValidAsync(CancellationToken cancellationToken)
    {
        if (!StorageStateExists())
        {
            throw new InstagramSessionExpiredException(
                $"Storage state missing at '{_options.StorageStatePath}'. Run login mode first.");
        }

        IPage? page = null;
        try
        {
            page = await _browserService.CreatePageAsync(cancellationToken);
            var response = await page.GotoAsync(
                _options.BaseUrl.TrimEnd('/') + "/",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

            var url = page.Url;
            await DetectSessionProblemsAsync(url, cancellationToken);

            if (response is not null && response.Status is >= 400)
            {
                _logger.LogWarning("Instagram home returned status {Status}", response.Status);
            }

            var loginVisible = await page.Locator(InstagramLocators.LoginFormSelector).CountAsync() > 0;
            if (loginVisible || IsLoginUrl(url))
            {
                throw new InstagramSessionExpiredException(
                    "Instagram session appears expired (login page detected). Run: dotnet run -- login");
            }
        }
        finally
        {
            if (page is not null)
            {
                await page.CloseAsync();
            }
        }
    }

    public Task DetectSessionProblemsAsync(string currentUrl, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (IsChallengeOrCheckpointUrl(currentUrl))
        {
            _logger.LogCritical("Instagram challenge/checkpoint detected at {Url}", currentUrl);
            throw new InstagramChallengeDetectedException(
                $"Instagram challenge/checkpoint detected at '{currentUrl}'. Manual intervention required. Do not retry automatically.");
        }

        if (IsLoginUrl(currentUrl))
        {
            _logger.LogCritical("Instagram session expired. Login URL detected: {Url}", currentUrl);
            throw new InstagramSessionExpiredException(
                $"Instagram session expired (redirected to '{currentUrl}'). Re-run: dotnet run -- login");
        }

        return Task.CompletedTask;
    }
}
