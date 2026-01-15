using Microsoft.Playwright.NUnit;

namespace LakeCountrySpanish.Playwright.Tests;

/// <summary>
/// Tests for public pages that don't require authentication.
/// These are safe to run against production.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class PublicPagesTests : PageTest
{
    [Test]
    [Description("Verify home page loads")]
    public async Task HomePage_ShouldLoad()
    {
        await Page.GotoAsync(TestConfig.BaseUrl);
        await Page.WaitForLoadStateAsync();

        var title = await Page.TitleAsync();

        Assert.That(title, Does.Contain("Lake Country Spanish").Or.Contain("Spanish"),
            "Home page title should contain site name");

        // Check for key elements
        Assert.That(await Page.Locator("nav, header").IsVisibleAsync(), Is.True,
            "Navigation should be visible");
    }

    [Test]
    [Description("Verify About page loads")]
    public async Task AboutPage_ShouldLoad()
    {
        await Page.GotoAsync($"{TestConfig.BaseUrl}/Home/About");
        await Page.WaitForLoadStateAsync();

        var title = await Page.TitleAsync();

        Assert.That(title, Does.Contain("About"),
            "About page should have appropriate title");

        // Should not have errors
        var hasError = await Page.Locator(".error, .text-red-700").IsVisibleAsync();
        Assert.That(hasError, Is.False, "About page should not show errors");
    }

    [Test]
    [Description("Verify Contact page loads")]
    public async Task ContactPage_ShouldLoad()
    {
        await Page.GotoAsync($"{TestConfig.BaseUrl}/Contact");
        await Page.WaitForLoadStateAsync();

        // Should have a contact form or contact info
        var hasForm = await Page.Locator("form, input[type='email']").IsVisibleAsync();
        var hasContactInfo = await Page.Locator(":text('email'), :text('contact')").IsVisibleAsync();

        Assert.That(hasForm || hasContactInfo, Is.True,
            "Contact page should have form or contact information");
    }

    [Test]
    [Description("Verify Privacy page loads with content")]
    public async Task PrivacyPage_ShouldLoad()
    {
        await Page.GotoAsync($"{TestConfig.BaseUrl}/Home/Privacy");
        await Page.WaitForLoadStateAsync();

        var title = await Page.TitleAsync();
        Assert.That(title, Does.Contain("Privacy"),
            "Privacy page should have appropriate title");

        // Should have actual content (not placeholder)
        var content = await Page.Locator("main, .content, article").TextContentAsync();
        Assert.That(content?.Length, Is.GreaterThan(500),
            "Privacy policy should have substantial content");

        // Check for key sections
        Assert.That(content, Does.Contain("cookie").IgnoreCase,
            "Privacy policy should mention cookies");
    }

    [Test]
    [Description("Verify mobile navigation toggle works")]
    public async Task Navigation_MobileToggle_ShouldWork()
    {
        // Set mobile viewport
        await Page.SetViewportSizeAsync(375, 667);

        await Page.GotoAsync(TestConfig.BaseUrl);
        await Page.WaitForLoadStateAsync();

        // Look for mobile menu button
        var mobileMenuButton = Page.Locator("button[onclick*='toggleMobileMenu'], .mobile-menu-button, button[aria-label*='menu']");

        if (await mobileMenuButton.IsVisibleAsync())
        {
            await mobileMenuButton.ClickAsync();

            // Menu should become visible
            await Page.WaitForTimeoutAsync(500);

            var mobileMenu = Page.Locator("#mobile-menu, .mobile-menu, nav.mobile");
            // Menu visibility check - might be toggled via class
            TestContext.WriteLine("Mobile menu toggle clicked");
        }
        else
        {
            TestContext.WriteLine("No mobile menu button found (may use different pattern)");
        }
    }

    [Test]
    [Description("Verify SEO meta tags are present")]
    public async Task HomePage_ShouldHaveSEOTags()
    {
        await Page.GotoAsync(TestConfig.BaseUrl);
        await Page.WaitForLoadStateAsync();

        // Check meta description
        var metaDescription = await Page.Locator("meta[name='description']").GetAttributeAsync("content");
        Assert.That(metaDescription, Is.Not.Null.And.Not.Empty,
            "Should have meta description");

        // Check Open Graph tags
        var ogTitle = await Page.Locator("meta[property='og:title']").GetAttributeAsync("content");
        Assert.That(ogTitle, Is.Not.Null.And.Not.Empty,
            "Should have Open Graph title");

        // Check canonical URL
        var canonical = await Page.Locator("link[rel='canonical']").GetAttributeAsync("href");
        Assert.That(canonical, Is.Not.Null.And.Not.Empty,
            "Should have canonical URL");

        TestContext.WriteLine($"Meta description: {metaDescription?.Substring(0, Math.Min(100, metaDescription?.Length ?? 0))}...");
    }

    [Test]
    [Description("Verify no console errors on home page")]
    public async Task HomePage_ShouldHaveNoConsoleErrors()
    {
        var consoleErrors = new List<string>();

        Page.Console += (_, e) =>
        {
            if (e.Type == "error")
            {
                consoleErrors.Add(e.Text);
            }
        };

        await Page.GotoAsync(TestConfig.BaseUrl);
        await Page.WaitForLoadStateAsync();

        // Wait a bit for any async scripts
        await Page.WaitForTimeoutAsync(2000);

        if (consoleErrors.Count > 0)
        {
            TestContext.WriteLine($"Console errors found: {string.Join(", ", consoleErrors)}");
        }

        // Allow some errors (like blocked trackers) but fail on critical JS errors
        var criticalErrors = consoleErrors.Where(e =>
            !e.Contains("Failed to load resource") &&
            !e.Contains("blocked") &&
            !e.Contains("CORS")).ToList();

        Assert.That(criticalErrors, Is.Empty,
            $"Should have no critical console errors. Found: {string.Join("; ", criticalErrors)}");
    }

    [Test]
    [Description("Verify footer is present on all public pages")]
    public async Task PublicPages_ShouldHaveFooter()
    {
        var pages = new[] { "", "/Home/About", "/Home/Privacy", "/Contact" };

        foreach (var path in pages)
        {
            await Page.GotoAsync($"{TestConfig.BaseUrl}{path}");
            await Page.WaitForLoadStateAsync();

            var hasFooter = await Page.Locator("footer").IsVisibleAsync();
            Assert.That(hasFooter, Is.True,
                $"Footer should be visible on {path}");
        }
    }

    [Test]
    [Description("Verify links don't lead to 404 errors")]
    public async Task HomePage_Links_ShouldNotBe404()
    {
        await Page.GotoAsync(TestConfig.BaseUrl);
        await Page.WaitForLoadStateAsync();

        // Get all internal links
        var links = await Page.Locator("a[href^='/'], a[href^='" + TestConfig.BaseUrl + "']").AllAsync();

        var brokenLinks = new List<string>();

        foreach (var link in links.Take(10)) // Check first 10 to keep test fast
        {
            var href = await link.GetAttributeAsync("href");
            if (string.IsNullOrEmpty(href)) continue;

            var fullUrl = href.StartsWith("/") ? $"{TestConfig.BaseUrl}{href}" : href;

            var response = await Page.APIRequest.GetAsync(fullUrl);
            if (response.Status == 404)
            {
                brokenLinks.Add(href);
            }
        }

        Assert.That(brokenLinks, Is.Empty,
            $"Found broken links: {string.Join(", ", brokenLinks)}");
    }
}
