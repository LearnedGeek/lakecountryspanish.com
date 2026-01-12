# Lake Country Spanish - Pre-Release Checklist

## 1. Domain & Hosting

- [ ] **Domain Registration**
  - Register domain name (e.g., lakecountryspanish.com)
  - Configure DNS records to point to hosting provider

- [ ] **Hosting Setup**
  - Choose hosting provider (Azure App Service, AWS, DigitalOcean, etc.)
  - Provision server/app service with appropriate tier
  - Configure deployment pipeline (GitHub Actions, Azure DevOps, etc.)

- [ ] **SSL Certificate**
  - Obtain SSL certificate (Let's Encrypt free, or paid certificate)
  - Configure HTTPS redirect in production
  - Update `appsettings.Production.json` with correct base URL

## 2. Database Configuration

- [ ] **Production Database**
  - Provision SQL Server database (Azure SQL, AWS RDS, or self-hosted)
  - Create database user with appropriate permissions
  - Update connection string in production secrets/environment variables
  - **DO NOT** store connection strings in appsettings.json for production

- [ ] **Database Migration**
  - Run all migrations on production database
  - Verify schema is correct
  - Seed initial data (admin user, default packages)

- [ ] **Backup Strategy**
  - Configure automated database backups
  - Test restore procedure
  - Document backup retention policy

## 3. Stripe Payment Configuration

- [ ] **Stripe Account Setup**
  - Create Stripe account at https://stripe.com
  - Complete business verification
  - Set up bank account for payouts

- [ ] **API Keys**
  - Get **Live** API keys from Stripe Dashboard > Developers > API Keys
  - Store `Publishable Key` and `Secret Key` securely
  - **IMPORTANT**: Use Live keys, not Test keys for production

- [ ] **Webhook Configuration**
  - Create webhook endpoint in Stripe Dashboard
  - URL: `https://yourdomain.com/api/payment/webhook`
  - Select events: `checkout.session.completed`
  - Copy Webhook Signing Secret

- [ ] **Update Configuration**
  ```json
  // Store these in environment variables or secrets manager, NOT in code
  {
    "Stripe": {
      "PublishableKey": "pk_live_...",
      "SecretKey": "sk_live_...",
      "WebhookSecret": "whsec_..."
    }
  }
  ```

- [ ] **Test Payment Flow**
  - Process a real small payment ($1) to verify integration
  - Verify webhook receives confirmation
  - Confirm payment appears in Stripe Dashboard
  - Refund test payment

## 4. Email/SMTP Configuration

- [ ] **Email Service Provider**
  Choose one:
  - [ ] SendGrid (recommended for transactional email)
  - [ ] Mailgun
  - [ ] Amazon SES
  - [ ] SMTP relay (Gmail, Outlook 365)

- [ ] **SendGrid Setup** (if using SendGrid)
  - Create account at https://sendgrid.com
  - Verify sender identity/domain
  - Create API key with Mail Send permissions
  - Configure DNS records (SPF, DKIM, DMARC) for deliverability

- [ ] **Update Configuration**
  ```json
  {
    "Email": {
      "Provider": "SendGrid",
      "ApiKey": "SG.xxxxx",
      "FromEmail": "karen@lakecountryspanish.com",
      "FromName": "Lake Country Spanish"
    }
  }
  ```

- [ ] **Email Templates**
  - Review all email templates for correct branding
  - Test email delivery to multiple providers (Gmail, Outlook, Yahoo)
  - Check spam score using mail-tester.com

- [ ] **Emails to Configure**
  - [ ] Welcome email (new student registration)
  - [ ] Password reset
  - [ ] Class booking confirmation
  - [ ] Class reminder (24 hours before)
  - [ ] Class cancellation notification
  - [ ] Class rescheduled notification
  - [ ] Payment confirmation

## 5. Application Configuration

- [ ] **appsettings.Production.json**
  ```json
  {
    "AppSettings": {
      "BaseUrl": "https://lakecountryspanish.com",
      "DefaultClassPrice": 25.00
    },
    "Logging": {
      "LogLevel": {
        "Default": "Warning",
        "Microsoft.AspNetCore": "Warning"
      }
    }
  }
  ```

- [ ] **Environment Variables** (recommended over config files)
  ```
  ASPNETCORE_ENVIRONMENT=Production
  ConnectionStrings__DefaultConnection=Server=...
  Stripe__SecretKey=sk_live_...
  Stripe__WebhookSecret=whsec_...
  Email__ApiKey=SG.xxx...
  ```

- [ ] **Secret Management**
  - Use Azure Key Vault, AWS Secrets Manager, or similar
  - Never commit secrets to source control
  - Rotate secrets periodically

## 6. Security Checklist

- [ ] **Authentication**
  - Verify password requirements are strong (min 8 chars, complexity)
  - Enable account lockout after failed attempts
  - Review MustChangePassword flow works correctly

- [ ] **HTTPS**
  - Force HTTPS redirect
  - Set HSTS headers
  - Verify no mixed content warnings

- [ ] **Data Protection**
  - Configure Data Protection keys for production
  - Store keys in persistent location (not in-memory)

- [ ] **Anti-Forgery**
  - Verify all forms include anti-forgery tokens
  - Verify AJAX calls include tokens where needed

- [ ] **File Uploads**
  - Verify upload directory exists and has correct permissions
  - Configure max file size limits
  - Validate file types on upload

## 7. Admin Account Setup

- [ ] **Create Production Admin**
  - Update SeedData or manually create admin account
  - Use strong, unique password
  - Store credentials securely (password manager)

- [ ] **Remove Test Data**
  - Remove or update test student accounts
  - Clear any test payments/classes
  - Update sample data if seeded

## 8. Monitoring & Logging

- [ ] **Application Monitoring**
  - Set up Application Insights (Azure) or similar
  - Configure error alerting
  - Set up uptime monitoring (UptimeRobot, Pingdom)

- [ ] **Logging**
  - Configure structured logging (Serilog recommended)
  - Set appropriate log levels for production
  - Configure log retention/rotation

- [ ] **Health Checks**
  - Add health check endpoint
  - Monitor database connectivity
  - Monitor external service connectivity (Stripe, Email)

## 9. Performance & Optimization

- [ ] **Caching**
  - Enable response caching where appropriate
  - Configure static file caching headers

- [ ] **Database**
  - Add appropriate indexes
  - Review query performance
  - Enable connection pooling

- [ ] **Static Files**
  - Minify CSS/JS if not using CDN
  - Enable compression (gzip/brotli)
  - Consider CDN for static assets

## 10. Legal & Compliance

- [ ] **Privacy Policy**
  - Create privacy policy page
  - Detail data collection and usage
  - Include cookie policy if applicable

- [ ] **Terms of Service**
  - Create terms of service page
  - Include cancellation/refund policy
  - Include payment terms

- [ ] **Cookie Consent**
  - Add cookie consent banner if using tracking cookies
  - Document cookies used

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

1. [ ] Create production database and run migrations
2. [ ] Configure all environment variables/secrets
3. [ ] Deploy application to hosting
4. [ ] Configure SSL certificate
5. [ ] Verify DNS propagation
6. [ ] Test all critical flows
7. [ ] Set up monitoring and alerts
8. [ ] Create production admin account
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

| Secret | Description | Where to Get |
|--------|-------------|--------------|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string | Your database provider |
| `Stripe:SecretKey` | Stripe API secret key | Stripe Dashboard > Developers > API Keys |
| `Stripe:PublishableKey` | Stripe API publishable key | Stripe Dashboard > Developers > API Keys |
| `Stripe:WebhookSecret` | Webhook signing secret | Stripe Dashboard > Developers > Webhooks |
| `Email:ApiKey` | SendGrid/email provider API key | SendGrid Dashboard > Settings > API Keys |
| `AppSettings:BaseUrl` | Production URL | Your domain (https://lakecountryspanish.com) |

---

## Notes

- Always test payment flows with real (small) transactions before announcing launch
- Keep test/development Stripe keys separate from production
- Have a rollback plan ready for the first deployment
- Consider soft launch to a few users before public announcement
