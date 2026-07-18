using InstagramStoryArchiver.Domain.Enums;

namespace InstagramStoryArchiver.Domain.Models;

public sealed class InstagramStoryMedia
{
    public required string StoryId { get; init; }
    public required string Username { get; init; }
    public StoryMediaType MediaType { get; init; }
    public required string MediaUrl { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
    public string? InstagramStoryId { get; init; }
}
