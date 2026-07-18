namespace InstagramStoryArchiver.Domain.Entities;

public class MonitoredInstagramUser
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastCheckedAt { get; set; }
    public DateTimeOffset? NextCheckAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int ConsecutiveFailureCount { get; set; }
    public string? LastError { get; set; }

    public ICollection<ArchivedInstagramStory> Stories { get; set; } = new List<ArchivedInstagramStory>();
}
