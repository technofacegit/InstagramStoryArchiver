namespace InstagramStoryArchiver.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
