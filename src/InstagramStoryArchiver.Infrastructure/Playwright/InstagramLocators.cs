namespace InstagramStoryArchiver.Infrastructure.Playwright;

/// <summary>
/// Centralized Instagram DOM locators. Update this class when Instagram UI changes.
/// Prefer href, role, aria-label, and visible text over fragile CSS class names.
/// </summary>
public static class InstagramLocators
{
    public const string StoryLinkHrefContains = "/stories/";

    /// <summary>Classic story links on profile (may be absent on modern UI).</summary>
    public const string ProfileStoryLinkSelector = "a[href*='/stories/']";

    /// <summary>Modern Instagram draws the story ring on a canvas inside the profile header.</summary>
    public const string ProfileHeaderCanvasSelector = "header canvas, section header canvas, main header canvas";

    /// <summary>Clickable profile photo / story ring button in header.</summary>
    public const string ProfileHeaderPhotoButtonSelector =
        "header img[alt*='profile picture'], header img[alt*='Profil fotoğrafı'], header [role='button'] img";

    public const string StoryViewerMediaSelector =
        "div[role='presentation'] img, div[role='dialog'] img, div[role='presentation'] video, div[role='dialog'] video, section video, section img[src*='cdninstagram'], section img[src*='fbcdn']";

    public const string NextStoryButtonAria = "Next";
    public const string CloseStoryButtonAria = "Close";
    public const string LoginFormSelector = "input[name='username'], form#loginForm, input[name='email']";
    public const string ChallengeTextHints = "Confirm it's you|Suspicious Login Attempt|Enter Security Code|checkpoint";
}
