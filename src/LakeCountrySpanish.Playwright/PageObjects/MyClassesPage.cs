using Microsoft.Playwright;

namespace LakeCountrySpanish.Playwright.PageObjects;

/// <summary>
/// Page object for the My Classes / Scheduling page.
/// </summary>
public class MyClassesPage : BasePage
{
    public MyClassesPage(IPage page) : base(page) { }

    public override async Task NavigateAsync()
    {
        await Page.GotoAsync($"{TestConfig.BaseUrl}/Student/MyClasses");
    }

    /// <summary>
    /// Check if we're on the MyClasses page.
    /// </summary>
    public async Task<bool> IsOnMyClassesPageAsync()
    {
        return Page.Url.Contains("/Student/MyClasses") ||
               await Page.Locator("h1:has-text('Classes'), h1:has-text('Schedule')").IsVisibleAsync();
    }

    /// <summary>
    /// Check if the calendar is visible.
    /// </summary>
    public async Task<bool> IsCalendarVisibleAsync()
    {
        return await Page.Locator(".calendar, [data-testid='calendar'], table, .grid").IsVisibleAsync();
    }

    /// <summary>
    /// Click on an available time slot in the calendar.
    /// </summary>
    /// <param name="dayOffset">Days from today (0 = today, 1 = tomorrow, etc.)</param>
    public async Task ClickAvailableSlotAsync(int dayOffset = 1)
    {
        // Look for green/available slots
        var availableSlots = Page.Locator(".bg-green-100, .available, [data-available='true'], button:has-text('Available')");
        var count = await availableSlots.CountAsync();

        if (count > 0)
        {
            await availableSlots.First.ClickAsync();
        }
        else
        {
            throw new Exception("No available time slots found on the calendar");
        }
    }

    /// <summary>
    /// Get the number of items in the cart.
    /// </summary>
    public async Task<int> GetCartItemCountAsync()
    {
        var cartItems = Page.Locator("#cartItems .cart-item, [data-testid='cart-item']");
        return await cartItems.CountAsync();
    }

    /// <summary>
    /// Check if the cart is empty.
    /// </summary>
    public async Task<bool> IsCartEmptyAsync()
    {
        var emptyMessage = Page.Locator("#emptyCartMessage, :text('Your cart is empty'), :text('No classes selected')");
        return await emptyMessage.IsVisibleAsync();
    }

    /// <summary>
    /// Get the cart total amount displayed.
    /// </summary>
    public async Task<decimal?> GetCartTotalAsync()
    {
        var totalElement = Page.Locator("#cartTotal, [data-testid='cart-total'], :text-matches('\\$\\d+\\.\\d{2}')").First;
        if (await totalElement.IsVisibleAsync())
        {
            var text = await totalElement.TextContentAsync();
            if (text != null)
            {
                var match = System.Text.RegularExpressions.Regex.Match(text, @"\$?(\d+\.?\d*)");
                if (match.Success && decimal.TryParse(match.Groups[1].Value, out decimal total))
                {
                    return total;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Click the checkout button.
    /// </summary>
    public async Task ClickCheckoutAsync()
    {
        await Page.ClickAsync("button:has-text('Checkout'), button:has-text('Pay'), a:has-text('Checkout')");
    }

    /// <summary>
    /// Remove an item from the cart by clicking the X button.
    /// </summary>
    public async Task RemoveFirstCartItemAsync()
    {
        var removeButton = Page.Locator(".cart-item button, [data-testid='remove-from-cart']").First;
        await removeButton.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Navigate to the previous week in the calendar.
    /// </summary>
    public async Task GoToPreviousWeekAsync()
    {
        await Page.ClickAsync("button:has-text('Previous'), button:has-text('←'), [data-action='prev-week']");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Navigate to the next week in the calendar.
    /// </summary>
    public async Task GoToNextWeekAsync()
    {
        await Page.ClickAsync("button:has-text('Next'), button:has-text('→'), [data-action='next-week']");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Confirm the booking in a modal dialog if one appears.
    /// </summary>
    public async Task ConfirmBookingModalAsync()
    {
        var confirmButton = Page.Locator("#bookingModal button:has-text('Confirm'), .modal button:has-text('Book'), button:has-text('Add to Cart')");
        if (await confirmButton.IsVisibleAsync())
        {
            await confirmButton.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }
    }

    /// <summary>
    /// Check if a booking confirmation modal is visible.
    /// </summary>
    public async Task<bool> IsBookingModalVisibleAsync()
    {
        return await Page.Locator("#bookingModal:not(.hidden), .modal:visible").IsVisibleAsync();
    }
}
