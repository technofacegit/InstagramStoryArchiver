using InstagramStoryArchiver.Domain.Models;

namespace InstagramStoryArchiver.Application.Abstractions;

public interface IInstagramStoryResponseParser
{
    IReadOnlyList<InstagramStoryMedia> Parse(string json, string expectedUsername);
}
