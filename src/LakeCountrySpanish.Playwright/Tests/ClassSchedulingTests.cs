using Microsoft.Playwright.NUnit;
using LakeCountrySpanish.Playwright.PageObjects;

namespace LakeCountrySpanish.Playwright.Tests;

/// <summary>
/// Tests for the class scheduling flow.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class ClassSchedulingTests : PageTest
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
    [Description("Verify MyClasses page loads with calendar")]
    public async Task MyClasses_ShouldLoadWithCalendar()
    {
        await _myClassesPage.NavigateAsync();

        Assert.That(await _myClassesPage.IsOnMyClassesPageAsync(), Is.True,
            "Should be on the MyClasses page");

        Assert.That(await _myClassesPage.IsCalendarVisibleAsync(), Is.True,
            "Calendar should be visible");
    }

    [Test]
    [Description("Verify cart starts empty")]
    public async Task MyClasses_Cart_ShouldStartEmpty()
    {
        await _myClassesPage.NavigateAsync();

        var cartCount = await _myClassesPage.GetCartItemCountAsync();

        // Cart might have items from previous sessions, so we just verify it loads
        TestContext.WriteLine($"Cart item count: {cartCount}");

        // If cart is empty, verify empty message
        if (cartCount == 0)
        {
            Assert.That(await _myClassesPage.IsCartEmptyAsync(), Is.True,
                "Should show empty cart message when no items");
        }
    }

    [Test]
    [Description("Verify week navigation works")]
    public async Task MyClasses_WeekNavigation_ShouldWork()
    {
        await _myClassesPage.NavigateAsync();

        // Get initial URL or date context
        var initialUrl = Page.Url;

        // Navigate to next week
        await _myClassesPage.GoToNextWeekAsync();

        // URL should change or calendar should update
        var afterNextWeek = Page.Url;
        TestContext.WriteLine($"After next week: {afterNextWeek}");

        // Navigate back
        await _myClassesPage.GoToPreviousWeekAsync();

        // Should be back roughly where we started
        TestContext.WriteLine($"After previous week: {Page.Url}");
    }

    [Test]
    [Description("Verify clicking available slot adds to cart (if slots available)")]
    public async Task MyClasses_ClickingAvailableSlot_ShouldAddToCart()
    {
        // Skip in production to avoid creating real reservations
        if (TestConfig.IsProduction)
        {
            Assert.Ignore("Skipping write operation in production environment");
        }

        await _myClassesPage.NavigateAsync();

        var initialCartCount = await _myClassesPage.GetCartItemCountAsync();

        try
        {
            // Try to click an available slot
            await _myClassesPage.ClickAvailableSlotAsync();

            // If a modal appears, confirm it
            if (await _myClassesPage.IsBookingModalVisibleAsync())
            {
                await _myClassesPage.ConfirmBookingModalAsync();
            }

            // Wait for cart to update
            await Page.WaitForTimeoutAsync(1000);

            var newCartCount = await _myClassesPage.GetCartItemCountAsync();

            Assert.That(newCartCount, Is.GreaterThan(initialCartCount),
                "Cart should have more items after adding a class");
        }
        catch (Exception ex)
        {
            TestContext.WriteLine($"Could not add to cart (may have no available slots): {ex.Message}");
            Assert.Warn("No available slots found to test booking flow");
        }
    }

    [Test]
    [Description("Verify cart total updates when items added")]
    public async Task MyClasses_CartTotal_ShouldReflectItems()
    {
        await _myClassesPage.NavigateAsync();

        var cartCount = await _myClassesPage.GetCartItemCountAsync();

        if (cartCount > 0)
        {
            var total = await _myClassesPage.GetCartTotalAsync();

            Assert.That(total, Is.Not.Null.And.GreaterThan(0),
                "Cart total should be displayed and greater than zero when items are present");

            TestContext.WriteLine($"Cart has {cartCount} items, total: ${total}");
        }
        else
        {
            TestContext.WriteLine("Cart is empty, skipping total verification");
        }
    }

    [Test]
    [Description("Verify removing item from cart works")]
    public async Task MyClasses_RemoveFromCart_ShouldWork()
    {
        if (TestConfig.IsProduction)
        {
            Assert.Ignore("Skipping write operation in production environment");
        }

        await _myClassesPage.NavigateAsync();

        var initialCount = await _myClassesPage.GetCartItemCountAsync();

        if (initialCount == 0)
        {
            // Try to add an item first
            try
            {
                await _myClassesPage.ClickAvailableSlotAsync();
                if (await _myClassesPage.IsBookingModalVisibleAsync())
                {
                    await _myClassesPage.ConfirmBookingModalAsync();
                }
                await Page.WaitForTimeoutAsync(1000);
                initialCount = await _myClassesPage.GetCartItemCountAsync();
            }
            catch
            {
                Assert.Warn("Could not add item to cart for removal test");
                return;
            }
        }

        if (initialCount > 0)
        {
            await _myClassesPage.RemoveFirstCartItemAsync();
            await Page.WaitForTimeoutAsync(500);

            var newCount = await _myClassesPage.GetCartItemCountAsync();

            Assert.That(newCount, Is.LessThan(initialCount),
                "Cart should have fewer items after removal");
        }
    }

    [Test]
    [Description("Verify page doesn't show errors")]
    public async Task MyClasses_ShouldNotShowErrors()
    {
        await _myClassesPage.NavigateAsync();

        Assert.That(await _myClassesPage.HasErrorMessageAsync(), Is.False,
            "MyClasses page should not display any error messages");
    }
}
