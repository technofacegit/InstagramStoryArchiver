namespace InstagramStoryArchiver.Domain.Exceptions;

public class InstagramNavigationException : Exception
{
    public InstagramNavigationException(string message)
        : base(message)
    {
    }

    public InstagramNavigationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
