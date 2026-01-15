using Microsoft.Playwright;

namespace LakeCountrySpanish.Playwright.PageObjects;

/// <summary>
/// Page object for the student dashboard.
/// </summary>
public class StudentDashboardPage : BasePage
{
    public StudentDashboardPage(IPage page) : base(page) { }

    public override async Task NavigateAsync()
    {
        await Page.GotoAsync($"{TestConfig.BaseUrl}/Student/Dashboard");
    }

    /// <summary>
    /// Check if we're on the dashboard page.
    /// </summary>
    public async Task<bool> IsOnDashboardAsync()
    {
        return Page.Url.Contains("/Student/Dashboard") ||
               await Page.Locator("h1:has-text('Dashboard'), h1:has-text('Welcome')").IsVisibleAsync();
    }

    /// <summary>
    /// Get the student's name displayed on the dashboard.
    /// </summary>
    public async Task<string?> GetWelcomeNameAsync()
    {
        var welcomeElement = Page.Locator("h1:has-text('Welcome'), h2:has-text('Welcome')").First;
        if (await welcomeElement.IsVisibleAsync())
        {
            return await welcomeElement.TextContentAsync();
        }
        return null;
    }

    /// <summary>
    /// Get the token balance displayed.
    /// </summary>
    public async Task<int?> GetTokenBalanceAsync()
    {
        var tokenElement = Page.Locator("[data-testid='token-balance'], .token-balance, :text-matches('\\d+ tokens?', 'i')").First;
        if (await tokenElement.IsVisibleAsync())
        {
            var text = await tokenElement.TextContentAsync();
            if (text != null)
            {
                var match = System.Text.RegularExpressions.Regex.Match(text, @"(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int tokens))
                {
                    return tokens;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Click the "Schedule Classes" or "My Classes" link.
    /// </summary>
    public async Task ClickScheduleClassesAsync()
    {
        await Page.ClickAsync("a:has-text('Schedule'), a:has-text('My Classes'), a[href*='MyClasses']");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Check if there are upcoming classes displayed.
    /// </summary>
    public async Task<bool> HasUpcomingClassesAsync()
    {
        return await Page.Locator(":text('Upcoming'), :text('Next Class')").IsVisibleAsync();
    }

    /// <summary>
    /// Get the count of upcoming classes shown.
    /// </summary>
    public async Task<int> GetUpcomingClassCountAsync()
    {
        var classCards = Page.Locator(".upcoming-class, [data-testid='upcoming-class'], .class-card");
        return await classCards.CountAsync();
    }

    /// <summary>
    /// Click on the quick action for Token Store.
    /// </summary>
    public async Task ClickTokenStoreAsync()
    {
        await Page.ClickAsync("a:has-text('Token Store'), a[href*='TokenStore']");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Check if the gamification elements are visible (tokens, badges, etc.).
    /// </summary>
    public async Task<bool> HasGamificationElementsAsync()
    {
        return await Page.Locator(":text('Token'), :text('Badge'), :text('Points')").First.IsVisibleAsync();
    }
}
