using InstagramStoryArchiver.Domain.Enums;

namespace InstagramStoryArchiver.Domain.Entities;

public class ArchivedInstagramStory
{
    public Guid Id { get; set; }
    public Guid MonitoredUserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string StoryKey { get; set; } = string.Empty;
    public string? InstagramStoryId { get; set; }
    public StoryMediaType MediaType { get; set; }
    public string? OriginalMediaUrl { get; set; }
    public string? StoredRelativePath { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset DownloadedAt { get; set; }
    public StoryDownloadStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public long? FileSize { get; set; }
    public string? Sha256 { get; set; }

    public MonitoredInstagramUser? MonitoredUser { get; set; }
}
