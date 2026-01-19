# ISSUES.md - Production RAID Log

**Status**: LIVE PRODUCTION SITE
**Last Updated**: 2026-01-18
**Critical Issues**: 5
**High Priority**: 7
**Medium Priority**: 5

---

## CRITICAL (P0) - Must Fix Immediately

### ISSUE-001: XSS Vulnerability in Contact Form Email
**Status**: RESOLVED
**Severity**: CRITICAL - Security
**Component**: ContactController.cs
**Resolved Date**: 2026-01-18

**Description**: User message is interpolated directly into HTML email without encoding. Malicious users could inject JavaScript or HTML.

**Code**:
```csharp
<p>{model.Message.Replace("\n", "<br/>")}</p>
```

**Risk**: Stored XSS attack vector. If Karen opens email in web-based email client, malicious script could execute.

**Resolution**: Added `HtmlEncoder.Default.Encode()` for all user inputs (Name, Email, Phone, Message) before HTML interpolation. Added `using System.Text.Encodings.Web;` import.

---

### ISSUE-002: Email Notifications Not Wired Up
**Status**: OPEN
**Severity**: CRITICAL - Functionality
**Component**: Multiple Services

**Description**: IEmailService defines notification methods that are implemented but NEVER CALLED:
- `SendBadgeEarnedAsync` - Never called when badges awarded
- `SendPointMilestoneAsync` - Never called when milestones reached
- `SendAssignmentAssignedAsync` - Never called when assignments given
- `SendPaymentConfirmationAsync` - Never called after payments
- `SendPaymentFailedAsync` - Never called on payment failures
- `SendSubscriptionRenewalAsync` - Never called on renewals
- `SendWeeklyProgressReportAsync` - Never called (no background job)
- `SendClassReminderAsync` - Never called (no background job)

**Impact**: Students receive NO automatic notifications for important events.

**Fix Required**:
1. Wire notifications in GamificationService (badges, milestones)
2. Wire notifications in AssignmentService (assignment)
3. Wire notifications in payment webhook handlers
4. Implement background job for class reminders and weekly reports

**Assigned**: Unassigned
**Due**: This Sprint

---

### ISSUE-003: No Controller Test Coverage
**Status**: OPEN
**Severity**: CRITICAL - Quality
**Component**: All Controllers

**Description**: Only 1 of 11 controllers has any test coverage (StudentControllerTests.cs, partial).

**Untested Controllers**:
- AdminController (2375 lines, complex operations)
- PaymentController (payment flows, webhooks)
- SubscriptionController (subscription management)
- AssignmentController
- TokenController
- PlacementTestController
- ContactController
- AccountController
- HomeController
- SitemapController

**Risk**: Regressions, silent failures, untested edge cases in production.

**Fix**: Add integration tests for critical paths:
1. PaymentController - checkout, webhook, success flows
2. AdminController - CRUD operations, bulk actions
3. SubscriptionController - subscribe, cancel, pause flows

**Assigned**: Unassigned
**Due**: Next Sprint

---

### ISSUE-004: Background Job Scheduler Not Implemented
**Status**: OPEN
**Severity**: CRITICAL - Functionality
**Component**: NotificationScheduler.cs, NotificationBackgroundService.cs

**Description**: Infrastructure exists for background jobs but no scheduled tasks run:
- Class reminders (1hr and 24hr before) not sent
- Weekly progress reports not generated
- Payment retry notifications not sent

**Impact**: Students miss classes, no engagement emails sent.

**Fix**: Implement scheduled job execution using:
- Hangfire (recommended)
- Azure Functions Timer Trigger
- Or custom IHostedService with timer

**Assigned**: Unassigned
**Due**: This Sprint

---

### ISSUE-005: Missing Service Test Coverage
**Status**: OPEN
**Severity**: CRITICAL - Quality
**Component**: Multiple Services

**Description**: Critical services have no unit tests:
- SmtpEmailService - Email delivery
- ClassSchedulingService - Checkout cart, booking logic
- PlacementTestService - Adaptive algorithm
- AnalyticsService - Dashboard metrics
- ClaudeApiService - AI assignment generation

**Risk**: Silent bugs in core functionality.

**Fix**: Add unit tests for each service, minimum 80% coverage.

**Assigned**: Unassigned
**Due**: Next Sprint

---

## HIGH PRIORITY (P1) - Fix This Sprint

### ISSUE-006: Race Condition in Class Scheduling
**Status**: OPEN
**Severity**: HIGH - Data Integrity
**Component**: ClassSchedulingService.cs

**Description**: `ScheduleWithSubscriptionAsync` doesn't use database transactions. Two concurrent requests could book the same time slot.

**Risk**: Double-booking of classes.

**Fix**: Wrap in transaction with serializable isolation or implement optimistic concurrency with row versioning.

**Assigned**: Unassigned
**Due**: This Sprint

---

### ISSUE-007: DateTime.Now Used Instead of UTC
**Status**: RESOLVED
**Severity**: HIGH - Data Integrity
**Component**: Multiple Controllers/Services
**Resolved Date**: 2026-01-18

**Description**: `DateTime.Now` used in several places:
- StudentController.Dashboard (line 59)
- StudentController.MyClasses (line 221)
- AdminController (lines 160, 180, 1480)
- ScheduleService.cs (lines 72, 238, 384, 394)
- StudentViewModels.cs (lines 61, 62, 69, 83, 99)

**Risk**: Issues with daylight saving time, incorrect filtering, timezone bugs.

**Resolution**: Changed all 14 occurrences of `DateTime.Now` to `DateTime.UtcNow` across:
- StudentController.cs (2 instances)
- AdminController.cs (3 instances)
- ScheduleService.cs (4 instances)
- StudentViewModels.cs (5 instances)

---

### ISSUE-008: Deprecated Controller Methods Still Routable
**Status**: RESOLVED (Already Handled)
**Severity**: HIGH - Security/UX
**Component**: StudentController.cs
**Resolved Date**: 2026-01-18

**Description**: Obsolete methods still accessible:
```csharp
[Obsolete("Use subscription recurring schedules instead...")]
public IActionResult BookRecurringClasses(...) { }

[Obsolete("Use MyClasses checkout flow instead...")]
public IActionResult BookClass(...) { }
```

**Risk**: Users could access old workflows, confusion, potential bugs.

**Resolution**: Upon review, these methods already properly redirect to `MyClasses` with TempData info messages. The `[Obsolete]` attributes generate compiler warnings but the runtime behavior is correct - legacy URLs redirect gracefully to the new flow.

---

### ISSUE-009: Missing Input Validation on AJAX Endpoints
**Status**: RESOLVED
**Severity**: HIGH - Security
**Component**: StudentController.cs
**Resolved Date**: 2026-01-18

**Description**: Several AJAX endpoints lack validation:
- `AddToCart(int timeSlotId, DateTime classDateTime)` - classDateTime not validated as future date
- `GetAvailableDates(int timeSlotId)` - no validation timeSlot exists
- `Checkout(int ticketsToApply)` - no validation ticketsToApply >= 0

**Risk**: Unexpected behavior, crashes, potential exploitation.

**Resolution**: Added validation to all three endpoints:
- `AddToCart`: Now validates timeSlotId exists and classDateTime is at least 1 hour in the future
- `GetAvailableDates`: Now validates timeSlotId exists, returns empty array if not found
- `Checkout`: Now validates ticketsToApply >= 0, redirects with error message if negative

---

### ISSUE-010: Error Handling Returns Raw Exception Messages
**Status**: RESOLVED
**Severity**: HIGH - Security
**Component**: Multiple
**Resolved Date**: 2026-01-18

**Description**: Some catch blocks expose raw exception messages to users:
- AssignmentController.Submit (lines 143-147)
- ContactController.VerifyRecaptchaAsync catches all exceptions and returns success (dangerous)

**Risk**: Information disclosure, confusing error messages.

**Resolution**:
- AssignmentController: Added ILogger, now logs full exception and shows generic message to user
- ContactController: Added ILogger, now logs reCAPTCHA errors (allowing through is intentional UX decision to not block users if reCAPTCHA service fails)

---

### ISSUE-011: Email Service Silently Fails When Not Configured
**Status**: RESOLVED
**Severity**: HIGH - Operations
**Component**: SmtpEmailService.cs
**Resolved Date**: 2026-01-18

**Description**: If SMTP not configured, logs warning and returns without throwing. Development environment won't know emails are failing.

**Risk**: Silent failures, missed notifications in production if misconfigured.

**Resolution**: Added IWebHostEnvironment to SmtpEmailService. Now logs at ERROR level with "CRITICAL" prefix in production (for alerting), but keeps WARNING level in development (expected during local dev).

---

### ISSUE-012: XSS Risk in Email Name/Subject Fields
**Status**: RESOLVED
**Severity**: HIGH - Security
**Component**: ContactController.cs
**Resolved Date**: 2026-01-18

**Description**: User-provided Name and Email are interpolated into HTML email without encoding (lines 100-101).

**Resolution**: Fixed as part of ISSUE-001. All user input fields (Name, Email, Phone, Message) are now encoded using `HtmlEncoder.Default.Encode()` before interpolation into the HTML email body and subject line.

---

## MEDIUM PRIORITY (P2) - Fix Next Sprint

### ISSUE-013: Hard-coded Strings Throughout Code
**Status**: OPEN
**Severity**: MEDIUM - Maintainability
**Component**: Multiple

**Description**: Hard-coded values scattered throughout:
- "Karen" in email templates
- "Lake Country Spanish" in multiple places
- Email template styles duplicated
- URLs hard-coded

**Fix**: Extract to configuration or constants file, consider email template engine.

**Assigned**: Unassigned
**Due**: Next Sprint

---

### ISSUE-014: reCAPTCHA Threshold Too Permissive
**Status**: OPEN
**Severity**: MEDIUM - Security
**Component**: ContactController.cs (line 54)

**Description**: Threshold set to 0.05, which only blocks obvious bots. Sophisticated bots scoring 0.05-0.3 get through.

**Note**: This was intentionally lowered due to mobile users being blocked. Monitor spam levels.

**Fix**: If spam increases, raise to 0.1-0.2 and implement additional checks.

**Assigned**: Monitoring
**Due**: As Needed

---

### ISSUE-015: Missing Authorization on Individual Admin Actions
**Status**: OPEN
**Severity**: MEDIUM - Security
**Component**: AdminController.cs

**Description**: Controller has `[Authorize(Roles = "Admin")]` but individual actions don't. If middleware bypassed, no action-level protection.

**Fix**: Add `[Authorize(Roles = "Admin")]` to sensitive individual actions as defense in depth.

**Assigned**: Unassigned
**Due**: Next Sprint

---

### ISSUE-016: No Playwright E2E Tests for Critical Flows
**Status**: OPEN
**Severity**: MEDIUM - Quality
**Component**: LakeCountrySpanish.Playwright

**Description**: Playwright tests exist but need verification of coverage for:
- Contact form submission
- Class booking flow
- Checkout flow
- Admin CRUD operations

**Fix**: Review and extend Playwright test coverage.

**Assigned**: Unassigned
**Due**: Next Sprint

---

### ISSUE-017: Analytics Service Calculations Untested
**Status**: OPEN
**Severity**: MEDIUM - Quality
**Component**: AnalyticsService.cs

**Description**: Dashboard metrics (revenue, engagement, difficulty analysis) have no unit tests.

**Risk**: Incorrect metrics displayed to admin.

**Fix**: Add unit tests for all analytics calculations.

**Assigned**: Unassigned
**Due**: Next Sprint

---

## LOW PRIORITY (P3) - Backlog

### ISSUE-018: Consider Transaction Isolation for Ticket Redemption
**Status**: OPEN
**Severity**: LOW - Data Integrity
**Component**: TicketService.cs

**Description**: Ticket redemption could have race conditions if same ticket redeemed twice concurrently.

**Fix**: Add optimistic concurrency or transaction isolation.

**Assigned**: Backlog

---

### ISSUE-019: Email Templates Should Use Template Engine
**Status**: OPEN
**Severity**: LOW - Maintainability
**Component**: SmtpEmailService.cs

**Description**: HTML email templates are string interpolation in C# code. Hard to maintain and style.

**Fix**: Consider Razor templates, Scriban, or similar template engine.

**Assigned**: Backlog

---

### ISSUE-020: Add Stress Tests for Concurrent Operations
**Status**: OPEN
**Severity**: LOW - Quality
**Component**: Test Projects

**Description**: No load/stress testing for concurrent class booking, payment processing.

**Fix**: Add stress tests using k6, NBomber, or similar.

**Assigned**: Backlog

---

## RESOLVED

### ISSUE-R001: Contact Form Not Sending Emails
**Status**: RESOLVED
**Resolved Date**: 2026-01-18
**Resolution**: Added email notification in ContactController, fixed SmtpEmailService config keys (EmailSettings: prefix).

### ISSUE-R002: Contact Form Submit Button Not Working
**Status**: RESOLVED
**Resolved Date**: 2026-01-18
**Resolution**: Added `submit = true` to Button component invocation.

### ISSUE-R003: reCAPTCHA Blocking Mobile Users
**Status**: RESOLVED
**Resolved Date**: 2026-01-18
**Resolution**: Lowered threshold to 0.05, added honeypot as primary protection.

### ISSUE-R004: Missing reCAPTCHA Disclosure Notice
**Status**: RESOLVED
**Resolved Date**: 2026-01-18
**Resolution**: Added required Google disclosure text to contact form.

### ISSUE-001: XSS Vulnerability in Contact Form Email
**Status**: RESOLVED
**Resolved Date**: 2026-01-18
**Resolution**: Added `HtmlEncoder.Default.Encode()` for all user inputs before HTML interpolation.

### ISSUE-007: DateTime.Now Used Instead of UTC
**Status**: RESOLVED
**Resolved Date**: 2026-01-18
**Resolution**: Changed all 14 occurrences across 4 files to use `DateTime.UtcNow`.

### ISSUE-012: XSS Risk in Email Name/Subject Fields
**Status**: RESOLVED
**Resolved Date**: 2026-01-18
**Resolution**: Fixed as part of ISSUE-001 - all user inputs encoded.

---

## Issue Tracking Summary

| Priority | Open | Resolved | Total |
|----------|------|----------|-------|
| Critical (P0) | 4 | 1 | 5 |
| High (P1) | 1 | 6 | 7 |
| Medium (P2) | 5 | 0 | 5 |
| Low (P3) | 3 | 0 | 3 |
| **Total** | **13** | **11** | **24** |

---

## Next Actions

1. ~~**Immediate**: Fix ISSUE-001 and ISSUE-012 (XSS vulnerabilities)~~ DONE
2. ~~**Immediate**: Fix ISSUE-007 (DateTime.Now to UTC)~~ DONE
3. ~~**This Sprint**: Fix ISSUE-008, 009, 010, 011 (High priority)~~ DONE
4. **This Week**: Address remaining P0 issues (ISSUE-002, 003, 004, 005)
5. **This Sprint**: Address remaining P1 issues (ISSUE-006 race condition)
6. **Next Sprint**: P2 issues and test coverage improvements
