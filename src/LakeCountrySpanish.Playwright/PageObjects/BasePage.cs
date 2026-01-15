using Microsoft.Playwright;

namespace LakeCountrySpanish.Playwright.PageObjects;

/// <summary>
/// Base class for all page objects providing common functionality.
/// </summary>
public abstract class BasePage
{
    protected readonly IPage Page;

    protected BasePage(IPage page)
    {
        Page = page;
    }

    /// <summary>
    /// Navigate to this page's URL.
    /// </summary>
    public abstract Task NavigateAsync();

    /// <summary>
    /// Wait for the page to be fully loaded.
    /// </summary>
    public virtual async Task WaitForLoadAsync()
    {
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Check if user is logged in by looking for logout link.
    /// </summary>
    public async Task<bool> IsLoggedInAsync()
    {
        return await Page.Locator("a[href='/Account/Logout'], button:has-text('Logout')").IsVisibleAsync();
    }

    /// <summary>
    /// Get the current page title.
    /// </summary>
    public async Task<string> GetTitleAsync()
    {
        return await Page.TitleAsync();
    }

    /// <summary>
    /// Check if a success message is displayed.
    /// </summary>
    public async Task<bool> HasSuccessMessageAsync()
    {
        return await Page.Locator(".bg-green-100, .text-green-700, [class*='success']").IsVisibleAsync();
    }

    /// <summary>
    /// Check if an error message is displayed.
    /// </summary>
    public async Task<bool> HasErrorMessageAsync()
    {
        return await Page.Locator(".bg-red-100, .text-red-700, [class*='error'], .validation-summary-errors").IsVisibleAsync();
    }

    /// <summary>
    /// Get the text of the first visible error message.
    /// </summary>
    public async Task<string?> GetErrorMessageAsync()
    {
        var errorElement = Page.Locator(".bg-red-100, .text-red-700, [class*='error'], .validation-summary-errors").First;
        if (await errorElement.IsVisibleAsync())
        {
            return await errorElement.TextContentAsync();
        }
        return null;
    }
}
