# SpanishScheduler Test Execution Plan

> **Context:** This plan was developed by analyzing the project's current test coverage against the standards in `docs/TESTING-STRATEGY.md`, `docs/HARDENING.md`, and the global `CLAUDE.md`. It is designed to be executed by Claude CLI in sequential phases.
>
> **Current state (as of 2026-02-22):** 273 tests passing (234 xUnit unit tests + 35 Playwright E2E tests). Only 2 of 12 controllers have any test coverage. ~146 controller action methods exist; ~25 have tests.

---

## Before You Start

**Read these files first — they define the rules:**
- `C:\Users\mcart\.claude\CLAUDE.md` — Global development standards (testing rules, code quality, etc.)
- `docs/TESTING-STRATEGY.md` — Testing patterns and anti-patterns
- `docs/HARDENING.md` — Production hardening reference

**Mandatory before writing any test code:**
1. Run `dotnet test src/LakeCountrySpanish.Tests/` — all tests must pass before you touch anything
2. Run `dotnet build src/LakeCountrySpanish.Web/` — 0 errors, 0 warnings
3. Read `src/LakeCountrySpanish.Tests/TestDbContextFactory.cs` to understand the DB isolation pattern
4. Read 2-3 existing test files to understand conventions (recommended: `TicketServiceTests.cs`, `StudentControllerTests.cs`, `StatusBadgeViewComponentTests.cs`)

**Conventions to follow (discovered from existing tests):**
- xUnit with `[Fact]` and `[Theory]`/`[InlineData]`
- Moq for external dependencies
- Real `ApplicationDbContext` via `TestDbContextFactory.Create()` (unique in-memory DB per test class)
- `IDisposable` pattern for test cleanup
- Constructor-based setup (no `IClassFixture` currently used)
- Naming: `MethodName_Scenario_ExpectedResult`
- `#region` sections to organize related tests within a file
- Helper methods at end of test class (e.g., `CreateTestStudent()`)

---

## Phase 1: CI Pipeline — Add Test Execution

**Goal:** Tests run automatically. A broken build cannot be deployed.

**Priority:** CRITICAL — Without this, all other testing work is unprotected.

### Task 1.1: Add CI test workflow

Create `.github/workflows/ci.yml` that runs on push to `main` and on pull requests:

```yaml
name: Build and Test

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build-and-test:
    name: Build and Test
    runs-on: windows-latest

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --configuration Release --no-restore

      - name: Run unit tests
        run: dotnet test src/LakeCountrySpanish.Tests/ --configuration Release --no-build --verbosity normal --logger "trx;LogFileName=test-results.trx" --filter "Category!=StripeIntegration"

      # Playwright tests excluded from CI — they require a running app instance
      # and browser binaries. Run locally or in a dedicated E2E pipeline.
```

### Task 1.2: Add test gate to deploy workflow

Edit `.github/workflows/deploy.yml` to add a test step between Build and Publish:

```yaml
      - name: Run tests
        run: dotnet test src/LakeCountrySpanish.Tests/ --configuration Release --no-build --filter "Category!=StripeIntegration"
```

Insert this step immediately after the `Build` step and before the `Publish` step.

### Task 1.3: Commit Phase 1

Commit message: "Add CI test execution to build and deploy pipelines"

---

## Phase 2: Priority 1 — Payment & Webhook Controllers

**Goal:** Cover the code paths that handle real money.

**Priority:** HIGH — Payment bugs cost real dollars and erode trust.

**Reference:** TESTING-STRATEGY.md Section 16, Priority 1: "Payment/financial operations — charges, refunds, balance changes"

### Task 2.1: PaymentController tests

Create `src/LakeCountrySpanish.Tests/Controllers/PaymentControllerTests.cs`

**Constructor dependencies to mock:**
```csharp
ApplicationDbContext _context          // Use TestDbContextFactory.Create()
UserManager<ApplicationUser>           // Mock
IPaymentService _paymentService        // Mock
IConfiguration _configuration          // Mock
```

**Action methods to test (8 total):**

| Method | Signature | Tests Needed |
|--------|-----------|-------------|
| `Checkout` | GET | Returns view with pending classes from cart; redirects if cart empty |
| `CreateCheckoutSession` | POST | Calls payment service; redirects to Stripe; handles service failure |
| `Success` | GET | Handles valid session ID; handles missing/invalid session ID |
| `Cancel` | GET | Returns cancel view; clears checkout state |
| `Webhook` | POST `[AllowAnonymous]` | Signature validation; payment success handling; unknown event type; malformed payload |
| `BuyPackage` | GET | Returns view with package details; handles invalid package ID |

**Key test scenarios:**
1. `Webhook_ReturnsOk_ForValidPaymentIntent` — Verify the webhook handler processes `payment_intent.succeeded` correctly
2. `Webhook_ReturnsBadRequest_ForInvalidSignature` — Security: invalid Stripe signatures must be rejected
3. `Webhook_ReturnsOk_ForUnhandledEventTypes` — Unrecognized events should return 200 (not crash)
4. `CreateCheckoutSession_RedirectsToStripe_WhenCartHasItems`
5. `CreateCheckoutSession_RedirectsWithError_WhenCartEmpty`
6. `Success_UpdatesClassStatus_WhenSessionValid`
7. `Checkout_ReturnsNotFound_WhenUserNotAuthenticated`

### Task 2.2: SubscriptionController tests

Create `src/LakeCountrySpanish.Tests/Controllers/SubscriptionControllerTests.cs`

**Constructor dependencies to mock:**
```csharp
ApplicationDbContext _context
UserManager<ApplicationUser>
ISubscriptionService _subscriptionService
IConfiguration _configuration
```

**Action methods to test (10 total):**

| Method | Tests Needed |
|--------|-------------|
| `Plans` | Returns view with available plans |
| `Subscribe` | Creates Stripe checkout; handles errors |
| `Success` | Processes successful subscription |
| `Manage` | Shows subscription management; handles no subscription |
| `SetSchedule` | Updates recurring schedule; validates input |
| `RequestCancellation` | Cancels subscription; sends notification |
| `Pause` | Pauses subscription |
| `Resume` | Resumes paused subscription |
| `Portal` | Redirects to Stripe customer portal |
| `Webhook` | Handles subscription lifecycle events |

**Key test scenarios:**
1. `Webhook_HandlesSubscriptionCreated_CreatesLocalRecord`
2. `Webhook_HandlesSubscriptionCancelled_UpdatesStatus`
3. `Subscribe_RedirectsToLogin_WhenNotAuthenticated`
4. `RequestCancellation_SendsAdminNotification`
5. `Manage_ReturnsNotFound_WhenNoActiveSubscription`

### Task 2.3: StudentController cart & checkout tests

Expand `src/LakeCountrySpanish.Tests/Controllers/StudentControllerTests.cs`

**New test regions to add (currently only deprecated booking and CancelClass tested):**

| Method | Tests Needed |
|--------|-------------|
| `AddToCart` | Adds class to pending; rejects past dates; rejects within 24hr cutoff; rejects duplicates; rejects conflicts |
| `RemoveFromCart` | Removes pending class; ignores non-existent; ignores other student's class |
| `GetCheckoutSummary` | Returns correct count and total; empty cart returns zero |
| `GetPendingCart` | Returns only current user's pending classes |
| `ClearCart` | Removes all pending classes; does not delete paid classes |
| `Checkout` | Creates Stripe session; redirects; handles empty cart |
| `CheckoutSuccess` | Updates class status from Pending to Scheduled; handles invalid session |
| `Dashboard` | Returns view with upcoming classes, stats, gamification data |
| `MyClasses` | Returns schedule view with correct week navigation |

**Critical tests (from prior commit — "CRITICAL: Prevent deleting paid classes when clearing cart"):**
1. `ClearCart_OnlyDeletesPendingClasses_NotPaidOrScheduled` — Regression test for the paid-class deletion bug
2. `AddToCart_RejectsClassWithin24Hours` — Validates the booking cutoff
3. `AddToCart_RejectsConflictingTimeSlot` — No double-booking

### Task 2.4: Commit Phase 2

Commit message: "Add payment, subscription, and cart controller tests"

---

## Phase 3: Priority 2 — Admin Controller

**Goal:** Cover the platform's management operations.

**Priority:** HIGH — Admin actions affect all users and have no undo.

**Reference:** TESTING-STRATEGY.md Section 16, Priority 2: "Admin operations — privilege escalation, data modification"

### Task 3.1: AdminController tests — Student Management

Create `src/LakeCountrySpanish.Tests/Controllers/AdminControllerTests.cs`

**Constructor dependencies to mock (13 total):**
```csharp
ApplicationDbContext _context              // TestDbContextFactory.Create()
UserManager<ApplicationUser>               // Mock
IPaymentService _paymentService            // Mock
IWebHostEnvironment _environment           // Mock
IEmailService _emailService                // Mock
IScheduleService _scheduleService          // Mock
ITokenService _tokenService                // Mock
ITicketService _ticketService              // Mock
IGamificationService _gamificationService  // Mock
IAssignmentService _assignmentService      // Mock
IConfiguration _configuration              // Mock
IAnalyticsService _analyticsService        // Mock
ILogger<AdminController> _logger           // Mock
```

**NOTE:** This controller has 54+ action methods. Do NOT try to test all of them in one sitting. Organize by region and prioritize by risk.

**Student Management region:**

| Method | Tests Needed |
|--------|-------------|
| `Students` | Returns view with student list |
| `StudentProfile` | Returns student details; handles invalid ID |
| `CreateStudent` GET | Returns create form |
| `CreateStudent` POST | Creates user; sends welcome email; handles duplicate email |
| `EditStudent` GET | Returns edit form with student data |
| `EditStudent` POST | Updates student; handles validation errors |

### Task 3.2: AdminController tests — Schedule Management

| Method | Tests Needed |
|--------|-------------|
| `TimeSlots` | Returns all time slots |
| `CreateTimeSlot` POST | Creates slot; validates no overlap |
| `DeleteTimeSlot` | Deletes only unused slots; prevents deleting slots with scheduled classes |
| `DeactivateTimeSlot` / `ReactivateTimeSlot` | Toggle IsActive flag |
| `Schedule` | Returns schedule view with classes |
| `CompleteClass` | Marks class completed; awards gamification points |
| `CancelClass` | Cancels class; handles notification |
| `CancelClassWithNotification` | Sends student notification email |
| `RescheduleClass` GET/POST | Shows available dates; reschedules and notifies |
| `SetClassUrl` | Updates classroom URL |

### Task 3.3: AdminController tests — Token/Ticket/Gamification Management

| Method | Tests Needed |
|--------|-------------|
| `TokenPermissions` | Returns permissions list |
| `GrantTokenPermission` POST | Grants permission; validates student exists |
| `DisableTokenPermission` | Disables permission |
| `GrantTokens` POST | Grants tokens to student |
| `StudentTokens` | Returns token history |
| `GrantTickets` POST | Grants tickets |
| `StudentGamification` | Returns gamification stats |
| `AdjustPoints` POST | Adjusts points up/down |
| `ResetStreaks` | Resets student streaks |

### Task 3.4: AdminController tests — Package, BlockedDate, and Testimonial Management

| Method | Tests Needed |
|--------|-------------|
| `Packages` | Returns package list |
| `CreatePackage` / `EditPackage` | CRUD operations |
| `BlockedDates` | Returns blocked dates |
| `CreateBlockedDate` / `DeleteBlockedDate` | CRUD operations |
| `Testimonials` | Returns testimonials with status |
| `ApproveTestimonial` / `FeatureTestimonial` / `HideTestimonial` | Status transitions |

### Task 3.5: AdminController tests — Assignment Management

| Method | Tests Needed |
|--------|-------------|
| `Assignments` | Returns assignment list filtered by status |
| `GenerateAssignment` POST | Calls AI service; handles failure |
| `CreateAssignment` POST | Creates manual assignment |
| `ReviewAssignment` / `ApproveAssignment` / `RejectAssignment` | Status transitions |
| `AssignToStudents` POST | Assigns to multiple students |

### Task 3.6: AdminController tests — Repair & Reconciliation

| Method | Tests Needed |
|--------|-------------|
| `RepairPendingCheckouts` | Fixes stuck pending classes |
| `OrphanedPayments` | Lists payments without matching classes |
| `CreateClassForPayment` | Creates class for orphaned payment |

**Key scenarios across all admin tests:**
1. Every POST action should verify the database was actually updated (reload entity after action)
2. Invalid entity IDs should return NotFound or redirect with error
3. Email notifications should be verified via `_emailServiceMock.Verify()`

### Task 3.7: Commit Phase 3

Commit message: "Add comprehensive AdminController test coverage"

---

## Phase 4: Priority 3 — Auth, Contact, and Remaining Controllers

**Goal:** Cover authentication flows and remaining untested controllers.

**Reference:** TESTING-STRATEGY.md Section 16, Priority 1: "Auth flows — login, registration, token refresh, role checks"

### Task 4.1: AccountController tests

Create `src/LakeCountrySpanish.Tests/Controllers/AccountControllerTests.cs`

**Constructor dependencies to mock:**
```csharp
SignInManager<ApplicationUser>    // Mock (requires Mock<IUserStore>, etc.)
UserManager<ApplicationUser>      // Mock
```

**Tests needed:**

| Method | Tests Needed |
|--------|-------------|
| `Login` GET | Returns login view |
| `Login` POST | Valid credentials redirect by role (Admin→Admin/Dashboard, Student→Student/Dashboard); invalid credentials show error; lockout handling |
| `Logout` GET | Returns logout confirmation |
| `LogoutPost` POST | Signs out and redirects to home |
| `AccessDenied` | Returns access denied view |
| `ChangePassword` GET | Returns change password form |
| `ChangePassword` POST | Changes password; validates old password; role-based redirect after change |

**Critical test: `Login_Post_RedirectsAdminToDashboard` and `Login_Post_RedirectsStudentToDashboard`** — These verify role-based routing works correctly.

### Task 4.2: ContactController tests

Create `src/LakeCountrySpanish.Tests/Controllers/ContactControllerTests.cs`

**Constructor dependencies to mock:**
```csharp
ApplicationDbContext _context
IConfiguration _configuration
IHttpClientFactory _httpClientFactory    // Mock
IEmailService _emailService              // Mock
ILogger<ContactController> _logger       // Mock
```

**Tests needed:**

| Method | Tests Needed |
|--------|-------------|
| `Index` GET | Returns contact form view |
| `Index` POST | Valid submission saves inquiry and sends email; honeypot field triggers silent rejection; reCAPTCHA failure shows error; HTML encoding of user input (XSS prevention) |
| `ThankYou` | Returns thank you view |

**Critical tests:**
1. `Index_Post_RejectsSubmission_WhenHoneypotFilled` — Bot detection
2. `Index_Post_HtmlEncodesUserInput` — XSS prevention
3. `Index_Post_SavesInquiryToDatabase` — Data persistence
4. `Index_Post_SendsAdminNotificationEmail` — Admin notification

**Note on reCAPTCHA testing:** The reCAPTCHA verification calls an external HTTP endpoint. Mock `IHttpClientFactory` to return a fake `HttpClient` that returns a controlled response. Do NOT call the real Google reCAPTCHA API in tests.

### Task 4.3: HomeController tests

Create `src/LakeCountrySpanish.Tests/Controllers/HomeControllerTests.cs`

**Constructor dependencies to mock:**
```csharp
ILogger<HomeController> _logger
ApplicationDbContext _context
```

**Tests needed:**

| Method | Tests Needed |
|--------|-------------|
| `Index` | Returns view with pricing tiers and featured testimonials |
| `About` | Returns view |
| `Privacy` | Returns view |
| `AreasWeServe` | Returns view |
| `Error` | Returns error view |

### Task 4.4: PlacementTestController tests

Create `src/LakeCountrySpanish.Tests/Controllers/PlacementTestControllerTests.cs`

**Constructor dependencies to mock:**
```csharp
IPlacementTestService _placementTestService
UserManager<ApplicationUser>
```

**Tests needed:**

| Method | Tests Needed |
|--------|-------------|
| `Index` | Shows test intro; redirects if test in progress |
| `Start` POST | Creates new test session |
| `Question` GET | Returns current question; handles test complete |
| `Submit` POST | Submits answer; advances to next question |
| `Results` | Shows test results; handles no results |
| `Abandon` POST | Abandons in-progress test |
| `Resume` | Resumes abandoned test |

### Task 4.5: AssignmentController tests

Create `src/LakeCountrySpanish.Tests/Controllers/AssignmentControllerTests.cs`

**Constructor dependencies to mock:**
```csharp
IAssignmentService _assignmentService
UserManager<ApplicationUser>
ILogger<AssignmentController>
```

**Tests needed:**

| Method | Tests Needed |
|--------|-------------|
| `Index` | Returns assignments for current user |
| `Take` | Returns assignment to complete; handles already submitted |
| `Submit` POST | Submits assignment; validates required fields |
| `Results` | Shows graded results |
| `History` | Shows past assignments |
| `Skip` POST | Skips assignment |

### Task 4.6: TokenController tests

Create `src/LakeCountrySpanish.Tests/Controllers/TokenControllerTests.cs`

**Constructor dependencies to mock:**
```csharp
ITokenService _tokenService
ILogger<TokenController>
```

**Tests needed:**

| Method | Tests Needed |
|--------|-------------|
| `Index` | Returns token balance and store |
| `Purchase` POST | Creates Stripe checkout for tokens |
| `Success` | Handles successful token purchase |
| `History` | Returns token transaction history |

### Task 4.7: SitemapController tests

Create `src/LakeCountrySpanish.Tests/Controllers/SitemapControllerTests.cs`

**Constructor dependencies to mock:**
```csharp
IConfiguration _configuration
```

**Tests needed:**
1. `Index_ReturnsXmlContent` — Verify content type is `application/xml`
2. `Index_ContainsRequiredUrls` — Verify sitemap includes key pages

### Task 4.8: Commit Phase 4

Commit message: "Add auth, contact, and remaining controller tests"

---

## Phase 5: Service Coverage Gaps

**Goal:** Cover services that have no tests or incomplete tests.

### Task 5.1: ClassSchedulingService tests

Create `src/LakeCountrySpanish.Tests/Services/ClassSchedulingServiceTests.cs`

Read `src/LakeCountrySpanish.Web/Services/ClassSchedulingService.cs` first to understand the interface.

**Key methods to test:**
- `CleanupStalePendingClassesAsync` — Cleans up abandoned cart items older than N minutes
- Any booking validation logic
- Conflict detection

### Task 5.2: PlacementTestService tests

Create `src/LakeCountrySpanish.Tests/Services/PlacementTestServiceTests.cs`

Read the service first. Test the adaptive question selection logic, scoring, and level determination.

### Task 5.3: AnalyticsService tests

Create `src/LakeCountrySpanish.Tests/Services/AnalyticsServiceTests.cs`

Read the service first. Test data aggregation methods.

### Task 5.4: Expand SmtpEmailService tests

The existing `SmtpEmailServiceTests.cs` has 35 tests but primarily tests logging behavior (whether emails are logged). Consider adding tests that verify:
- Email subject lines contain expected content
- Template helper methods produce valid HTML structure
- Admin email skip behavior when `AdminEmail` is not configured

### Task 5.5: Expand ScheduleService tests

The existing `ScheduleServiceTests.cs` has 14 tests. Add coverage for:
- `GetCancellationStatusAsync` — Tests for within/outside 24hr window
- `CancelClassWithForfeitAsync` — Tests for credit forfeiture logic
- `BookRecurringClassesAsync` — Tests for recurring booking
- `CanBookClass` — Tests for 24-hour cutoff (regression test for recent change)
- Calendar slot generation with blocked dates

### Task 5.6: Commit Phase 5

Commit message: "Add service test coverage for scheduling, placement, and analytics"

---

## Phase 6: ScheduledTasksController — Complete Coverage

**Goal:** The existing tests cover authentication only. Add tests for the actual task logic.

### Task 6.1: Expand ScheduledTasksController tests

Add to `src/LakeCountrySpanish.Tests/Controllers/ScheduledTasksControllerTests.cs`

**Missing coverage:**

| Method | Missing Tests |
|--------|-------------|
| `CheckExpiredTickets` | Auth validation; calls ticket service; returns count |
| `SendClassReminders` | Email failure doesn't crash the endpoint; handles students with no email |
| `CleanupStaleCheckouts` | Calls service with correct timeout parameter |

### Task 6.2: Commit Phase 6

Commit message: "Complete ScheduledTasksController test coverage"

---

## Phase 7: Code Coverage Reporting

**Goal:** Make coverage visible and track improvement over time.

### Task 7.1: Add coverage runsettings

Create `src/LakeCountrySpanish.Tests/coverage.runsettings`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat Code Coverage">
        <Configuration>
          <Format>cobertura</Format>
          <ExcludeByFile>**/Migrations/**</ExcludeByFile>
          <ExcludeByAttribute>ExcludeFromCodeCoverage</ExcludeByAttribute>
          <SkipAutoProps>true</SkipAutoProps>
          <IncludeTestAssembly>false</IncludeTestAssembly>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

### Task 7.2: Update CI to collect coverage

Update `.github/workflows/ci.yml` test step:

```yaml
      - name: Run unit tests with coverage
        run: dotnet test src/LakeCountrySpanish.Tests/ --configuration Release --no-build --verbosity normal --filter "Category!=StripeIntegration" --collect:"XPlat Code Coverage" --settings src/LakeCountrySpanish.Tests/coverage.runsettings
```

### Task 7.3: Commit Phase 7

Commit message: "Add code coverage reporting configuration"

---

## Execution Rules

These rules apply to ALL phases. Violating them will create problems.

### Before each phase:
1. `dotnet build src/LakeCountrySpanish.Web/` — must produce 0 errors, 0 warnings
2. `dotnet test src/LakeCountrySpanish.Tests/` — all existing tests must pass
3. Read the source code you're about to test — understand it before writing tests

### While writing tests:
4. **Use `TestDbContextFactory.Create()`** for database contexts — never `new ApplicationDbContext()`
5. **Use `Moq`** for service dependencies — never call real external services
6. **One logical assertion per test** — don't test 5 things in one method
7. **Follow the existing naming convention:** `MethodName_Scenario_ExpectedResult`
8. **Use `#region` sections** to organize tests by method/feature area
9. **Do NOT add packages** to the test project unless absolutely necessary
10. **Do NOT create base classes** unless you see repeated setup across 3+ test files

### After each phase:
11. `dotnet build` — 0 errors, 0 warnings
12. `dotnet test` — all tests pass (old AND new)
13. Commit with a descriptive message
14. Report the new test count

### Do NOT:
- Write tests that depend on test execution order
- Write tests that depend on external services (Stripe, SMTP, Google reCAPTCHA)
- Use `async void` in test methods
- Call `.Result` or `.Wait()` on async methods
- Introduce new test frameworks (stick with xUnit + Moq)
- Create a `WebApplicationFactory` or `IntegrationTestBase` — that's a future phase not in this plan
- Add comments or docstrings to code you didn't write
- Modify production code to make tests pass (except for genuine bugs you discover)

---

## Expected Outcomes

| Phase | New Test Files | Estimated New Tests | Cumulative Total |
|-------|---------------|-------------------|-----------------|
| 1 | 0 (CI only) | 0 | 273 |
| 2 | 2 new + 1 expanded | ~40-50 | ~320 |
| 3 | 1 large file | ~60-80 | ~390 |
| 4 | 7 new files | ~50-60 | ~450 |
| 5 | 3 new + 2 expanded | ~30-40 | ~485 |
| 6 | 1 expanded | ~5-8 | ~490 |
| 7 | 0 (config only) | 0 | ~490 |

**Target: ~490 tests** covering all 12 controllers and all critical services.

---

## Priority Justification (from TESTING-STRATEGY.md Section 16)

| Phase | Risk × Frequency | Justification |
|-------|-------------------|--------------|
| 1 — CI Pipeline | Infrastructure | "A test that doesn't run automatically might as well not exist" |
| 2 — Payment/Webhooks | **High risk, High frequency** | Handles real money; Stripe webhook errors lose revenue |
| 3 — Admin Controller | **High risk, Lower frequency** | Admin actions affect all users; 54 untested methods |
| 4 — Auth & Other Controllers | **High risk, High frequency** (auth) / Medium (others) | Auth is the most common failure point |
| 5 — Service Gaps | **Medium risk** | Business logic already partially covered |
| 6 — ScheduledTasks | **Medium risk, Lower frequency** | Background jobs; partial coverage exists |
| 7 — Coverage Reporting | Infrastructure | Track progress; prevent regression |

---

## Notes for the Executing Agent

- **AdminController has 13 constructor dependencies.** This is the most complex setup. Create a private helper method in the test class to build the controller with all mocks.
- **StudentController already has a test file.** Add new test regions to the existing file — do not create a second file.
- **ScheduledTasksController already has a test file.** Same — expand the existing file.
- **The `[Authorize]` attribute on controllers is NOT tested by unit tests.** Unit tests bypass the MVC pipeline. Auth attribute testing would require `WebApplicationFactory` integration tests (future work, not in this plan).
- **For `UserManager<ApplicationUser>` mocking**, follow the pattern in `StudentControllerTests.cs`:
  ```csharp
  var store = new Mock<IUserStore<ApplicationUser>>();
  var userManagerMock = new Mock<UserManager<ApplicationUser>>(
      store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
  ```
- **For `SignInManager<ApplicationUser>` mocking** (AccountController), you need:
  ```csharp
  var userStore = new Mock<IUserStore<ApplicationUser>>();
  var userManager = new Mock<UserManager<ApplicationUser>>(
      userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
  var contextAccessor = new Mock<IHttpContextAccessor>();
  var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
  var signInManager = new Mock<SignInManager<ApplicationUser>>(
      userManager.Object, contextAccessor.Object, claimsFactory.Object, null!, null!, null!, null!);
  ```
- **For `IHttpClientFactory` mocking** (ContactController reCAPTCHA), mock the factory to return a handler that returns a controlled JSON response:
  ```csharp
  var mockHandler = new Mock<HttpMessageHandler>();
  mockHandler.Protected()
      .Setup<Task<HttpResponseMessage>>("SendAsync",
          ItExpr.IsAny<HttpRequestMessage>(),
          ItExpr.IsAny<CancellationToken>())
      .ReturnsAsync(new HttpResponseMessage
      {
          StatusCode = HttpStatusCode.OK,
          Content = new StringContent("{\"success\": true, \"score\": 0.9}")
      });
  var httpClient = new HttpClient(mockHandler.Object);
  var httpClientFactoryMock = new Mock<IHttpClientFactory>();
  httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
  ```
- **Build and test after every file you create**, not at the end of a phase. Catching errors early prevents cascading fixes.
- **If a test requires modifying production code** (e.g., making a method virtual for mocking, or adding an interface), stop and flag it. Do not modify production code without explicit approval.
