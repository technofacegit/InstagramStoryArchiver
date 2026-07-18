using System.ComponentModel.DataAnnotations;

namespace InstagramStoryArchiver.Application.Options;

public sealed class InstagramOptions
{
    public const string SectionName = "Instagram";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://www.instagram.com";

    [Required]
    public string StorageStatePath { get; set; } = "data/instagram-storage-state.json";

    public bool Headless { get; set; } = true;

    [Range(5, 300)]
    public int NavigationTimeoutSeconds { get; set; } = 45;

    [Range(5, 120)]
    public int StoryLoadTimeoutSeconds { get; set; } = 20;

    [Range(1, 1440)]
    public int PollingIntervalMinutes { get; set; } = 15;

    [Range(0, 600)]
    public int MinimumDelayBetweenUsersSeconds { get; set; } = 20;

    [Range(1, 600)]
    public int MaximumDelayBetweenUsersSeconds { get; set; } = 40;

    [Range(1, 1024)]
    public int MaximumDownloadSizeMb { get; set; } = 100;

    [Range(0, 15)]
    public int SuccessJitterSeconds { get; set; } = 90;

    [Range(1, 30)]
    public int MaxMonitoredUsers { get; set; } = 15;

    public void Validate()
    {
        if (MaximumDelayBetweenUsersSeconds < MinimumDelayBetweenUsersSeconds)
        {
            throw new ValidationException(
                $"{nameof(MaximumDelayBetweenUsersSeconds)} must be greater than or equal to {nameof(MinimumDelayBetweenUsersSeconds)}.");
        }
    }
}
