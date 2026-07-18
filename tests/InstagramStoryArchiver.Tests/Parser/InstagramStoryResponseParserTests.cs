using InstagramStoryArchiver.Domain.Enums;
using InstagramStoryArchiver.Infrastructure.Playwright;
using Microsoft.Extensions.Logging.Abstractions;

namespace InstagramStoryArchiver.Tests.Parser;

public class InstagramStoryResponseParserTests
{
    private readonly InstagramStoryResponseParser _parser = new(NullLogger<InstagramStoryResponseParser>.Instance);

    [Fact]
    public void Parse_ExtractsNestedVideoUrlAndStoryId()
    {
        var json = """
        {
          "data": {
            "reels_media": [{
              "user": { "username": "demo" },
              "items": [{
                "pk": "111",
                "id": "111_222",
                "taken_at": 1700000000,
                "media_type": 2,
                "video_versions": [{ "url": "https://scontent.cdninstagram.com/v/t50.video/video123.mp4", "width": 720, "height": 1280 }],
                "user": { "username": "demo" }
              }]
            }]
          }
        }
        """;

        var results = _parser.Parse(json, "demo");
        Assert.NotEmpty(results);
        var media = results[0];
        Assert.Equal(StoryMediaType.Video, media.MediaType);
        Assert.Contains("video123.mp4", media.MediaUrl);
        Assert.Equal("111", media.InstagramStoryId);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000), media.PublishedAt);
    }

    [Fact]
    public void Parse_ExtractsNestedImageUrl()
    {
        var json = """
        {
          "items": [{
            "pk": "999",
            "taken_at": 1700001111,
            "user": { "username": "demo" },
            "image_versions2": {
              "candidates": [
                { "url": "https://scontent.cdninstagram.com/v/t51.photo/small.jpg", "width": 320, "height": 320 },
                { "url": "https://scontent.cdninstagram.com/v/t51.photo/large.jpg", "width": 1080, "height": 1920 }
              ]
            }
          }]
        }
        """;

        var results = _parser.Parse(json, "demo");
        Assert.Single(results);
        Assert.Equal(StoryMediaType.Image, results[0].MediaType);
        Assert.Contains("large.jpg", results[0].MediaUrl);
        Assert.Equal("999", results[0].StoryId);
    }

    [Fact]
    public void Parse_SkipsMediaForUnexpectedUsername()
    {
        var json = """
        {
          "pk": "1",
          "video_url": "https://scontent.cdninstagram.com/v/t50.video/x.mp4",
          "user": { "username": "someoneelse" }
        }
        """;

        var results = _parser.Parse(json, "demo");
        Assert.Empty(results);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsEmptyWithoutThrowing()
    {
        var results = _parser.Parse("{not-json", "demo");
        Assert.Empty(results);
    }

    [Fact]
    public void Parse_IgnoresJavaScriptBodiesStartingWithSemicolon()
    {
        var results = _parser.Parse(";!function(){window.foo=1;}();", "demo");
        Assert.Empty(results);
    }

    [Fact]
    public void Parse_StripsForLoopPrefixAndExtractsMedia()
    {
        var json = """
        for (;;);{"pk":"77","taken_at":1700000000,"user":{"username":"demo"},"video_versions":[{"url":"https://scontent.cdninstagram.com/v/t50.video/x.mp4","width":720,"height":1280}]}
        """;

        var results = _parser.Parse(json, "demo");
        Assert.Single(results);
        Assert.Equal(StoryMediaType.Video, results[0].MediaType);
    }

    [Theory]
    [InlineData("for (;;);{\"a\":1}", "{\"a\":1}")]
    [InlineData("  ;{\"a\":1}", "{\"a\":1}")]
    [InlineData(";!function(){}", null)]
    [InlineData("not json", null)]
    public void ExtractJsonPayload_HandlesPrefixes(string input, string? expected)
    {
        Assert.Equal(expected, InstagramStoryResponseParser.ExtractJsonPayload(input));
    }

    [Fact]
    public void Parse_ExtractsTimestampFromString()
    {
        var json = """
        {
          "pk": "55",
          "timestamp": "2026-07-18T15:00:00Z",
          "user": { "username": "demo" },
          "image_versions2": {
            "candidates": [
              { "url": "https://scontent.cdninstagram.com/v/t51.photo/a.jpg", "width": 100, "height": 100 }
            ]
          }
        }
        """;

        var results = _parser.Parse(json, "demo");
        Assert.Single(results);
        Assert.NotNull(results[0].PublishedAt);
        Assert.Equal(2026, results[0].PublishedAt!.Value.Year);
    }
}
