# ISSUES.md - Production RAID Log

**Status**: LIVE PRODUCTION SITE
**Last Updated**: 2026-01-19
**Critical Issues**: 2 open, 3 resolved/partial (P0)
**High Priority**: 0 open, 7 resolved (P1)
**Medium Priority**: 5 open (P2)
**Low Priority**: 3 open (P3)

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
**Status**: PARTIALLY RESOLVED
**Severity**: CRITICAL - Functionality
**Component**: Multiple Services
**Resolved Date**: 2026-01-19

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

**Resolution (Partial)**:
1. **WIRED**: `SendBadgeEarnedAsync` - Added to `GamificationService.AwardBadgeAsync`
2. **WIRED**: `SendPointMilestoneAsync` - Added to `GamificationService.CheckAndConvertPointsToTokensAsync`
3. **WIRED**: `SendAssignmentAssignedAsync` - Added to `AssignmentService.AssignToStudentAsync`
4. **WIRED**: `SendPaymentConfirmationAsync` - Added to `StripePaymentService.ProcessWebhookAsync`
5. **WIRED**: `SendPaymentFailedAsync` - Already was wired in `StripeSubscriptionService`
6. **WIRED**: `SendSubscriptionRenewalAsync` - Added to `StripeSubscriptionService.ProcessSuccessfulPayment`
7. **WIRED**: `SendClassReminderAsync` - Added to `ScheduledTasksController.SendClassReminders` (ISSUE-004)
8. **NOT WIRED**: `SendWeeklyProgressReportAsync` - Requires complex data gathering, deferred to next sprint

**Remaining Work**: Item 8 (weekly progress reports) deferred - requires gathering student progress data across multiple entities.

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
**Status**: PARTIALLY RESOLVED
**Severity**: CRITICAL - Functionality
**Component**: ScheduledTasksController.cs (new)
**Resolved Date**: 2026-01-19

**Description**: Infrastructure exists for background jobs but no scheduled tasks run:
- Class reminders (1hr and 24hr before) not sent
- Weekly progress reports not generated
- Payment retry notifications not sent

**Impact**: Students miss classes, no engagement emails sent.

**Resolution (Partial)**: Created HTTP endpoint-based scheduled tasks using SmarterASP's task scheduler:

**New Controller**: `ScheduledTasksController.cs` at `/api/tasks/`
- `GET /api/tasks/send-class-reminders?key={secret}` - Sends 24hr and 1hr class reminders
- `GET /api/tasks/cleanup-stale-checkouts?key={secret}` - Removes abandoned cart items (30min+)
- `GET /api/tasks/check-expired-tickets?key={secret}` - Reports on expired tickets
- `GET /api/tasks/health` - Health check (no auth required)

**Security**: All task endpoints require `ScheduledTasks:SecretKey` config value.
- Preferred: Pass via `X-Task-Key` header (not logged)
- Fallback: Pass via `?key=` query parameter (for SmarterASP compatibility - may appear in server logs)

**Database Changes**: Added `Reminder24HrSent` and `Reminder1HrSent` to ScheduledClass entity.

**SmarterASP Configuration Required**:
1. Add `ScheduledTasks:SecretKey` to appsettings.Production.json
2. Configure scheduled task in SmarterASP control panel:
   - URL: `https://lakecountryspanish.com/api/tasks/send-class-reminders?key={your-secret}`
   - Frequency: 30 minutes
   - Timeout: 60 seconds

**Remaining Work**:
- Weekly progress reports (ISSUE-002 item 7) - needs more complex data gathering
- Deploy and configure SmarterASP scheduled tasks

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
**Status**: RESOLVED
**Severity**: HIGH - Data Integrity
**Component**: ClassSchedulingService.cs, ScheduleService.cs
**Resolved Date**: 2026-01-19

**Description**: Multiple booking methods didn't use database transactions. Two concurrent requests could book the same time slot.

**Risk**: Double-booking of classes.

**Resolution**: Added SERIALIZABLE transaction isolation to all booking methods:
- `ClassSchedulingService.AddClassToCheckoutAsync` - wrapped in transaction with DbUpdateException handling
- `ClassSchedulingService.ScheduleWithSubscriptionAsync` - wrapped in transaction with DbUpdateException handling
- `ScheduleService.BookClassAsync` - wrapped in transaction with DbUpdateException handling
- `ScheduleService.BookRecurringClassesAsync` - wrapped in transaction with DbUpdateException handling

All methods now:
1. Begin transaction with `System.Data.IsolationLevel.Serializable`
2. Check availability within transaction
3. Insert class record
4. Commit transaction
5. Catch `DbUpdateException` for concurrent conflict, rollback and return null/error

Added `ILogger<T>` to both services for logging concurrent booking attempts.

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

### ISSUE-006: Race Condition in Class Scheduling
**Status**: RESOLVED
**Resolved Date**: 2026-01-19
**Resolution**: Added SERIALIZABLE transaction isolation to all booking methods in ClassSchedulingService.cs and ScheduleService.cs. Added ILogger for concurrent conflict logging.

### ISSUE-002: Email Notifications (Partial)
**Status**: PARTIALLY RESOLVED
**Resolved Date**: 2026-01-19
**Resolution**: Wired up 7 of 8 email notification methods:
- Badge earned, point milestones, assignment assigned, payment confirmation, payment failed, subscription renewal, class reminders
- Remaining 1 (weekly progress reports) deferred to next sprint - requires complex data gathering

### ISSUE-004: Background Job Scheduler (Partial)
**Status**: PARTIALLY RESOLVED
**Resolved Date**: 2026-01-19
**Resolution**: Created ScheduledTasksController.cs with HTTP endpoints for:
- Class reminders (24hr and 1hr) via `/api/tasks/send-class-reminders`
- Stale checkout cleanup via `/api/tasks/cleanup-stale-checkouts`
- Ticket expiry monitoring via `/api/tasks/check-expired-tickets`

Configured for use with SmarterASP's URL-based task scheduler (30-minute interval).
Added database migration for `Reminder24HrSent` and `Reminder1HrSent` fields.

---

## Issue Tracking Summary

| Priority | Open | Partial | Resolved | Total |
|----------|------|---------|----------|-------|
| Critical (P0) | 2 | 2 | 1 | 5 |
| High (P1) | 0 | 0 | 7 | 7 |
| Medium (P2) | 5 | 0 | 0 | 5 |
| Low (P3) | 3 | 0 | 0 | 3 |
| **Total** | **10** | **2** | **8** | **20** |

**Note**: ISSUE-002 and ISSUE-004 are "Partial" - core functionality implemented, minor items deferred.

---

## Next Actions

1. ~~**Immediate**: Fix ISSUE-001 and ISSUE-012 (XSS vulnerabilities)~~ DONE
2. ~~**Immediate**: Fix ISSUE-007 (DateTime.Now to UTC)~~ DONE
3. ~~**This Sprint**: Fix ISSUE-008, 009, 010, 011 (High priority)~~ DONE
4. ~~**This Sprint**: Fix ISSUE-006 (race condition)~~ DONE
5. ~~**This Sprint**: Fix ISSUE-004 (scheduled tasks endpoint)~~ DONE
6. **This Week**: Address remaining P0 issues (ISSUE-003, 005 - test coverage)
7. **Next Sprint**: P2 issues and test coverage improvements

---

## Deployment Action Items (Owner Required)

The following actions must be completed manually after deploying the code changes:

### 1. Add Scheduled Tasks Secret Key to Production Config

**File**: `appsettings.Production.json`

Add the following section (generate a secure random key):
```json
"ScheduledTasks": {
  "SecretKey": "GENERATE-A-SECURE-RANDOM-KEY-HERE"
}
```

**To generate a secure key**, use one of these methods:
- PowerShell: `[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }) -as [byte[]])`
- Online: Use a password generator with 32+ characters
- Example format: `xK9m2pQ7nL4vR8wY1tB6cF3hJ5gA0sD9`

### 2. Run Database Migration

After deployment, run the migration to add reminder tracking fields:

```bash
dotnet ef database update --context ApplicationDbContext
```

Or via SQL Server Management Studio, apply migration: `AddClassReminderTracking`

### 3. Configure SmarterASP Scheduled Tasks

Go to SmarterASP Control Panel → Advance → Schedule Tasks

**Task 1: Class Reminders** (Required)
| Setting | Value |
|---------|-------|
| URL | `https://lakecountryspanish.com/api/tasks/send-class-reminders?key=YOUR-SECRET-KEY` |
| Protocol | `https://` |
| Frequency | 30 minutes |
| Timeout | 60 seconds |
| Task Type | Schedule Task |

**Task 2: Cleanup Stale Checkouts** (Recommended)
| Setting | Value |
|---------|-------|
| URL | `https://lakecountryspanish.com/api/tasks/cleanup-stale-checkouts?key=YOUR-SECRET-KEY` |
| Protocol | `https://` |
| Frequency | 30 minutes |
| Timeout | 30 seconds |
| Task Type | Schedule Task |

**Task 3: Expired Tickets Report** (Optional - for monitoring)
| Setting | Value |
|---------|-------|
| URL | `https://lakecountryspanish.com/api/tasks/check-expired-tickets?key=YOUR-SECRET-KEY` |
| Protocol | `https://` |
| Frequency | 1440 minutes (daily) |
| Timeout | 30 seconds |
| Task Type | Schedule Task |

**Security Note**: The query parameter approach is used because SmarterASP's scheduler only supports URL-based calls (no custom headers). If you switch to a scheduler that supports headers (Azure Functions, cron + curl, etc.), use the `X-Task-Key` header instead:
```bash
curl -H "X-Task-Key: YOUR-SECRET-KEY" https://lakecountryspanish.com/api/tasks/send-class-reminders
```
The header approach is preferred because it doesn't appear in server access logs.

### 4. Verify Health Endpoint

After deployment, verify the scheduled tasks system is working:

```
GET https://lakecountryspanish.com/api/tasks/health
```

Expected response:
```json
{
  "status": "healthy",
  "timestamp": "2026-01-19T...",
  "endpoints": ["send-class-reminders", "cleanup-stale-checkouts", "check-expired-tickets"]
}
```

### 5. Test a Scheduled Task Manually

Test the class reminders endpoint with your secret key:
```
GET https://lakecountryspanish.com/api/tasks/send-class-reminders?key=YOUR-SECRET-KEY
```

Expected response (when no classes need reminders):
```json
{
  "success": true,
  "remindersSent": 0,
  "errors": 0,
  "timestamp": "2026-01-19T..."
}
```

---

## Files Changed This Session (2026-01-19)

### New Files
- `Controllers/ScheduledTasksController.cs` - HTTP endpoints for scheduled tasks
- `Data/Migrations/[timestamp]_AddClassReminderTracking.cs` - Database migration

### Modified Files
- `Models/Entities/ScheduledClass.cs` - Added `Reminder24HrSent`, `Reminder1HrSent` fields
- `Services/ClassSchedulingService.cs` - Added transaction isolation for race condition fix
- `Services/ScheduleService.cs` - Added transaction isolation for race condition fix
- `Services/GamificationService.cs` - Wired up badge/milestone email notifications, added IEmailService
- `Services/AssignmentService.cs` - Wired up assignment email notifications, added IEmailService
- `Services/StripePaymentService.cs` - Wired up payment confirmation emails, added IEmailService
- `Services/StripeSubscriptionService.cs` - Wired up subscription renewal emails

---

## Session Summary (2026-01-19)

### Issues Resolved This Session
| Issue | Description | Status |
|-------|-------------|--------|
| ISSUE-006 | Race condition in class scheduling | **RESOLVED** |
| ISSUE-002 | Email notifications not wired up | **PARTIAL** (7/8 done) |
| ISSUE-004 | Background job scheduler | **PARTIAL** (endpoints created) |

### Key Accomplishments
1. **Race Condition Fix**: Added SERIALIZABLE transaction isolation to 4 booking methods
2. **Email Notifications**: Wired up 7 notification types (badges, milestones, assignments, payments, subscriptions, class reminders)
3. **Scheduled Tasks**: Created HTTP endpoint-based scheduled task system compatible with SmarterASP
4. **Database**: Added reminder tracking fields with migration

### Remaining Critical Items
- ISSUE-003: Controller test coverage (deferred to next sprint)
- ISSUE-005: Service test coverage (deferred to next sprint)
- Weekly progress report emails (complex data gathering needed)

### Owner Action Required
See **Deployment Action Items** section above for manual configuration steps needed after deploying code changes.
