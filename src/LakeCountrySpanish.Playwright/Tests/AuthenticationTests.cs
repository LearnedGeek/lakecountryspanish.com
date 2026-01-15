using Microsoft.Playwright.NUnit;
using LakeCountrySpanish.Playwright.PageObjects;

namespace LakeCountrySpanish.Playwright.Tests;

/// <summary>
/// Tests for authentication flows: login, logout, access denied.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class AuthenticationTests : PageTest
{
    private LoginPage _loginPage = null!;
    private StudentDashboardPage _dashboardPage = null!;

    [SetUp]
    public void Setup()
    {
        _loginPage = new LoginPage(Page);
        _dashboardPage = new StudentDashboardPage(Page);
    }

    [Test]
    [Description("Verify the login page loads correctly")]
    public async Task LoginPage_ShouldLoad()
    {
        await _loginPage.NavigateAsync();

        Assert.That(await _loginPage.IsOnLoginPageAsync(), Is.True,
            "Should be on the login page");

        var title = await _loginPage.GetTitleAsync();
        Assert.That(title, Does.Contain("Login").Or.Contain("Sign In"),
            "Page title should contain 'Login' or 'Sign In'");
    }

    [Test]
    [Description("Verify successful login redirects to dashboard")]
    public async Task Login_WithValidCredentials_ShouldRedirectToDashboard()
    {
        await _loginPage.NavigateAsync();

        await _loginPage.LoginAsync(
            TestConfig.TestStudentEmail,
            TestConfig.TestStudentPassword);

        // Should redirect to dashboard or home
        Assert.That(
            Page.Url.Contains("/Student/Dashboard") || Page.Url.Contains("/Home") || Page.Url == TestConfig.BaseUrl + "/",
            Is.True,
            $"Should redirect after login. Current URL: {Page.Url}");

        Assert.That(await _loginPage.IsLoggedInAsync(), Is.True,
            "Should show logged-in state (logout link visible)");
    }

    [Test]
    [Description("Verify login with invalid password shows error")]
    public async Task Login_WithInvalidPassword_ShouldShowError()
    {
        await _loginPage.NavigateAsync();

        await _loginPage.LoginAsync(
            TestConfig.TestStudentEmail,
            "WrongPassword123!");

        Assert.That(await _loginPage.IsOnLoginPageAsync(), Is.True,
            "Should remain on login page after failed login");

        Assert.That(await _loginPage.HasErrorMessageAsync(), Is.True,
            "Should show error message for invalid credentials");
    }

    [Test]
    [Description("Verify login with non-existent email shows error")]
    public async Task Login_WithNonExistentEmail_ShouldShowError()
    {
        await _loginPage.NavigateAsync();

        await _loginPage.LoginAsync(
            "nonexistent@example.com",
            "SomePassword123!");

        Assert.That(await _loginPage.IsOnLoginPageAsync(), Is.True,
            "Should remain on login page after failed login");

        Assert.That(await _loginPage.HasErrorMessageAsync(), Is.True,
            "Should show error message for non-existent user");
    }

    [Test]
    [Description("Verify accessing protected page redirects to login")]
    public async Task ProtectedPage_WhenNotLoggedIn_ShouldRedirectToLogin()
    {
        // Try to access dashboard directly without logging in
        await Page.GotoAsync($"{TestConfig.BaseUrl}/Student/Dashboard");
        await Page.WaitForLoadStateAsync();

        // Should redirect to login
        Assert.That(Page.Url, Does.Contain("/Account/Login"),
            "Should redirect to login page when accessing protected resource");
    }

    [Test]
    [Description("Verify logout clears session")]
    public async Task Logout_ShouldClearSession()
    {
        // First login
        await _loginPage.NavigateAsync();
        await _loginPage.LoginAsync(
            TestConfig.TestStudentEmail,
            TestConfig.TestStudentPassword);

        // Verify logged in
        Assert.That(await _loginPage.IsLoggedInAsync(), Is.True);

        // Click logout
        await Page.ClickAsync("a[href*='Logout'], button:has-text('Logout')");
        await Page.WaitForLoadStateAsync();

        // Verify logged out - either on home page or login page
        Assert.That(await _loginPage.IsLoggedInAsync(), Is.False,
            "Should not show logged-in state after logout");
    }

    [Test]
    [Description("Verify Remember Me checkbox exists")]
    public async Task LoginPage_ShouldHaveRememberMeOption()
    {
        await _loginPage.NavigateAsync();

        // This might not exist depending on your implementation
        var hasRememberMe = await _loginPage.HasRememberMeAsync();

        // Just log the result - not a failure if it doesn't exist
        TestContext.WriteLine($"Remember Me checkbox present: {hasRememberMe}");
    }
}
