using InstagramStoryArchiver.Application.Utilities;
using InstagramStoryArchiver.Domain.Enums;

namespace InstagramStoryArchiver.Tests.Utilities;

public class ArchivePathBuilderTests
{
    [Fact]
    public void BuildRelativePath_UsesExpectedStructure()
    {
        var published = new DateTimeOffset(2026, 7, 18, 18, 45, 30, TimeSpan.Zero);
        var path = ArchivePathBuilder.BuildRelativePath("ExampleUser", published, "ABC123", ".mp4");

        Assert.Equal(Path.Combine("exampleuser", "2026", "07", "18", "exampleuser_184530_ABC123.mp4"), path);
    }

    [Fact]
    public void SanitizeFileName_RemovesInvalidCharacters()
    {
        var sanitized = ArchivePathBuilder.SanitizeFileName("a/b:c*d?.mp4");
        Assert.DoesNotContain("/", sanitized);
        Assert.DoesNotContain(":", sanitized);
        Assert.DoesNotContain("*", sanitized);
        Assert.DoesNotContain("?", sanitized);
    }

    [Theory]
    [InlineData(StoryMediaType.Video, "video/mp4", "https://x/a.bin", ".mp4")]
    [InlineData(StoryMediaType.Image, "image/jpeg", "https://x/a.bin", ".jpg")]
    [InlineData(StoryMediaType.Image, "image/png", "https://x/a.bin", ".png")]
    [InlineData(StoryMediaType.Image, "image/webp", "https://x/a.bin", ".webp")]
    [InlineData(StoryMediaType.Unknown, "application/octet-stream", "https://cdn/x/file.jpg", ".jpg")]
    public void ResolveExtension_UsesContentTypeOrUrl(
        StoryMediaType mediaType,
        string contentType,
        string url,
        string expected)
    {
        Assert.Equal(expected, ArchivePathBuilder.ResolveExtension(mediaType, contentType, url));
    }
}
