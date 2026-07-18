namespace InstagramStoryArchiver.Application.Abstractions;

public interface IInstanceLock : IAsyncDisposable
{
    Task<bool> TryAcquireAsync(CancellationToken cancellationToken);
}
