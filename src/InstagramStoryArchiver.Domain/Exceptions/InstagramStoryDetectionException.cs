namespace InstagramStoryArchiver.Domain.Exceptions;

public class InstagramStoryDetectionException : Exception
{
    public InstagramStoryDetectionException(string message)
        : base(message)
    {
    }

    public InstagramStoryDetectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
