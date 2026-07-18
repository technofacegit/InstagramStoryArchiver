namespace InstagramStoryArchiver.Infrastructure.Playwright;

/// <summary>
/// Centralized Instagram DOM locators. Update this class when Instagram UI changes.
/// Prefer href, role, aria-label, and visible text over fragile CSS class names.
/// </summary>
public static class InstagramLocators
{
    public const string StoryLinkHrefContains = "/stories/";
    public const string ProfileStoryRingSelector = "header a[href*='/stories/'], a[href*='/stories/'][role='link']";
    public const string StoryCanvasSelector = "div[role='presentation'] img, div[role='dialog'] video, div[role='presentation'] video";
    public const string NextStoryButtonAria = "Next";
    public const string CloseStoryButtonAria = "Close";
    public const string LoginFormSelector = "input[name='username'], form#loginForm, input[name='email']";
    public const string ChallengeTextHints = "Confirm it's you|Suspicious Login Attempt|Enter Security Code|checkpoint";
}
