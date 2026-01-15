using Microsoft.Playwright;

namespace LakeCountrySpanish.Playwright.PageObjects;

/// <summary>
/// Page object for the login page.
/// </summary>
public class LoginPage : BasePage
{
    public LoginPage(IPage page) : base(page) { }

    public override async Task NavigateAsync()
    {
        await Page.GotoAsync($"{TestConfig.BaseUrl}/Account/Login");
    }

    /// <summary>
    /// Fill in the email field.
    /// </summary>
    public async Task EnterEmailAsync(string email)
    {
        await Page.FillAsync("input[name='Email'], input[type='email'], #Email", email);
    }

    /// <summary>
    /// Fill in the password field.
    /// </summary>
    public async Task EnterPasswordAsync(string password)
    {
        await Page.FillAsync("input[name='Password'], input[type='password'], #Password", password);
    }

    /// <summary>
    /// Click the login/submit button.
    /// </summary>
    public async Task ClickLoginAsync()
    {
        await Page.ClickAsync("button[type='submit'], input[type='submit']");
    }

    /// <summary>
    /// Perform a complete login flow.
    /// </summary>
    public async Task LoginAsync(string email, string password)
    {
        await EnterEmailAsync(email);
        await EnterPasswordAsync(password);
        await ClickLoginAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Check if the "Remember me" checkbox exists.
    /// </summary>
    public async Task<bool> HasRememberMeAsync()
    {
        return await Page.Locator("input[name='RememberMe'], #RememberMe").IsVisibleAsync();
    }

    /// <summary>
    /// Check the "Remember me" checkbox.
    /// </summary>
    public async Task CheckRememberMeAsync()
    {
        await Page.CheckAsync("input[name='RememberMe'], #RememberMe");
    }

    /// <summary>
    /// Check if we're on the login page.
    /// </summary>
    public async Task<bool> IsOnLoginPageAsync()
    {
        return Page.Url.Contains("/Account/Login") ||
               await Page.Locator("h1:has-text('Login'), h2:has-text('Login'), h1:has-text('Sign In')").IsVisibleAsync();
    }
}
