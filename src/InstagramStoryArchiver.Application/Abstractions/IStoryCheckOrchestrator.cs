namespace InstagramStoryArchiver.Application.Abstractions;

public interface IStoryCheckOrchestrator
{
    Task<StoryCheckCycleResult> RunDueUsersAsync(CancellationToken cancellationToken);
    Task<StoryCheckUserResult> CheckUserAsync(string username, CancellationToken cancellationToken);
}

public sealed record StoryCheckCycleResult(
    int UsersChecked,
    int StoriesDownloaded,
    int StoriesSkipped,
    bool StoppedDueToSessionIssue);

public sealed record StoryCheckUserResult(
    string Username,
    bool HadActiveStories,
    int NewStories,
    int Downloaded,
    int SkippedDuplicates,
    int Failed);
