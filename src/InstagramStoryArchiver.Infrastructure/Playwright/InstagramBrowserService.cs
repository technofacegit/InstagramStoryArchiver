using InstagramStoryArchiver.Application.Abstractions;
using InstagramStoryArchiver.Application.Options;
using InstagramStoryArchiver.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace InstagramStoryArchiver.Infrastructure.Playwright;

public sealed class InstagramBrowserService : IInstagramBrowserService
{
    private readonly InstagramOptions _options;
    private readonly ILogger<InstagramBrowserService> _logger;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private bool _restartAttempted;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public InstagramBrowserService(IOptions<InstagramOptions> options, ILogger<InstagramBrowserService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsInitialized => _context is not null;
    public IBrowserContext BrowserContext => _context ?? throw new InvalidOperationException("Browser is not initialized.");
    public IAPIRequestContext RequestContext => BrowserContext.APIRequest;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_context is not null)
            {
                return;
            }

            await CreateBrowserAsync(cancellationToken);
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task RestartOnceAsync(CancellationToken cancellationToken)
    {
        if (_restartAttempted)
        {
            throw new InstagramNavigationException("Browser restart already attempted once. Aborting restart loop.");
        }

        _restartAttempted = true;
        _logger.LogWarning("Restarting Playwright browser once after failure.");
        await DisposeInternalAsync();
        await CreateBrowserAsync(cancellationToken);
    }

    public async Task<IPage> CreatePageAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await InitializeAsync(cancellationToken);
        var page = await BrowserContext.NewPageAsync();
        page.SetDefaultTimeout(_options.NavigationTimeoutSeconds * 1000);
        page.SetDefaultNavigationTimeout(_options.NavigationTimeoutSeconds * 1000);
        return page;
    }

    public async Task SaveStorageStateAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await BrowserContext.StorageStateAsync(new BrowserContextStorageStateOptions { Path = path });
        try
        {
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not tighten permissions on storage state file.");
        }
    }

    private async Task CreateBrowserAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = _options.Headless
        });

        var storagePath = Path.GetFullPath(_options.StorageStatePath);
        var contextOptions = new BrowserNewContextOptions
        {
            Locale = "en-US",
            UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36",
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 }
        };

        if (File.Exists(storagePath))
        {
            contextOptions.StorageStatePath = storagePath;
            _logger.LogInformation("Loading Instagram storage state from {Path}", storagePath);
        }

        _context = await _browser.NewContextAsync(contextOptions);
        _logger.LogInformation("Playwright browser initialized (Headless={Headless})", _options.Headless);
    }

    private async Task DisposeInternalAsync()
    {
        if (_context is not null)
        {
            await _context.DisposeAsync();
            _context = null;
        }

        if (_browser is not null)
        {
            await _browser.DisposeAsync();
            _browser = null;
        }

        _playwright?.Dispose();
        _playwright = null;
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeInternalAsync();
        _initLock.Dispose();
    }
}
