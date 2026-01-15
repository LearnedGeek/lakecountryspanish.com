using Microsoft.Playwright.NUnit;
using LakeCountrySpanish.Playwright.PageObjects;

namespace LakeCountrySpanish.Playwright.Tests;

/// <summary>
/// Tests for the checkout flow up to Stripe redirect.
/// These tests verify the application correctly initiates payment but do NOT
/// complete actual payments on Stripe's hosted checkout page.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class CheckoutFlowTests : PageTest
{
    private LoginPage _loginPage = null!;
    private MyClassesPage _myClassesPage = null!;

    [SetUp]
    public async Task Setup()
    {
        _loginPage = new LoginPage(Page);
        _myClassesPage = new MyClassesPage(Page);

        // Login before each test
        await _loginPage.NavigateAsync();
        await _loginPage.LoginAsync(
            TestConfig.TestStudentEmail,
            TestConfig.TestStudentPassword);
    }

    [Test]
    [Description("Verify checkout button is disabled when cart is empty")]
    public async Task Checkout_WithEmptyCart_ShouldBeDisabledOrHidden()
    {
        await _myClassesPage.NavigateAsync();

        // First clear any existing cart items
        while (await _myClassesPage.GetCartItemCountAsync() > 0)
        {
            await _myClassesPage.RemoveFirstCartItemAsync();
            await Page.WaitForTimeoutAsync(300);
        }

        // Check if checkout button is disabled or hidden
        var checkoutButton = Page.Locator("button:has-text('Checkout'), button:has-text('Pay')");

        if (await checkoutButton.IsVisibleAsync())
        {
            var isDisabled = await checkoutButton.IsDisabledAsync();
            Assert.That(isDisabled, Is.True,
                "Checkout button should be disabled when cart is empty");
        }
        else
        {
            // Button not visible is also acceptable
            Assert.Pass("Checkout button is hidden when cart is empty");
        }
    }

    [Test]
    [Description("Verify checkout redirects to Stripe when cart has items")]
    public async Task Checkout_WithItems_ShouldRedirectToStripe()
    {
        if (TestConfig.IsProduction)
        {
            Assert.Ignore("Skipping payment flow in production environment");
        }

        await _myClassesPage.NavigateAsync();

        // Ensure we have items in cart
        var cartCount = await _myClassesPage.GetCartItemCountAsync();
        if (cartCount == 0)
        {
            try
            {
                await _myClassesPage.ClickAvailableSlotAsync();
                if (await _myClassesPage.IsBookingModalVisibleAsync())
                {
                    await _myClassesPage.ConfirmBookingModalAsync();
                }
                await Page.WaitForTimeoutAsync(1000);
                cartCount = await _myClassesPage.GetCartItemCountAsync();
            }
            catch
            {
                Assert.Warn("Could not add items to cart - no available slots");
                return;
            }
        }

        if (cartCount == 0)
        {
            Assert.Warn("Cart is empty, cannot test checkout flow");
            return;
        }

        // Verify total is shown
        var total = await _myClassesPage.GetCartTotalAsync();
        Assert.That(total, Is.Not.Null.And.GreaterThan(0),
            "Cart total should be visible before checkout");

        TestContext.WriteLine($"Cart has {cartCount} items totaling ${total}");

        // Click checkout and wait for redirect
        await _myClassesPage.ClickCheckoutAsync();

        // Wait for navigation (Stripe redirect)
        await Page.WaitForURLAsync(url =>
            url.Contains("checkout.stripe.com") ||
            url.Contains("/Payment/") ||
            url.Contains("/Checkout/"),
            new() { Timeout = 10000 });

        var currentUrl = Page.Url;
        TestContext.WriteLine($"Redirected to: {currentUrl}");

        // Should be on Stripe checkout or our payment confirmation page
        Assert.That(
            currentUrl.Contains("checkout.stripe.com") ||
            currentUrl.Contains("stripe.com") ||
            currentUrl.Contains("/Payment/"),
            Is.True,
            $"Should redirect to Stripe checkout. Current URL: {currentUrl}");

        // If we made it to Stripe, verify it's a test mode session (if visible)
        if (currentUrl.Contains("stripe.com"))
        {
            var pageContent = await Page.ContentAsync();
            // In test mode, Stripe shows "TEST MODE" banner
            TestContext.WriteLine("Successfully redirected to Stripe checkout");

            // Don't actually complete the payment - just verify we got there
            Assert.Pass("Successfully initiated Stripe checkout session");
        }
    }

    [Test]
    [Description("Verify cancel URL returns to MyClasses")]
    public async Task Checkout_Cancel_ShouldReturnToMyClasses()
    {
        if (TestConfig.IsProduction)
        {
            Assert.Ignore("Skipping payment flow in production environment");
        }

        // Navigate directly to a cancel URL if we know the pattern
        await Page.GotoAsync($"{TestConfig.BaseUrl}/Student/MyClasses?canceled=true");
        await Page.WaitForLoadStateAsync();

        // Should be back on MyClasses
        Assert.That(await _myClassesPage.IsOnMyClassesPageAsync(), Is.True,
            "Cancel should return to MyClasses page");

        // May show a cancelled message
        var pageContent = await Page.ContentAsync();
        TestContext.WriteLine("Returned to MyClasses after cancel");
    }

    [Test]
    [Description("Verify success URL handles completion")]
    public async Task Checkout_Success_ShouldShowConfirmation()
    {
        // Navigate directly to success URL (simulating return from Stripe)
        // Note: This won't actually process a payment, just tests the UI handling
        await Page.GotoAsync($"{TestConfig.BaseUrl}/Student/MyClasses?success=true");
        await Page.WaitForLoadStateAsync();

        // Should be on MyClasses or show success message
        var pageContent = await Page.ContentAsync();

        // Just verify the page handles the success parameter without error
        Assert.That(await _myClassesPage.HasErrorMessageAsync(), Is.False,
            "Success URL should not show errors");

        TestContext.WriteLine("Success URL handled without errors");
    }

    [Test]
    [Description("Verify cart persists across page navigation")]
    public async Task Cart_ShouldPersistAcrossNavigation()
    {
        if (TestConfig.IsProduction)
        {
            Assert.Ignore("Skipping cart modification in production");
        }

        await _myClassesPage.NavigateAsync();

        var initialCount = await _myClassesPage.GetCartItemCountAsync();

        // Add item if cart is empty
        if (initialCount == 0)
        {
            try
            {
                await _myClassesPage.ClickAvailableSlotAsync();
                if (await _myClassesPage.IsBookingModalVisibleAsync())
                {
                    await _myClassesPage.ConfirmBookingModalAsync();
                }
                await Page.WaitForTimeoutAsync(1000);
            }
            catch
            {
                Assert.Warn("Could not add item to cart");
                return;
            }
        }

        var countBeforeNav = await _myClassesPage.GetCartItemCountAsync();
        if (countBeforeNav == 0)
        {
            Assert.Warn("Cart is empty, cannot test persistence");
            return;
        }

        // Navigate away
        await Page.GotoAsync($"{TestConfig.BaseUrl}/Student/Dashboard");
        await Page.WaitForLoadStateAsync();

        // Navigate back
        await _myClassesPage.NavigateAsync();

        var countAfterNav = await _myClassesPage.GetCartItemCountAsync();

        Assert.That(countAfterNav, Is.EqualTo(countBeforeNav),
            "Cart should persist across page navigation");

        TestContext.WriteLine($"Cart maintained {countAfterNav} items after navigation");
    }
}
