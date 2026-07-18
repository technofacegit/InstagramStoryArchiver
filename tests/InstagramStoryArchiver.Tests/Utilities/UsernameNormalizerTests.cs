using InstagramStoryArchiver.Application.Utilities;

namespace InstagramStoryArchiver.Tests.Utilities;

public class UsernameNormalizerTests
{
    [Theory]
    [InlineData("ExampleUser", "exampleuser")]
    [InlineData("@ExampleUser", "exampleuser")]
    [InlineData("  @demo.user  ", "demo.user")]
    public void Normalize_RemovesAtAndLowercases(string input, string expected)
    {
        Assert.Equal(expected, UsernameNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad user")]
    [InlineData("!!!")]
    public void Normalize_Invalid_Throws(string input)
    {
        Assert.ThrowsAny<ArgumentException>(() => UsernameNormalizer.Normalize(input));
    }
}
