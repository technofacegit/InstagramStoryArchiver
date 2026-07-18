namespace InstagramStoryArchiver.Application.Abstractions;

public interface IInstagramSessionService
{
    bool StorageStateExists();
    Task EnsureSessionValidAsync(CancellationToken cancellationToken);
    bool IsLoginUrl(string url);
    bool IsChallengeOrCheckpointUrl(string url);
    Task DetectSessionProblemsAsync(string currentUrl, CancellationToken cancellationToken);
}
