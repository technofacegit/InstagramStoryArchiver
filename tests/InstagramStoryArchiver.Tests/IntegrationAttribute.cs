namespace InstagramStoryArchiver.Tests;

/// <summary>
/// Marker for optional integration tests that hit Instagram.
/// Exclude with: dotnet test --filter "Category!=Integration"
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class IntegrationAttribute : Attribute
{
}
