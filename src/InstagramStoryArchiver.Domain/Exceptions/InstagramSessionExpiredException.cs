namespace InstagramStoryArchiver.Domain.Exceptions;

public class InstagramSessionExpiredException : Exception
{
    public InstagramSessionExpiredException(string message)
        : base(message)
    {
    }

    public InstagramSessionExpiredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
