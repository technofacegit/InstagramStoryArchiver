namespace InstagramStoryArchiver.Domain.Exceptions;

public class InstagramMediaDownloadException : Exception
{
    public InstagramMediaDownloadException(string message)
        : base(message)
    {
    }

    public InstagramMediaDownloadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
