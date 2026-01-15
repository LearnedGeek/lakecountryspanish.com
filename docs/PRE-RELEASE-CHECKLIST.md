# Lake Country Spanish - Pre-Release Checklist

## 1. Domain & Hosting

- [x] **Domain Registration**
  - Register domain name (lakecountryspanish.com) ✅
  - Configure DNS records to point to hosting provider ✅

- [x] **Hosting Setup**
  - Hosting provider: Site4Now (Azure-based) ✅
  - Server provisioned ✅
  - Deployment configured ✅

- [x] **SSL Certificate**
  - SSL certificate configured ✅
  - HTTPS redirect in production (`app.UseHttpsRedirection()`) ✅
  - Base URL configured in appsettings ✅

## 2. Database Configuration

- [x] **Production Database**
  - SQL Server database provisioned ✅
  - Connection string configured in production ✅
  - Credentials stored securely ✅

- [x] **Database Migration**
  - Automatic migrations on startup (`context.Database.Migrate()`) ✅
  - Schema verified ✅
  - Seed data configured (SeedData.cs) ✅

- [ ] **Backup Strategy**
  - Configure automated database backups
  - Test restore procedure
  - Document backup retention policy

## 3. Stripe Payment Configuration

- [x] **Stripe Account Setup**
  - Stripe account created ✅
  - Business verification complete ✅
  - Bank account configured ✅

- [x] **API Keys**
  - Live API keys obtained (pk_live_, sk_live_) ✅
  - Keys stored in configuration ✅
  - Using Live keys for production ✅

- [x] **Webhook Configuration**
  - Payment webhook endpoint: `/api/payment/webhook` ✅
  - Subscription webhook endpoint: `/Subscription/Webhook` ✅
  - Webhook signing secret configured ✅

- [ ] **Test Payment Flow**
  - Process a real small payment ($1) to verify integration
  - Verify webhook receives confirmation
  - Confirm payment appears in Stripe Dashboard
  - Refund test payment

- [x] **Payment Error Handling (Critical)** ✅ ALL FIXED
  - [x] Added logging to webhook exception handlers
  - [x] Webhook endpoints return proper HTTP codes (400/500/200)
  - [x] Email notification for failed subscription payments
  - [x] Refund event handling (`charge.refunded`)

- [x] **Subscription Webhook Events** ✅ ALL IMPLEMENTED
  - [x] `customer.subscription.created`
  - [x] `customer.subscription.updated`
  - [x] `customer.subscription.deleted`
  - [x] `invoice.payment_succeeded`
  - [x] `invoice.payment_failed`
  - [x] Separate webhook endpoint for subscriptions

## 4. Email/SMTP Configuration

- [x] **Email Service Provider**
  - SMTP relay configured (Site4Now mail server) ✅
  - SSL/TLS enabled (port 587) ✅

- [x] **SMTP Configuration**
  - Host: mail5010.site4now.net ✅
  - From: noreply@lakecountryspanish.com ✅
  - From Name: Lake Country Spanish ✅

- [ ] **Email Deliverability**
  - Configure DNS records (SPF, DKIM, DMARC) for better deliverability
  - Test email delivery to multiple providers (Gmail, Outlook, Yahoo)
  - Check spam score using mail-tester.com

- [x] **Email Templates** ✅ IMPLEMENTED
  - [x] Class reminder (24 hours before)
  - [x] Class cancellation notification
  - [x] Class rescheduled notification
  - [x] Payment confirmation
  - [x] Payment failed notification
  - [x] Subscription renewal notification
  - [x] Assignment assigned notification
  - [x] Weekly progress report
  - [x] Badge earned notification
  - [x] Point milestone notification
  - [ ] Welcome email (new student registration) - Not implemented
  - [ ] Class booking confirmation - Not implemented

## 5. Application Configuration

- [x] **appsettings.Production.json** ✅
  - BaseUrl configured ✅
  - DefaultClassPrice: 25.00 ✅
  - Logging levels configured ✅

- [x] **Configuration Structure** ✅
  - Stripe keys in config ✅
  - Email settings in config ✅
  - Database connection configured ✅

- [ ] **Secret Management**
  - Consider Azure Key Vault or similar for enhanced security
  - Rotate secrets periodically

## 6. Security Checklist

- [x] **Authentication** ✅ COMPLETE
  - [x] Password minimum 8 characters
  - [x] Requires uppercase letter
  - [x] Requires lowercase letter
  - [x] Requires digit
  - [x] Unique email required
  - [x] Account lockout configured (5 attempts, 5 min lockout)
  - [x] MustChangePassword flow implemented

- [x] **HTTPS** ✅ COMPLETE
  - [x] HTTPS redirect enabled (`app.UseHttpsRedirection()`)
  - [x] HSTS headers enabled (`app.UseHsts()`)
  - [ ] Verify no mixed content warnings (requires browser testing)

- [ ] **Data Protection**
  - Configure Data Protection keys for production
  - Store keys in persistent location (not in-memory)

- [x] **Anti-Forgery** ✅ COMPLETE
  - [x] `[ValidateAntiForgeryToken]` on all POST actions (69 instances)
  - [x] AJAX calls include tokens where needed

- [x] **File Uploads** ✅ CONFIGURED
  - Upload handling in DocumentService
  - File type validation implemented
  - Max file size limits configured

## 7. Admin Account Setup

- [x] **Create Production Admin** ✅
  - Admin account seeded via SeedData.cs
  - Default credentials configured (should be changed on first login)

- [ ] **Remove Test Data**
  - Remove or update test student accounts
  - Clear any test payments/classes
  - Update sample data if seeded

## 8. Monitoring & Logging

- [x] **Logging** ✅ IMPLEMENTED
  - [x] ILogger<T> injected in all key services
  - [x] Structured logging with named properties
  - [x] Error logging in catch blocks
  - [x] Information logging for key operations
  - [ ] Consider Serilog for file-based rotation (optional)

- [ ] **Application Monitoring**
  - Set up Application Insights (Azure) or similar
  - Configure error alerting
  - Set up uptime monitoring (UptimeRobot, Pingdom)

- [ ] **Health Checks**
  - Add health check endpoint
  - Monitor database connectivity
  - Monitor external service connectivity (Stripe, Email)

## 9. Performance & Optimization

- [x] **Database** ✅ OPTIMIZED
  - [x] Indexes on frequently queried columns
  - [x] Unique constraints for data integrity
  - [x] Connection pooling (default EF Core behavior)

- [ ] **Caching**
  - Enable response caching where appropriate
  - Configure static file caching headers

- [ ] **Static Files**
  - Minify CSS/JS if not using CDN
  - Enable compression (gzip/brotli)
  - Consider CDN for static assets

## 10. Legal & Compliance

- [x] **Privacy Policy** ✅ COMPLETE
  - [x] Full privacy policy content added to Privacy.cshtml
  - [x] Covers: data collection, cookies, data sharing, security, user rights
  - [x] Cookie policy section included with table of cookies used

- [ ] **Terms of Service**
  - Create terms of service page
  - Include cancellation/refund policy
  - Include payment terms

- [x] **Cookie Consent** ✅ NOT REQUIRED
  - [x] Only essential authentication cookie used (no tracking)
  - [x] Essential cookies are exempt from consent requirements under GDPR
  - [x] Cookie usage documented in Privacy Policy

## 11. Testing Before Launch

- [ ] **Functional Testing**
  - [ ] User registration and login
  - [ ] Password change (forced and voluntary)
  - [ ] Student dashboard loads correctly
  - [ ] Class booking flow works
  - [ ] Package purchase with real Stripe payment
  - [ ] Class cancellation (with and without forfeit)
  - [ ] Feedback submission
  - [ ] Tip payment flow
  - [ ] Admin dashboard functions
  - [ ] Admin can manage students
  - [ ] Admin can manage schedule
  - [ ] Admin can approve testimonials
  - [ ] Contact form submission
  - [ ] Document upload/download
  - [ ] Email notifications sent

- [ ] **Cross-Browser Testing**
  - [ ] Chrome
  - [ ] Firefox
  - [ ] Safari
  - [ ] Edge
  - [ ] Mobile browsers

- [ ] **Mobile Responsiveness**
  - [ ] Test on actual mobile devices
  - [ ] Verify all modals work on mobile
  - [ ] Test calendar/booking on touch devices

## 12. Deployment Steps

1. [x] Create production database and run migrations ✅
2. [x] Configure all environment variables/secrets ✅
3. [x] Deploy application to hosting ✅
4. [x] Configure SSL certificate ✅
5. [x] Verify DNS propagation ✅
6. [ ] Test all critical flows
7. [ ] Set up monitoring and alerts
8. [x] Create production admin account ✅
9. [ ] Configure automated backups
10. [ ] Document deployment process

## 13. Post-Launch

- [ ] **Monitor First 24-48 Hours**
  - Watch error logs closely
  - Monitor payment processing
  - Check email deliverability

- [ ] **Backup Verification**
  - Verify first backup completed successfully
  - Test restore procedure

- [ ] **Documentation**
  - Document admin procedures
  - Create FAQ for students
  - Document common troubleshooting steps

---

## Quick Reference: Required Secrets

| Secret | Description | Status |
|--------|-------------|--------|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string | ✅ Configured |
| `Stripe:SecretKey` | Stripe API secret key | ✅ Configured |
| `Stripe:PublishableKey` | Stripe API publishable key | ✅ Configured |
| `Stripe:WebhookSecret` | Webhook signing secret | ✅ Configured |
| `EmailSettings:SmtpHost` | SMTP server | ✅ Configured |
| `AppSettings:BaseUrl` | Production URL | ✅ Configured |

---

## 14. Payment Code Fixes (Technical) ✅ ALL COMPLETE

All critical payment issues identified during review have been fixed:

- [x] **StripePaymentService.cs - Silent Failures** ✅ FIXED
  - Added ILogger to StripePaymentService
  - ProcessWebhookAsync now returns `WebhookProcessingResult` with detailed status
  - All errors are logged with appropriate severity levels
  - Added comprehensive logging for all webhook processing steps

- [x] **PaymentController.cs - Always Returns OK** ✅ FIXED
  - Webhook endpoint now returns proper HTTP codes based on processing result
  - Returns 400 for invalid signatures (Stripe won't retry)
  - Returns 500 for processing failures (Stripe will retry)
  - Returns 200 only for success or duplicate processing

- [x] **StripeSubscriptionService.cs - Missing Email Notification** ✅ FIXED
  - Implemented `SendPaymentFailedAsync` email notification
  - HandleInvoicePaymentFailed now sends email to student when subscription payment fails
  - Email includes subscription tier, amount, and instructions to update payment method

- [x] **Missing Refund Handling** ✅ FIXED
  - Added `charge.refunded` event handler in StripePaymentService
  - Payment status is updated to Refunded when refund occurs in Stripe Dashboard

- [x] **Idempotency Checks** ✅ ADDED
  - ProcessWebhookAsync checks if payment is already completed before processing
  - Checks if StripePaymentIntentId was already processed for ANY payment
  - StudentPackage creation checks for existing package before creating
  - GrantSubscriptionTicketsAsync checks for existing tickets before granting

- [x] **Database Constraints** ✅ ADDED
  - Added unique filtered index on Payment.StripePaymentIntentId
  - Added unique filtered index on Subscription.StripeSubscriptionId
  - Prevents duplicate payment/subscription processing at database level

- [x] **Subscription Ticket Race Condition** ✅ FIXED
  - GrantSubscriptionTicketsAsync now uses transactions
  - Double-checks within transaction to handle concurrent webhook calls
  - Returns existing tickets if already granted for the period

- [x] **ProcessTipWebhookAsync Error Handling** ✅ FIXED
  - Added logging for all error paths
  - Added idempotency check for already-paid tips

- [x] **Hardcoded Fallback Price Warning** ✅ FIXED
  - Logs warning when using $25 fallback price
  - Alerts when AppSettings:DefaultClassPrice is not configured

---

## Summary: Remaining Action Items

### Critical (Before Launch)
1. [ ] Test real payment flow with small transaction
2. [ ] Complete functional testing checklist
3. [ ] Configure database backups

### Important (Should Do)
4. [x] ~~Add actual content to Privacy Policy page~~ ✅ DONE
5. [ ] Create Terms of Service page
6. [ ] Test email deliverability (spam score)
7. [ ] Set up monitoring/alerting

### Nice to Have
8. [ ] Add welcome email template
9. [ ] Add class booking confirmation email
10. [x] ~~Add cookie consent banner~~ ✅ NOT NEEDED (essential cookies only, exempt from consent)
11. [ ] Add health check endpoints
12. [ ] Configure Serilog for file-based logging

---

## Future Feature Requests

These are features to consider for future development:

1. [ ] **Group Classes** - Allow multiple students to be enrolled in the same class session
   - Would require changes to ScheduledClass entity (many-to-many with students)
   - Admin UI for creating group class slots
   - Student UI for joining group classes
   - Different pricing model for group vs private lessons

2. [ ] **Cart Reservation Expiration** - Auto-release cart reservations after timeout
   - Currently slots are held indefinitely when added to cart
   - Consider 15-30 minute timeout with warning
   - Background job to clean up stale reservations

---

## Notes

- Always test payment flows with real (small) transactions before announcing launch
- Keep test/development Stripe keys separate from production
- Have a rollback plan ready for the first deployment
- Consider soft launch to a few users before public announcement
