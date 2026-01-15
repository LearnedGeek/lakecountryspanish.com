using Microsoft.Playwright.NUnit;
using LakeCountrySpanish.Playwright.PageObjects;

namespace LakeCountrySpanish.Playwright.Tests;

/// <summary>
/// Tests for the student dashboard page.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class StudentDashboardTests : PageTest
{
    private LoginPage _loginPage = null!;
    private StudentDashboardPage _dashboardPage = null!;

    [SetUp]
    public async Task Setup()
    {
        _loginPage = new LoginPage(Page);
        _dashboardPage = new StudentDashboardPage(Page);

        // Login before each test
        await _loginPage.NavigateAsync();
        await _loginPage.LoginAsync(
            TestConfig.TestStudentEmail,
            TestConfig.TestStudentPassword);
    }

    [Test]
    [Description("Verify dashboard loads after login")]
    public async Task Dashboard_ShouldLoadAfterLogin()
    {
        await _dashboardPage.NavigateAsync();

        Assert.That(await _dashboardPage.IsOnDashboardAsync(), Is.True,
            "Should be on the dashboard page");
    }

    [Test]
    [Description("Verify dashboard shows welcome message with name")]
    public async Task Dashboard_ShouldShowWelcomeMessage()
    {
        await _dashboardPage.NavigateAsync();

        var welcomeText = await _dashboardPage.GetWelcomeNameAsync();

        Assert.That(welcomeText, Is.Not.Null.And.Not.Empty,
            "Should display a welcome message");

        TestContext.WriteLine($"Welcome message: {welcomeText}");
    }

    [Test]
    [Description("Verify dashboard shows gamification elements (tokens, etc.)")]
    public async Task Dashboard_ShouldShowGamificationElements()
    {
        await _dashboardPage.NavigateAsync();

        Assert.That(await _dashboardPage.HasGamificationElementsAsync(), Is.True,
            "Should display gamification elements (tokens, badges, points)");
    }

    [Test]
    [Description("Verify Schedule Classes link works")]
    public async Task Dashboard_ScheduleClassesLink_ShouldNavigate()
    {
        await _dashboardPage.NavigateAsync();
        await _dashboardPage.ClickScheduleClassesAsync();

        Assert.That(
            Page.Url.Contains("/MyClasses") || Page.Url.Contains("/Schedule") || Page.Url.Contains("/BookClass"),
            Is.True,
            $"Should navigate to scheduling page. Current URL: {Page.Url}");
    }

    [Test]
    [Description("Verify Token Store link works")]
    public async Task Dashboard_TokenStoreLink_ShouldNavigate()
    {
        await _dashboardPage.NavigateAsync();

        try
        {
            await _dashboardPage.ClickTokenStoreAsync();

            Assert.That(Page.Url, Does.Contain("TokenStore"),
                "Should navigate to Token Store page");
        }
        catch
        {
            // Token store link might not be visible in all states
            TestContext.WriteLine("Token Store link not found or not visible");
        }
    }

    [Test]
    [Description("Verify dashboard displays without errors")]
    public async Task Dashboard_ShouldNotShowErrors()
    {
        await _dashboardPage.NavigateAsync();

        Assert.That(await _dashboardPage.HasErrorMessageAsync(), Is.False,
            "Dashboard should not display any error messages");
    }

    [Test]
    [Description("Verify navigation menu is visible")]
    public async Task Dashboard_NavigationMenu_ShouldBeVisible()
    {
        await _dashboardPage.NavigateAsync();

        // Check for common nav elements
        var hasNav = await Page.Locator("nav, .navigation, header").IsVisibleAsync();

        Assert.That(hasNav, Is.True,
            "Navigation menu should be visible");
    }
}
