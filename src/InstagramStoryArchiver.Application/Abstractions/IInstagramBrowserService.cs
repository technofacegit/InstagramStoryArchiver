using Microsoft.Playwright;

namespace InstagramStoryArchiver.Application.Abstractions;

public interface IInstagramBrowserService : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task<IPage> CreatePageAsync(CancellationToken cancellationToken);
    IAPIRequestContext RequestContext { get; }
    IBrowserContext BrowserContext { get; }
    bool IsInitialized { get; }
    Task RestartOnceAsync(CancellationToken cancellationToken);
    Task SaveStorageStateAsync(string path, CancellationToken cancellationToken);
}
