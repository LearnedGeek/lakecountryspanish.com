# Stripe Payment Testing Guide

This guide explains how to test Stripe payment integration locally using the Stripe CLI.

## Prerequisites

### 1. Install Stripe CLI

**Windows (PowerShell):**
```powershell
# Using Scoop
scoop install stripe

# Or download from GitHub releases:
# https://github.com/stripe/stripe-cli/releases
# Extract and add to PATH
```

**Mac:**
```bash
brew install stripe/stripe-cli/stripe
```

**Linux:**
```bash
# Download from https://github.com/stripe/stripe-cli/releases
# Or use the install script
```

### 2. Login to Stripe
```bash
stripe login
```
This opens a browser window to authenticate with your Stripe account.

## Running Integration Tests

### Unit Tests (No Stripe Required)
These tests mock Stripe and test business logic:
```bash
cd src/LakeCountrySpanish.Tests
dotnet test --filter "FullyQualifiedName~StripePaymentServiceTests|FullyQualifiedName~StripeWebhookTests"
```

### E2E Integration Tests (Requires Stripe Test Keys)
Set your test secret key and run:
```bash
# Set environment variable
$env:STRIPE_TEST_SECRET_KEY = "sk_test_YOUR_KEY_HERE"

# Run integration tests
dotnet test --filter "Category=StripeIntegration"
```

## Testing Webhooks Locally

### 1. Start Webhook Forwarding
```bash
stripe listen --forward-to https://localhost:5001/api/payment/webhook
```

This outputs a webhook signing secret like:
```
> Ready! Your webhook signing secret is whsec_xxxxxxxxxxxxx
```

### 2. Update Development Config
Add this secret to `appsettings.Development.json`:
```json
{
  "Stripe": {
    "WebhookSecret": "whsec_xxxxxxxxxxxxx"
  }
}
```

### 3. Start Your Application
```bash
cd src/LakeCountrySpanish.Web
dotnet run
```

### 4. Trigger Test Events
In another terminal:
```bash
# Test checkout completion
stripe trigger checkout.session.completed

# Test subscription events
stripe trigger customer.subscription.created
stripe trigger customer.subscription.updated
stripe trigger invoice.paid
stripe trigger invoice.payment_failed
```

## Test Card Numbers

Use these card numbers in Stripe's test mode:

| Card Number | Description |
|-------------|-------------|
| 4242 4242 4242 4242 | Success |
| 4000 0000 0000 0002 | Card declined |
| 4000 0000 0000 9995 | Insufficient funds |
| 4000 0000 0000 3220 | 3D Secure required |

- **Expiry**: Any future date (e.g., 12/34)
- **CVC**: Any 3 digits (e.g., 123)
- **ZIP**: Any 5 digits (e.g., 12345)

## Full E2E Test Flow

1. **Terminal 1 - Start webhook listener:**
   ```bash
   stripe listen --forward-to https://localhost:5001/api/payment/webhook
   ```

2. **Terminal 2 - Start application:**
   ```bash
   cd src/LakeCountrySpanish.Web
   dotnet run --environment Development
   ```

3. **Browser - Complete a test purchase:**
   - Navigate to https://localhost:5001
   - Login as a test student
   - Purchase a package using test card `4242 4242 4242 4242`
   - Verify the webhook is received in Terminal 1
   - Check the database for updated payment status

## Monitoring Webhook Deliveries

### In Stripe CLI
The `stripe listen` command shows real-time webhook deliveries:
```
2024-01-14 12:00:00   --> checkout.session.completed [evt_xxx]
2024-01-14 12:00:00  <--  [200] POST https://localhost:5001/api/payment/webhook
```

### In Stripe Dashboard
1. Go to Developers → Webhooks
2. Click on your endpoint
3. View "Event deliveries" tab
4. Check response codes and bodies for debugging

## Troubleshooting

### Webhook Returns 400
- Check that `Stripe-Signature` header is present
- Verify webhook secret matches in config

### Webhook Returns 500
- Check application logs for exceptions
- Verify database connectivity
- Check if payment record exists

### Session Not Found
- Ensure payment was created before checkout session
- Check `StripeSessionId` was saved to database

### Signature Verification Failed
- Ensure using correct webhook secret (CLI vs Dashboard)
- Check clock skew between systems

## Environment Configuration

### Development (`appsettings.Development.json`)
```json
{
  "Stripe": {
    "PublishableKey": "pk_test_xxx",
    "SecretKey": "sk_test_xxx",
    "WebhookSecret": "whsec_xxx"
  }
}
```

### Production (`appsettings.Production.json`)
```json
{
  "Stripe": {
    "PublishableKey": "pk_live_xxx",
    "SecretKey": "sk_live_xxx",
    "WebhookSecret": "whsec_xxx"
  }
}
```

## CI/CD Testing

For automated testing in CI/CD pipelines, use Stripe's test fixtures:
```bash
# In GitHub Actions or similar
- name: Run Stripe Integration Tests
  env:
    STRIPE_TEST_SECRET_KEY: ${{ secrets.STRIPE_TEST_SECRET_KEY }}
  run: dotnet test --filter "Category=StripeIntegration"
```
