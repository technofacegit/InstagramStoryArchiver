namespace InstagramStoryArchiver.Domain.Exceptions;

public class InstagramResponseParseException : Exception
{
    public InstagramResponseParseException(string message)
        : base(message)
    {
    }

    public InstagramResponseParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
