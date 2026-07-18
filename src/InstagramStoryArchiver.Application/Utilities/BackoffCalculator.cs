namespace InstagramStoryArchiver.Application.Utilities;

public static class BackoffCalculator
{
    public static readonly TimeSpan FirstFailure = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan SecondFailure = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan ThirdFailure = TimeSpan.FromMinutes(60);
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(4);

    public static TimeSpan Calculate(int consecutiveFailureCount)
    {
        return consecutiveFailureCount switch
        {
            <= 0 => TimeSpan.Zero,
            1 => FirstFailure,
            2 => SecondFailure,
            3 => ThirdFailure,
            _ => MaxBackoff
        };
    }

    public static DateTimeOffset CalculateNextCheckAfterSuccess(
        DateTimeOffset now,
        TimeSpan baseInterval,
        TimeSpan jitter)
    {
        return now + baseInterval + jitter;
    }

    public static DateTimeOffset CalculateNextCheckAfterFailure(
        DateTimeOffset now,
        int consecutiveFailureCount)
    {
        return now + Calculate(consecutiveFailureCount);
    }
}
