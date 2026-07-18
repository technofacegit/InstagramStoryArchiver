using InstagramStoryArchiver.Application.Utilities;

namespace InstagramStoryArchiver.Tests.Utilities;

public class BackoffCalculatorTests
{
    [Theory]
    [InlineData(1, 15)]
    [InlineData(2, 30)]
    [InlineData(3, 60)]
    [InlineData(4, 240)]
    [InlineData(10, 240)]
    public void Calculate_UsesExponentialBackoffMinutes(int failures, int expectedMinutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), BackoffCalculator.Calculate(failures));
    }
}
