using InstagramStoryArchiver.Application.Abstractions;

namespace InstagramStoryArchiver.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
