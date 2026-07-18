namespace InstagramStoryArchiver.Domain.Exceptions;

public class InstagramChallengeDetectedException : Exception
{
    public InstagramChallengeDetectedException(string message)
        : base(message)
    {
    }

    public InstagramChallengeDetectedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
