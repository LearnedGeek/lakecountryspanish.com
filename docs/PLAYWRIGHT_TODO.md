# Playwright E2E Testing - Progress Tracker

## Overview

Playwright is set up for end-to-end testing of the Lake Country Spanish web application. Tests cover authentication, scheduling, checkout flows, and public pages.

**Project Location:** `src/LakeCountrySpanish.Playwright/`

---

## Setup Instructions

### First-Time Setup

1. **Install Playwright browsers:**
   ```bash
   cd src/LakeCountrySpanish.Playwright
   pwsh bin/Debug/net8.0/playwright.ps1 install
   ```
   Or on Windows:
   ```cmd
   powershell bin\Debug\net8.0\playwright.ps1 install
   ```

2. **Configure test credentials:**
   Set environment variables or update `TestConfig.cs`:
   ```bash
   # Windows PowerShell
   $env:TEST_BASE_URL = "https://localhost:5001"
   $env:TEST_STUDENT_EMAIL = "teststudent@test.com"
   $env:TEST_STUDENT_PASSWORD = "Test123!"
   ```

3. **Build the project:**
   ```bash
   dotnet build src/LakeCountrySpanish.Playwright
   ```

### Running Tests

```bash
# Run all Playwright tests
dotnet test src/LakeCountrySpanish.Playwright

# Run specific test class
dotnet test src/LakeCountrySpanish.Playwright --filter "FullyQualifiedName~AuthenticationTests"

# Run with visible browser (headed mode)
dotnet test src/LakeCountrySpanish.Playwright -- Playwright:LaunchOptions:Headless=false

# Run against production (read-only tests)
$env:TEST_ENVIRONMENT = "production"
$env:TEST_BASE_URL = "https://lakecountryspanish.com"
dotnet test src/LakeCountrySpanish.Playwright --filter "FullyQualifiedName~PublicPagesTests"
```

---

## Test Coverage Status

### ✅ Implemented Tests

| Test Class | Tests | Status | Notes |
|------------|-------|--------|-------|
| **AuthenticationTests** | 7 | ✅ Complete | Login, logout, access control |
| **StudentDashboardTests** | 7 | ✅ Complete | Dashboard loading, navigation |
| **ClassSchedulingTests** | 7 | ✅ Complete | Calendar, cart, booking |
| **CheckoutFlowTests** | 5 | ✅ Complete | Checkout up to Stripe redirect |
| **PublicPagesTests** | 9 | ✅ Complete | Home, About, Privacy, SEO |

### 🔲 Planned Tests (TODO)

| Test Class | Priority | Description |
|------------|----------|-------------|
| **AdminDashboardTests** | Medium | Admin panel functionality |
| **ContactFormTests** | Medium | Contact form submission |
| **PasswordChangeTests** | High | Forced password change, voluntary change |
| **SubscriptionTests** | Medium | Subscription plan selection |
| **EmailNotificationTests** | Low | Verify emails are sent (requires mailhog) |
| **MobileResponsiveTests** | Medium | Test on various viewport sizes |
| **AccessibilityTests** | Low | WCAG compliance checks |

---

## Test Details

### AuthenticationTests
- [x] Login page loads correctly
- [x] Successful login redirects to dashboard
- [x] Invalid password shows error
- [x] Non-existent email shows error
- [x] Protected pages redirect to login
- [x] Logout clears session
- [x] Remember me checkbox exists

### StudentDashboardTests
- [x] Dashboard loads after login
- [x] Welcome message displays
- [x] Gamification elements visible
- [x] Schedule classes link works
- [x] Token store link works
- [x] No error messages displayed
- [x] Navigation menu visible

### ClassSchedulingTests
- [x] MyClasses page loads with calendar
- [x] Cart starts empty
- [x] Week navigation works
- [x] Clicking available slot adds to cart
- [x] Cart total reflects items
- [x] Remove from cart works
- [x] No error messages displayed

### CheckoutFlowTests
- [x] Checkout disabled when cart empty
- [x] Checkout redirects to Stripe
- [x] Cancel URL returns to MyClasses
- [x] Success URL handles completion
- [x] Cart persists across navigation

### PublicPagesTests
- [x] Home page loads
- [x] About page loads
- [x] Contact page loads
- [x] Privacy page has content
- [x] Mobile navigation toggle
- [x] SEO meta tags present
- [x] No console errors
- [x] Footer on all pages
- [x] No 404 links

---

## Configuration

### TestConfig.cs Settings

| Setting | Default | Environment Variable |
|---------|---------|---------------------|
| BaseUrl | https://localhost:5001 | TEST_BASE_URL |
| TestStudentEmail | teststudent@test.com | TEST_STUDENT_EMAIL |
| TestStudentPassword | Test123! | TEST_STUDENT_PASSWORD |
| TestAdminEmail | admin@lakecountryspanish.com | TEST_ADMIN_EMAIL |
| TestAdminPassword | Admin123! | TEST_ADMIN_PASSWORD |
| IsProduction | false | TEST_ENVIRONMENT=production |

### playwright.runsettings

```xml
<Playwright>
  <BrowserName>chromium</BrowserName>
  <LaunchOptions>
    <Headless>true</Headless>
    <SlowMo>0</SlowMo>
  </LaunchOptions>
</Playwright>
```

---

## Page Objects

| Page Object | Location | Purpose |
|-------------|----------|---------|
| BasePage | PageObjects/BasePage.cs | Common functionality |
| LoginPage | PageObjects/LoginPage.cs | Authentication |
| StudentDashboardPage | PageObjects/StudentDashboardPage.cs | Dashboard interactions |
| MyClassesPage | PageObjects/MyClassesPage.cs | Scheduling & cart |

---

## Payment Testing Strategy

Playwright tests the flow **up to** Stripe redirect but cannot complete payments on Stripe's hosted checkout.

### What Playwright CAN test:
- Cart totals calculate correctly
- Checkout button creates valid session
- Redirect to Stripe occurs
- Return URLs (success/cancel) work

### What requires manual testing:
- Completing payment on Stripe checkout
- Webhook processing after payment
- Payment confirmation in database

### Recommended approach:
1. Run Playwright tests to verify UI flow
2. Use `stripe listen --forward-to localhost:5001/api/payment/webhook` for webhook testing
3. Manual test with $1 payment before production launch

---

## CI/CD Integration

Add to your CI pipeline:

```yaml
- name: Install Playwright
  run: pwsh src/LakeCountrySpanish.Playwright/bin/Debug/net8.0/playwright.ps1 install chromium

- name: Run Playwright Tests
  run: dotnet test src/LakeCountrySpanish.Playwright --logger "trx;LogFileName=playwright-results.trx"
  env:
    TEST_BASE_URL: ${{ secrets.TEST_URL }}
    TEST_STUDENT_EMAIL: ${{ secrets.TEST_EMAIL }}
    TEST_STUDENT_PASSWORD: ${{ secrets.TEST_PASSWORD }}
```

---

## Troubleshooting

### Common Issues

**Browser not installed:**
```
Error: Executable doesn't exist at ...
```
Solution: Run `pwsh playwright.ps1 install`

**Timeout on login:**
Check that TEST_BASE_URL is correct and the app is running.

**Tests fail in CI but pass locally:**
- Ensure headless mode is enabled
- Check that test credentials are set in CI secrets
- Verify network access to test environment

**Cart tests fail with "no available slots":**
The test student may have no available time slots. Either:
- Add available slots in admin panel
- Use a different test account
- Skip the test with `Assert.Warn()`

---

## Future Enhancements

1. **Visual regression testing** - Add screenshot comparisons
2. **Performance testing** - Measure page load times
3. **API testing** - Test REST endpoints directly
4. **Parallel execution** - Run tests across multiple browsers
5. **Test data setup** - Add fixtures for consistent test state

---

## Changelog

| Date | Changes |
|------|---------|
| 2025-01-15 | Initial Playwright setup with core test suites |

---

## Manual Testing Procedures

### Pre-Launch Testing Checklist

Before going live, complete these manual tests in addition to running Playwright:

#### 1. Payment Flow (Critical)

**Setup:**
```bash
# Terminal 1 - Run the app locally
cd src/LakeCountrySpanish.Web
dotnet run

# Terminal 2 - Forward Stripe webhooks
stripe listen --forward-to https://localhost:5001/api/payment/webhook
```

**Test Steps:**
1. Login as test student
2. Go to /Student/MyClasses
3. Add a class to cart
4. Click Checkout
5. On Stripe checkout, use test card: `4242 4242 4242 4242` (any future date, any CVC)
6. Complete payment
7. Verify:
   - [ ] Redirected to success page
   - [ ] Payment shows as "Completed" in database
   - [ ] Class shows in "Upcoming Classes"
   - [ ] Webhook received (check stripe listen terminal)

**Test Refund:**
1. Go to Stripe Dashboard → Payments
2. Find the test payment and issue refund
3. Verify webhook updates payment status to "Refunded"

#### 2. Subscription Flow

1. Login as test student without subscription
2. Go to /Subscription/Plans
3. Select a plan and checkout
4. Verify:
   - [ ] Subscription created in Stripe
   - [ ] Subscription shows in student dashboard
   - [ ] Tickets/classes allocated correctly

#### 3. Email Deliverability

1. Trigger a test email (class reminder, payment confirmation)
2. Check inbox (including spam folder)
3. Use [mail-tester.com](https://mail-tester.com) to check spam score

#### 4. Cross-Browser Quick Check

Test these critical flows in each browser:
- [ ] Chrome: Login → Dashboard → Schedule class
- [ ] Firefox: Login → Dashboard → Schedule class
- [ ] Safari: Login → Dashboard → Schedule class
- [ ] Edge: Login → Dashboard → Schedule class
- [ ] Mobile (Chrome/Safari): Login → Dashboard

#### 5. Admin Functions

Login as admin and verify:
- [ ] Can view all students
- [ ] Can manage schedule/time slots
- [ ] Can approve testimonials
- [ ] Can view payment history
- [ ] Dashboard analytics load

### Production Smoke Test

After deploying to production, run these quick checks:

```bash
# Run read-only tests against production
$env:TEST_ENVIRONMENT = "production"
$env:TEST_BASE_URL = "https://lakecountryspanish.com"
dotnet test src/LakeCountrySpanish.Playwright --filter "FullyQualifiedName~PublicPagesTests"
```

**Manual checks:**
- [ ] Home page loads with correct branding
- [ ] SSL certificate valid (padlock in browser)
- [ ] Login page accessible
- [ ] Contact form visible
- [ ] No console errors (F12 → Console)

### Test Credentials Reference

| Environment | Email | Password | Notes |
|-------------|-------|----------|-------|
| Local Dev | teststudent@test.com | Test123! | Seeded by SeedData.cs |
| Local Admin | admin@lakecountryspanish.com | Admin123! | Seeded by SeedData.cs |
| Production | (create test account) | (secure password) | Create via registration |

### Stripe Test Cards

| Card Number | Scenario |
|-------------|----------|
| 4242 4242 4242 4242 | Success |
| 4000 0000 0000 0002 | Declined |
| 4000 0000 0000 3220 | 3D Secure required |
| 4000 0025 0000 3155 | Requires authentication |

---

## Notes

- Tests marked with `IsProduction` check will skip write operations against production
- PublicPagesTests are safe to run against any environment
- Authentication tests require valid test credentials
- Checkout tests stop at Stripe redirect (don't complete payment)
