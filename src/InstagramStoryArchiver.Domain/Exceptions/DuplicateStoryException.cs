namespace InstagramStoryArchiver.Domain.Exceptions;

public class DuplicateStoryException : Exception
{
    public DuplicateStoryException(string username, string storyKey)
        : base($"Story already exists for {username}/{storyKey}.")
    {
        Username = username;
        StoryKey = storyKey;
    }

    public string Username { get; }
    public string StoryKey { get; }
}
