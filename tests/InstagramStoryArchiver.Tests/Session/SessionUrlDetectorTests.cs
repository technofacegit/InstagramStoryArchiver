using InstagramStoryArchiver.Application.Utilities;

namespace InstagramStoryArchiver.Tests.Session;

public class SessionUrlDetectorTests
{
    [Theory]
    [InlineData("https://www.instagram.com/accounts/login/", true)]
    [InlineData("https://www.instagram.com/accounts/login/?next=/", true)]
    [InlineData("https://www.instagram.com/exampleuser/", false)]
    public void IsLoginUrl_DetectsLogin(string url, bool expected)
    {
        Assert.Equal(expected, SessionUrlDetector.IsLoginUrl(url));
    }

    [Theory]
    [InlineData("https://www.instagram.com/challenge/", true)]
    [InlineData("https://www.instagram.com/challenge/action/", true)]
    [InlineData("https://www.instagram.com/accounts/suspended/", true)]
    [InlineData("https://www.instagram.com/exampleuser/", false)]
    public void IsChallengeOrCheckpointUrl_DetectsChallenge(string url, bool expected)
    {
        Assert.Equal(expected, SessionUrlDetector.IsChallengeOrCheckpointUrl(url));
    }
}
