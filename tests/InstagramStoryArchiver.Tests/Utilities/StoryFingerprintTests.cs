using InstagramStoryArchiver.Application.Utilities;

namespace InstagramStoryArchiver.Tests.Utilities;

public class StoryFingerprintTests
{
    [Fact]
    public void Create_PrefersInstagramStoryId()
    {
        var key = StoryFingerprint.Create("12345", "user", "https://cdn.example/a.jpg", DateTimeOffset.UtcNow);
        Assert.Equal("12345", key);
    }

    [Fact]
    public void Create_SameInputs_ProduceSameFingerprint()
    {
        var published = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var a = StoryFingerprint.Create(null, "user", "https://cdn.instagram.com/v/t51.abc/xyz123.mp4", published);
        var b = StoryFingerprint.Create(null, "user", "https://cdn.instagram.com/v/t51.abc/xyz123.mp4", published);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Create_DifferentStories_ProduceDifferentFingerprints()
    {
        var published = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var a = StoryFingerprint.Create(null, "user", "https://cdn.instagram.com/v/t51.abc/aaa111.mp4", published);
        var b = StoryFingerprint.Create(null, "user", "https://cdn.instagram.com/v/t51.abc/bbb222.mp4", published);
        Assert.NotEqual(a, b);
    }
}
