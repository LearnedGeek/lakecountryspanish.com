# Production Hardening Playbook

> **Why this exists:** During a client demo, an Android app failed to sync data with the web backend. Debugging with AI produced confident-sounding guesses — "the route is wrong," "it's an auth issue" — but none were grounded in actual diagnostic evidence. The root cause remained unfound because there was no contract enforcement, no audit trail, and no single source of truth between client and server. This playbook documents the systematic hardening we applied to API Combat to prevent exactly that class of failure.

---

## Table of Contents

1. [The Core Problem](#1-the-core-problem)
2. [API Contract as Single Source of Truth](#2-api-contract-as-single-source-of-truth)
3. [Authentication & Authorization](#3-authentication--authorization)
4. [Input Validation & Data Integrity](#4-input-validation--data-integrity)
5. [Race Conditions & Concurrency](#5-race-conditions--concurrency)
6. [Error Handling & Diagnostics](#6-error-handling--diagnostics)
7. [Security Headers & Transport](#7-security-headers--transport)
8. [Rate Limiting](#8-rate-limiting)
9. [Monitoring, Alerts & Audit Trail](#9-monitoring-alerts--audit-trail)
10. [Automated Testing Strategy](#10-automated-testing-strategy)
11. [Load Testing](#11-load-testing)
12. [Deployment Reliability](#12-deployment-reliability)
13. [Backup & Recovery](#13-backup--recovery)
14. [Request/Response Tracing](#14-requestresponse-tracing)
15. [Contract Testing for Multi-Team Projects](#15-contract-testing-for-multi-team-projects)
16. [Environment Parity](#16-environment-parity)
17. [Client-Side Hardening Guidance](#17-client-side-hardening-guidance)
18. [Emergency Triage — "The Demo Is Broken Right Now"](#18-emergency-triage--the-demo-is-broken-right-now)
19. [Quick-Start Checklist for New Projects](#19-quick-start-checklist-for-new-projects)

---

## 1. The Core Problem

When a client app and a server disagree, debugging devolves into guessing. Without evidence, even AI-assisted debugging becomes a coin flip. The fix isn't better guessing — it's eliminating ambiguity before it starts:

| Failure Mode | Root Cause | Prevention |
|---|---|---|
| Client sends wrong route | No contract enforcement | OpenAPI spec as single source of truth |
| Auth token rejected silently | Unclear auth scheme requirements | Explicit dual-scheme config + docs |
| Data doesn't sync | Silent server errors | Structured error responses + logging |
| Works locally, fails in prod | Environment drift | Health checks + launch checklist |
| Race condition corrupts state | No transaction boundaries | DB-level guards + concurrency tests |
| Can't reproduce the bug | No audit trail | Immutable activity ledger |

**Principle:** Every request should either succeed with the expected response, or fail with a clear, logged, diagnosable error. No silent failures. No ambiguous states.

---

## 2. API Contract as Single Source of Truth

**The problem it solves:** Client and server teams disagree on route paths, parameter names, auth requirements, or response shapes. During a demo, "the API changed" meets "no it didn't" and nobody can prove anything.

### What We Did

- **OpenAPI spec auto-generated from code** at `/openapi/v1.json` — the spec is always current because it's derived from the running application, not a separate document that drifts.
- **Custom API docs renderer** at `/api-docs/v1` with Material Design 3 styling — developers can browse endpoints, see request/response schemas, and try things out.
- **Enum-driven validation** — strategy fields use C# enums that auto-generate into the OpenAPI spec. If the enum changes, the docs and validation update simultaneously.
- **HATEOAS-lite links** — API responses include `_links` with `href`, `method`, and `rel` so clients discover valid operations from responses rather than hardcoding URLs.
- **Robot player E2E test** — a spec-driven test that walks all HATEOAS links without using any DTOs, proving the API contract is internally consistent.

### Recommendations for Any Project

1. **Generate your spec from code, not the other way around.** Hand-maintained specs drift within days.
2. **Serve the spec at a well-known URL.** Clients should fetch it, not receive it via email.
3. **Write at least one E2E test that exercises the full API surface** by following links from the root endpoint. If a route changes, the test breaks.
4. **Include auth requirements in the spec.** Every endpoint should declare which auth schemes it accepts.
5. **Version your API** (`/api/v1/...`) so breaking changes don't silently break clients.

---

## 3. Authentication & Authorization

**The problem it solves:** "It's an auth issue" is the second guess for every API failure. With multiple auth schemes (JWT, cookies, API keys), it's easy for requests to silently fail because they're using the wrong scheme for the wrong endpoint.

### What We Did

**Three authentication schemes with explicit priority:**

| Priority | Scheme | Use Case | Header/Mechanism |
|---|---|---|---|
| 1 | API Key | External integrations | `X-Api-Key: acg_...` |
| 2 | JWT Bearer | API clients | `Authorization: Bearer <token>` |
| 3 | Cookies | Web UI (browser) | Automatic via `HttpOnly` cookie |

**Key implementation details:**

- **Policy scheme (`JWT_OR_COOKIE`)** routes requests to the correct handler based on the request path — `/api/*` defaults to JWT, everything else to cookies.
- **Explicit auth scheme override** on endpoints where browser JS calls API routes: `[Authorize(AuthenticationSchemes = "Cookies,Bearer")]`. This is a common gotcha — without it, the browser's cookie is ignored on API endpoints.
- **JWT hardening:** Zero clock skew (`ClockSkew = TimeSpan.Zero`), 60-minute expiry, HMAC-SHA256, validate issuer + audience + lifetime + signing key.
- **Cookie hardening:** `HttpOnly = true` (XSS protection), `SameSite = Lax` (CSRF protection), `Secure` in production.
- **API key format:** `acg_{32-byte-base64}` — BCrypt hashed in DB, only prefix stored for logging. Max 5 keys per player. New-IP alerts.
- **Deleted account check** on token refresh — prevents zombied sessions.
- **Email-based login** — harder to enumerate than usernames.
- **Generic error messages** on login failure — doesn't reveal whether account exists.

### The Auth Gotcha Checklist

For any project with multiple auth schemes, test these scenarios explicitly:

- [ ] Browser JS calling API endpoint with cookie — does it authenticate?
- [ ] API client with JWT calling web endpoint — does it authenticate?
- [ ] Expired token — does it return 401 (not 500 or redirect)?
- [ ] Deleted/deactivated account — can it still use existing tokens?
- [ ] Token refresh — does it re-validate account state?
- [ ] New client IP — is there any alerting?

---

## 4. Input Validation & Data Integrity

**The problem it solves:** Garbage in, garbage out. Unvalidated input leads to corrupt state, crashes, or security vulnerabilities that surface as "the API doesn't work" during demos.

### What We Did

**Boundary enforcement on every user-facing input:**

| Entity | Field | Constraint |
|---|---|---|
| Player | Username | 3-50 chars, `[Required]` |
| Player | Email | `[EmailAddress]`, max 100 chars |
| Player | Password | 8-100 chars |
| Guild | Name | 1-50 chars |
| Guild | Tag | 1-5 chars |
| Guild | Description | max 500 chars |
| Chat message | Content | max 500 chars, trimmed, whitespace-only rejected |
| Strategy | Name | 1-100 chars |
| Strategy | Description | max 1000 chars |
| Strategy | JSON | max 50KB |
| Leaderboard | Limit | capped at 100 |
| Activity feed | Page | coerced to >= 1 |
| Guild war | Limit | clamped 1-50 |
| Notifications | Page/PageSize | page >= 1, pageSize 1-100 |
| Discord link | User ID | validated as ulong format |

**Model state validation factory** — returns structured JSON errors with specific guidance ("Check the field type and format") instead of framework defaults.

**MaxLength attributes on domain models** — defense in depth at the DB level, not just the API layer.

### Recommendations

1. **Validate at the API boundary, enforce at the DB level.** Don't trust that your own code won't pass bad data.
2. **Coerce where safe, reject where not.** A page number of -1 can safely become 1. A username of 500 chars should be rejected.
3. **Return structured validation errors.** `{ "errors": [{ "field": "name", "message": "..." }] }` — not a 500 with a stack trace.

---

## 5. Race Conditions & Concurrency

**The problem it solves:** Two requests hit at the same time. Without transaction boundaries, currency gets double-spent, tournaments over-fill, or guild membership exceeds capacity.

### What We Did

**DB transactions wrapping multi-step operations:**

| Operation | Guard |
|---|---|
| Strategy marketplace buy/sell | Transaction on currency deduction + credit |
| Tournament entry | Transaction on fee deduction + DB-level count check for capacity |
| Guild treasury deposit | Transaction on player currency decrease + treasury increase |
| Guild treasury purchase | Transaction on treasury decrease + upgrade application |
| Guild invite/accept | DB-level member count check (not in-memory) |
| Loot claim all | Single transaction for all gold/XP awards (atomic) |

**Key principle:** Check counts and balances **inside the transaction at the DB level**, not by reading in-memory state then writing. The read-check-write pattern is inherently racy unless it's within a serializable transaction or uses DB constraints.

### Recommendations

1. **Wrap currency/inventory operations in transactions.** If step 2 fails, step 1 must roll back.
2. **Use DB-level constraints** (unique indexes, check constraints) as the ultimate guard. Application-level checks are necessary but not sufficient.
3. **Test concurrent access** — send N simultaneous requests for the same resource and verify only the correct number succeed.

---

## 6. Error Handling & Diagnostics

**The problem it solves:** The client gets a 500 error with no body, or an HTML error page when it expected JSON. The server log says nothing useful. Nobody can figure out what happened.

### What We Did

**Global exception middleware (`GlobalExceptionMiddleware`):**
- Catches all unhandled exceptions
- Generates a **support ID** (format: `ERR-XXXX-XXXX`) — user can report this, support can look it up
- Logs full context: method, path, exception, stack trace, player ID, IP, user agent
- Persists to `AppLog` table via `IAppLogService`
- Returns **JSON for API requests**, re-executes status code pages for web requests
- **Never exposes stack traces** to the client

**Structured error responses for known status codes:**

| Code | Response |
|---|---|
| 401 | `{ "error": "Unauthorized. Provide a valid Bearer token..." }` |
| 403 | `{ "error": "Forbidden. You don't have permission..." }` |
| 404 | `{ "error": "The requested endpoint was not found." }` |
| 405 | `{ "error": "HTTP method not allowed..." }` |
| 429 | `{ "error": "Too many requests. Please slow down." }` |

**Silent failure elimination:**
- Deserialization failures in NotificationService: now **logged**, not swallowed
- Strategy deserialization errors in TeamController: now **logged**
- Player-not-found in LootService.RollLoot: now **logged** with warning
- Challenge generation for missing player: now **logged** and returns gracefully
- Webhook failures: return **500** (not 200) so Stripe retries

### Recommendations

1. **Every error must produce a log entry.** If something fails and there's no log, it didn't happen as far as debugging is concerned.
2. **Support IDs in error responses.** The user says "I got ERR-A1B2-C3D4" and you can find the full stack trace in seconds.
3. **Never return HTML to an API client.** Check `Accept` headers or request path to determine response format.
4. **Never swallow exceptions.** Catch them, log them, return a clean error. `catch { }` is a debugging black hole.
5. **Webhook handlers must return appropriate status codes.** Return 200 only on success. 500 tells the sender to retry.

---

## 7. Security Headers & Transport

**The problem it solves:** XSS, clickjacking, MIME sniffing, and other browser-level attacks.

### What We Did

Applied on **every response** via middleware in `Program.cs`:

```
X-Content-Type-Options: nosniff          — Prevents MIME type sniffing
X-Frame-Options: DENY                    — Prevents clickjacking via iframe
Referrer-Policy: strict-origin-when-cross-origin  — Controls referrer leaking
Permissions-Policy: camera=(), microphone=(), geolocation=()  — Disables unnecessary browser APIs
```

**Transport security:**
- `UseHttpsRedirection()` in middleware pipeline
- Cookies: `Secure = true` in production
- CORS: production allows only `https://apicombat.com` + `https://www.apicombat.com`

### Recommendations

1. **Add security headers to every project from day one.** It's 10 lines of middleware that prevent entire classes of attacks.
2. **Lock CORS to your actual domains in production.** `AllowAnyOrigin` in prod is an open invitation.
3. **Enforce HTTPS.** No exceptions. Use HSTS if you control the domain fully.

---

## 8. Rate Limiting

**The problem it solves:** Abuse, DDoS, credential stuffing, and runaway client bugs that hammer the server.

### What We Did

**Tier-based rate limiting middleware (`RateLimitingMiddleware`):**

| Tier | Limit | Use Case |
|---|---|---|
| Free | 60 req/min | Default |
| Premium | 120 req/min | Paid tier |
| Premium Plus | 300 req/min | Highest tier |

**Response headers on every request:**
- `X-RateLimit-Limit` — max requests allowed
- `X-RateLimit-Remaining` — requests left in window
- `X-RateLimit-Reset` — Unix timestamp when window resets

**429 response** with `Retry-After` header when limit exceeded.

**IP-based tracking** with tier bucketing. Stale entry cleanup every ~100 requests.

### Recommendations

1. **Rate limit from day one.** It's trivial to add and prevents entire categories of abuse.
2. **Include rate limit headers in responses.** Clients can self-throttle instead of hitting the wall.
3. **Make it togglable for testing** — our middleware has a static `Enabled` flag that tests set to `false`.

---

## 9. Monitoring, Alerts & Audit Trail

**The problem it solves:** Something went wrong in production and nobody noticed until a user complained. Or: something went wrong and there's no record of what happened.

### What We Did

**Three layers of observability:**

**Layer 1 — Immutable Activity Ledger:**
- 46 audit points across 17 services
- Every state change (currency transfer, guild action, rating change, etc.) recorded
- Immutable — entries are append-only, never modified or deleted
- Enables after-the-fact reconstruction of "what happened"

**Layer 2 — Admin Audit Log:**
- All admin actions tracked: currency adjustments, tier changes, admin toggles, password resets
- Who did what, to whom, when, and why (JSON details)
- Viewable at `/Admin/AuditLog` with filtering and pagination

**Layer 3 — Automated Alerts (`AdminAlertJob`, runs hourly):**

| Alert | Severity | Trigger |
|---|---|---|
| Battle queue stalled | Warning | >10 battles queued >30 min |
| Signup spike | Info | >100 signups/day |
| Guild boss expired | Info | Boss expired without defeat |
| Error rate spike | Warning/Critical | >=10/hour (Warning), >=50/hour (Critical) |

**Supporting infrastructure:**
- `AppLog` table for error persistence with support IDs
- `PlayerActivity` table tracking daily request counts per player
- `ApiKeyUsageLog` for API key usage tracking (IP, user agent, endpoint)
- Error monitoring dashboard in admin panel

### Recommendations

1. **Log state changes, not just errors.** When debugging, knowing what *did* happen is as valuable as knowing what went wrong.
2. **Automated alerts with severity levels.** Don't wait for users to tell you the system is down.
3. **Audit admin actions.** If an admin changes a user's data, there must be a record.
4. **Support IDs connect user reports to server logs.** This alone can cut debugging time from hours to minutes.

---

## 10. Automated Testing Strategy

**The problem it solves:** "It works on my machine." Regression. Fear of refactoring. Demo-day surprises.

### What We Did

**606 automated tests across four layers:**

| Layer | Count | What It Catches |
|---|---|---|
| Unit tests | ~500 | Logic errors, edge cases, null handling |
| Integration tests | ~95 | API contract violations, auth failures, DB issues |
| Robot player E2E | 1 (walks all links) | Broken HATEOAS links, spec drift |
| Playwright smoke tests | 11 | Rendering failures, broken pages, JS errors |

**Test infrastructure:**
- `WebApplicationFactory` + InMemory DB + Moq + xUnit
- `IntegrationTestBase` with helpers: `RegisterPlayer()`, `CreateAuthenticatedClient()`
- Rate limiting disabled in tests (static toggle)
- `TransactionIgnoredWarning` suppressed for InMemory DB

**The Robot Player test** is particularly valuable — it reads the OpenAPI spec, registers a player, and walks every HATEOAS link it discovers. No DTOs, no hardcoded routes. If a link changes or breaks, the test fails. This is the closest thing to a client integration test you can write without a real client.

### Recommendations

1. **Integration tests > unit tests for API hardening.** Unit tests verify logic; integration tests verify the contract.
2. **Write a "robot client" test** that exercises your API by following links from the root. This catches the exact class of bug that killed the demo.
3. **Run tests in CI on every push.** A test that doesn't run automatically might as well not exist.
4. **Test auth scenarios explicitly** — expired tokens, wrong scheme, missing headers, deleted accounts.

---

## 11. Load Testing

**The problem it solves:** The app works with 1 user. Does it work with 100? 1,000? The demo has 50 people hitting it simultaneously and the server falls over.

### What We Did

**k6 load test script** for smoke testing, plus NBomber documentation for progressive load testing:

| Test Type | Users | Duration | Purpose |
|---|---|---|---|
| Smoke | 10 | 1 min | Validate API works under minimal load |
| Baseline | 50 | 5 min | Establish normal performance metrics |
| Stress | Ramp to 500 | 10 min | Find the breaking point |
| Soak | 100 | 1 hour | Detect memory leaks, connection exhaustion |
| Spike | 10 → 500 instantly | 5 min | Simulate sudden traffic burst |

**Target benchmarks:**
- p95 latency < 200ms (reads), < 500ms (writes)
- Error rate < 1% under normal load
- Sustained 100+ RPS on basic hardware

### Recommendations

1. **Run the smoke test before every demo.** 10 users, 1 minute. If it fails, you know before the audience does.
2. **Establish a baseline** so you can detect performance regressions.
3. **Test with realistic data volumes.** An empty database is fast. A database with 10,000 users isn't.

---

## 12. Deployment Reliability

**The problem it solves:** Deployment succeeds, but the app doesn't start. Or it starts but can't reach the database. Or the old version is still running because files were locked.

### What We Did

- **Health check endpoint** at `/health` — verifies DB connectivity via EF Core
- **Auto-migration on startup** (`MigrateAsync()`) — schema is always current
- **`EnableMSDeployAppOffline=true`** — shows maintenance page during deployment
- **App pool recycle before publish** — prevents `ERROR_FILE_IN_USE`
- **10 retries at 15-second intervals** for deployment resilience
- **Dedicated app pool** — isolated from other sites on shared hosting
- **Environment-specific config** — `appsettings.Production.json` separate from development
- **Version tracking** for support troubleshooting

### Pre-Deploy Checklist

- [ ] `dotnet build` — 0 errors, 0 warnings
- [ ] All automated tests pass
- [ ] Health check returns 200 on staging/local
- [ ] Database connection string is correct for target environment
- [ ] JWT secret is unique and strong (not committed to source control)
- [ ] CORS origins match production domain
- [ ] reCAPTCHA keys are for production domain
- [ ] SMTP credentials are configured and tested

---

## 13. Backup & Recovery

**The problem it solves:** Data loss. Corrupted migration. "Can we roll back?"

### What We Did

- **Automated database backup** via `DatabaseBackupJob` (runs at configurable UTC hour, default 3 AM)
- **Full-table JSON export** compressed into ZIP archives
- **Azure Blob Storage** for off-site backup
- **30-day retention** with automated cleanup of old backups
- **Progress tracking** — rows exported, duration, file size logged

### Recommendations

1. **Automate backups from day one.** Manual backups don't happen.
2. **Store backups off-site.** Same-server backups don't survive server failure.
3. **Test your restore process.** A backup you've never restored is a hope, not a plan.
4. **Log backup success/failure.** Include it in your alerting.

---

## 14. Request/Response Tracing

**The problem it solves:** The client says "it doesn't work." The server says "I never got a request." Without visibility into the actual HTTP traffic, you're debugging with blindfolds on. This is the #1 reason demo failures turn into 30-minute guessing sessions instead of 30-second fixes.

### Correlation IDs

Add a `X-Correlation-Id` header to every request/response. The flow:

1. **Client generates a UUID** and sends it as `X-Correlation-Id` on every request.
2. **Server logs include the correlation ID** on every log entry for that request.
3. **Server echoes it back** in the response header.
4. **Client logs the correlation ID** alongside its own local state.

When something fails, you match client-side and server-side logs by correlation ID. No guessing which request caused which error.

```
Client log:  [abc-123] POST /api/v1/battles → timeout after 30s
Server log:  [abc-123] POST /api/v1/battles → 200 OK in 45ms
Diagnosis:   Request arrived and succeeded. Client timeout is too aggressive.
```

Without this, you get: "It's broken." "The server looks fine." "Well it's not fine on my end." Stalemate.

### Network-Level Capture

For debugging client-server mismatches, nothing beats seeing the raw HTTP:

| Tool | Platform | Best For |
|---|---|---|
| **Charles Proxy** | macOS/Windows/Linux | Mobile app debugging (HTTPS proxy) |
| **Fiddler** | Windows/macOS/Linux | .NET ecosystem, detailed inspection |
| **mitmproxy** | CLI (any platform) | Scripted/automated capture |
| **Chrome DevTools → Network** | Browser | Web client debugging |
| **Android Studio → Network Inspector** | Android | Android-specific traffic |
| **`curl -v`** | CLI | Quick manual verification |

### What to Capture

When a request "doesn't work," capture and compare these between a working and failing request:

1. **Full URL** — including query string, trailing slashes, encoding
2. **HTTP method** — POST vs PUT vs PATCH matters
3. **Request headers** — especially `Authorization`, `Content-Type`, `Accept`
4. **Request body** — is it actually JSON? Is the `Content-Type` header correct?
5. **Response status code** — 401 vs 403 vs 404 vs 500 all mean different things
6. **Response body** — the error message tells you what went wrong
7. **Response time** — did it timeout? Was it slow?

### Recommendations

1. **Implement correlation IDs from day one.** It's a header and a log field. Trivial to add, transformative for debugging.
2. **Require client teams to log the full HTTP response** (status + body) on failure, not just "it failed."
3. **During demos, have a network capture running.** Charles Proxy or browser DevTools. When something breaks, you have the evidence immediately.
4. **Add request/response logging in non-production environments.** Log the full request and response (minus sensitive headers) for every API call. Disable in production for performance/privacy.

---

## 15. Contract Testing for Multi-Team Projects

**The problem it solves:** Team A builds the server. Team B builds the Android app. Team C builds the web frontend. Team A changes a field name from `playerId` to `player_id` and deploys. Teams B and C find out during the demo.

### The Core Idea

Contract testing verifies that the **consumer's expectations** match the **provider's actual behavior** — automatically, before deployment.

### Consumer-Driven Contract Testing (Pact)

The gold standard for multi-team API projects:

1. **Consumer (client) writes a contract:** "When I send `POST /api/v1/battles` with this body, I expect a 201 with a response containing `battleId` as a string."
2. **Contract is shared** via a Pact Broker (or file exchange).
3. **Provider (server) verifies the contract** in its CI pipeline: "Does my API actually return what the consumer expects?"
4. **If the contract breaks, the provider's build fails** — before deployment, before the demo.

```
Consumer contract:
  POST /api/v1/battles
  → 201 { "battleId": "string", "status": "string" }

Provider verification:
  ✓ POST /api/v1/battles returns 201
  ✓ Response contains "battleId" (string)
  ✓ Response contains "status" (string)

Provider changes "battleId" to "battle_id":
  ✗ Response missing "battleId" — CONTRACT BROKEN
  → Build fails. Deploy blocked. Demo saved.
```

### Lighter Alternatives

Not every project needs Pact. Lighter approaches that still catch contract drift:

| Approach | Effort | Catches |
|---|---|---|
| **OpenAPI spec diffing** | Low | Field renames, removed endpoints, type changes |
| **Snapshot testing** on API responses | Low | Unexpected response shape changes |
| **Shared DTO library** (if same language) | Medium | Compile-time contract enforcement |
| **Robot client test** (what we built) | Medium | Broken links, missing endpoints, auth changes |
| **Pact consumer-driven contracts** | High | All of the above, across teams and languages |

### Recommendations

1. **At minimum, diff your OpenAPI spec in CI.** If the spec changes, require a changelog entry. Tools: `openapi-diff`, `oasdiff`.
2. **For multi-team projects, adopt Pact** or similar. The setup cost pays for itself after the first avoided demo failure.
3. **Pin API versions.** Clients declare which version they support. Breaking changes go in a new version.
4. **Never rename a field without a deprecation period.** Add the new field, keep the old one, remove it after all consumers migrate.

---

## 16. Environment Parity

**The problem it solves:** "It works on my machine." "It works in staging." "It works in the unit tests." But it doesn't work in production / during the demo. Environment drift is the silent killer of demos.

### Common Drift Points

| What Drifts | Symptom | Prevention |
|---|---|---|
| **Base URL** | Client hits `localhost:5000` instead of `api.example.com` | Environment config files, not hardcoded URLs |
| **Auth provider** | Token from dev auth server rejected by prod | Environment-specific auth config, tested per environment |
| **DB schema** | Migration ran locally but not in production | Auto-migration on startup + health check that verifies schema |
| **Feature flags** | Feature enabled in dev, disabled in prod | Feature flag dashboard with environment comparison |
| **SSL/TLS** | Works over HTTP locally, fails over HTTPS in prod | Develop with HTTPS locally (`dotnet dev-certs`) |
| **CORS origins** | `localhost` allowed in dev, not in prod | Environment-specific CORS config (we do this) |
| **Third-party keys** | Stripe test keys vs live keys, reCAPTCHA domains | Environment-specific config (we do this) |
| **Connection strings** | Pointing at wrong database | Health check that logs which DB it connected to |
| **Timezone** | Server in UTC, dev machine in local time | Force UTC in app code (`DateTime.UtcNow` everywhere) |

### Environment Verification Script

Build a script (or endpoint) that dumps non-secret configuration for comparison:

```json
GET /api/v1/diagnostics/environment (admin-only)

{
  "environment": "Production",
  "version": "1.0.42",
  "database": "connected (SQL Server, 51 tables)",
  "migrations": "up to date",
  "auth": {
    "jwtIssuer": "apicombat.com",
    "jwtAudience": "apicombat-api",
    "corsOrigins": ["https://apicombat.com", "https://www.apicombat.com"]
  },
  "services": {
    "smtp": "configured (mail5013.site4now.net)",
    "stripe": "configured (live mode)",
    "recaptcha": "configured",
    "azureBackup": "configured"
  },
  "backgroundJobs": 10,
  "uptime": "4h 23m"
}
```

### Recommendations

1. **Never hardcode URLs, keys, or connection strings.** Always pull from environment-specific config.
2. **Build an environment diagnostics endpoint** (admin-protected). When something "doesn't work in prod," this tells you what's different in 5 seconds.
3. **Develop with HTTPS locally.** If you only discover TLS issues in production, it's already too late.
4. **Use `DateTime.UtcNow` everywhere.** Time zone bugs are the sneakiest environment parity issue.
5. **Compare environment configs before deploying to a new environment.** Diff the config files. Missing keys are silent killers.

---

## 17. Client-Side Hardening Guidance

**The problem it solves:** The server is perfectly hardened. The client sends a request, gets a timeout, and shows the user a blank screen. Or it retries 1,000 times in a loop and DDoS-es its own server. The server can't fix client bugs, but it can provide guidance that prevents them.

### Retry Policy

Not all failures are permanent. Clients should retry on transient errors — but intelligently:

```
Retry decision matrix:

  Status 200-299  → Success. Don't retry.
  Status 400-499  → Client error. Don't retry (except 429).
  Status 429      → Rate limited. Retry AFTER the Retry-After header value.
  Status 500-599  → Server error. Retry with backoff.
  Timeout         → Retry with backoff.
  Network error   → Retry with backoff.
```

### Exponential Backoff

```
Attempt 1: wait 1s
Attempt 2: wait 2s
Attempt 3: wait 4s
Attempt 4: wait 8s
Attempt 5: give up, show error to user

Formula: delay = min(base * 2^attempt + random_jitter, max_delay)
Jitter:  add 0-500ms random to prevent thundering herd
Max:     cap at 30s regardless of attempt count
```

### Timeout Configuration

| Operation | Suggested Timeout | Rationale |
|---|---|---|
| Read (GET) | 10s | Should be fast; fail early if not |
| Write (POST/PUT) | 30s | May involve DB transactions |
| File upload | 60-120s | Large payloads need time |
| Health check | 5s | If health check is slow, something is wrong |

### Offline / Degraded Mode

For mobile apps especially:

1. **Detect offline state** before making requests. Don't show a spinner for 30 seconds then fail.
2. **Cache the last-known-good state** and show it with a "data may be stale" indicator.
3. **Queue writes for sync** when connectivity returns (if your data model supports it).
4. **Show the HTTP status code and error message to the user during development.** A blank screen tells you nothing. `"403: Your subscription has expired"` tells you everything.

### What the Server Should Provide to Clients

Include these in your API documentation or developer guide:

- **Base URL per environment** — don't make clients guess
- **Auth flow walkthrough** — step-by-step token acquisition, refresh, and error handling
- **Rate limit headers explained** — what `X-RateLimit-Remaining: 0` means and what to do
- **Error response schema** — every error returns `{ "error": "human-readable message" }` so clients can display it
- **Webhook/callback retry policy** — if the server retries webhooks, document the schedule
- **SDK or example code** — a working `curl` example for every endpoint eliminates an entire class of integration bugs

### Recommendations

1. **Document the retry policy in your API docs.** If clients don't know they should retry on 500, they won't.
2. **Return `Retry-After` on 429 and 503.** Don't make clients guess when to retry.
3. **Provide a Postman collection or `curl` examples.** A working example is worth a thousand pages of documentation.
4. **During integration, have both teams watch the same request** — client devs see what they sent, server devs see what they received. Mismatches become obvious instantly.

---

## 18. Emergency Triage — "The Demo Is Broken Right Now"

**The problem it solves:** It's demo time. Something doesn't work. The clock is ticking. You need a systematic process, not panicked guessing.

### The 60-Second Triage Flowchart

```
START: "The feature isn't working"
  │
  ├─ Step 1: Is the server up?
  │   └─ Hit the health endpoint: GET /health
  │       ├─ No response / timeout → SERVER IS DOWN. Check hosting dashboard. Restart app pool.
  │       └─ 200 OK → Server is up. Continue.
  │
  ├─ Step 2: Is the request reaching the server?
  │   └─ Check server logs (or network capture)
  │       ├─ No log entry → Request isn't arriving. Check: URL, DNS, firewall, VPN, proxy.
  │       └─ Log entry exists → Request arrived. Continue.
  │
  ├─ Step 3: What did the server respond?
  │   └─ Check the HTTP status code
  │       ├─ 401 → Auth issue. Token expired? Wrong scheme? Missing header?
  │       ├─ 403 → Forbidden. User lacks permission? Tier gating? CORS preflight?
  │       ├─ 404 → Wrong URL. Check route, method, version prefix.
  │       ├─ 405 → Wrong HTTP method. Sending GET instead of POST?
  │       ├─ 422 → Validation failed. Check request body against schema.
  │       ├─ 429 → Rate limited. Wait and retry.
  │       ├─ 500 → Server error. Check logs for support ID (ERR-XXXX-XXXX).
  │       └─ 200 → Server thinks it worked. Problem is client-side.
  │
  ├─ Step 4: Server returned 200 but client shows wrong data?
  │   └─ Compare response body to what client is rendering
  │       ├─ Response body is correct → Client parsing/rendering bug.
  │       └─ Response body is wrong → Server logic bug. Check the support ID / logs.
  │
  └─ Step 5: Still stuck?
      └─ Capture the FULL request and response (headers + body) from both sides.
          Compare them byte-by-byte. The mismatch IS the bug.
```

### The Demo Survival Kit

Have these open **before** the demo starts:

| Tool | Purpose |
|---|---|
| **Health endpoint in a browser tab** | Confirms server is alive at a glance |
| **Server log viewer** (admin panel, Azure, or tail -f) | See errors in real-time |
| **Network capture** (Charles, DevTools, etc.) | See exactly what the client sent and received |
| **Backup demo environment** | If prod breaks, switch to staging |
| **The OpenAPI docs page** | Verify endpoint contracts on the spot |

### The 3 Questions That Replace Guessing

When an AI assistant (or a human) starts guessing, redirect with these three questions:

1. **"What is the HTTP status code?"** — This alone eliminates 80% of guesses. A 401 is not a 404 is not a 500.
2. **"What is in the response body?"** — The server almost always tells you what went wrong, if you read the response.
3. **"Does the request match the OpenAPI spec?"** — Wrong route, wrong method, wrong body shape, wrong auth header. Compare against the spec, not against memory.

If you can answer these three questions, you can diagnose most API failures in under a minute. If you can't answer them, no amount of code reading will help — you need to capture the actual traffic first.

### When Debugging with AI

Give the AI assistant **evidence, not symptoms:**

| Bad prompt | Good prompt |
|---|---|
| "The app isn't syncing data" | "POST /api/v1/battles returns 401 with body `{ "error": "Unauthorized" }`. The request includes `Authorization: Bearer <token>`. The token was issued 2 hours ago." |
| "The API is broken" | "GET /api/v1/leaderboard returns 404. The OpenAPI spec shows it should be at `/api/v1/leaderboard`. Here are the request headers: ..." |
| "It works locally but not in prod" | "Health check returns 200. The request reaches the server (I see it in logs). But the response is `403` with `{ "error": "CORS" }`. Production CORS is configured for `apicombat.com` but the client is calling from `www.apicombat.com`." |

**The pattern:** Status code + response body + request details = diagnosis. Symptoms alone = guessing.

---

## 19. Quick-Start Checklist for New Projects

Use this as a starting point when hardening any API-backed project. Not everything applies to every project, but everything here has prevented a real bug or outage.

### Day 1 — Diagnostics First

These cost almost nothing to implement and save hours when things break:

- [ ] **Health check endpoint** — at minimum, verify DB connectivity
- [ ] **Correlation ID header** (`X-Correlation-Id`) — echo from request to response, include in all logs
- [ ] **Structured error responses** — JSON with error message, never stack traces
- [ ] **Environment diagnostics endpoint** (admin-only) — dump non-secret config for comparison

### Week 1 — Foundation

- [ ] **OpenAPI spec auto-generated from code** — serve at a well-known URL
- [ ] **Global exception middleware** with logging and support IDs
- [ ] **Input validation** on every endpoint — length limits, type checks, bounds
- [ ] **Security headers** — X-Content-Type-Options, X-Frame-Options, Referrer-Policy
- [ ] **HTTPS enforced** — no exceptions
- [ ] **CORS locked to production domains**
- [ ] **Secrets not in source control** — use environment-specific config files
- [ ] **Client integration guide** — base URL per env, auth walkthrough, error schema, curl examples

### Week 2 — Trust But Verify

- [ ] **Auth scheme documented per endpoint** — which scheme, what scopes
- [ ] **Integration tests for every auth scenario** — right scheme, wrong scheme, expired, deleted account
- [ ] **Robot client test** that walks the API via HATEOAS links or spec
- [ ] **Rate limiting** with response headers (`X-RateLimit-Remaining`, `Retry-After`)
- [ ] **Transaction boundaries** on multi-step operations (especially currency/inventory)
- [ ] **Webhook handlers return proper status codes** — 500 on failure, not 200
- [ ] **No silent exception swallowing** — every catch block logs something
- [ ] **OpenAPI spec diffing in CI** — detect breaking changes before deployment
- [ ] **Environment parity check** — diff config across dev/staging/prod

### Week 3 — Observability

- [ ] **Audit trail** for state changes — who changed what, when
- [ ] **Admin audit log** for privileged actions
- [ ] **Automated alerts** for error rate spikes, queue stalls, capacity limits
- [ ] **Smoke-level load test** — 10 users, 1 minute, run before demos
- [ ] **Playwright or similar browser smoke tests** for critical user flows
- [ ] **Request/response logging** in non-production environments

### Before Every Demo

- [ ] Run the smoke load test
- [ ] Hit the health endpoint — confirm server is up and DB is connected
- [ ] Hit the environment diagnostics endpoint — confirm config matches expectations
- [ ] Verify the last deployment succeeded (check logs, not assumptions)
- [ ] Test the **specific flow you're going to demo**, end to end, on the target environment
- [ ] Have server logs open in a second window during the demo
- [ ] Have a network capture tool ready (Charles, DevTools, etc.)
- [ ] Know the 3 triage questions: status code? response body? does it match the spec?

---

## References

- [Implementation Task List](IMPLEMENTATION_TASK_LIST.md) — detailed audit of every hardening change
- [Launch Checklist](LAUNCH-CHECKLIST.md) — production readiness verification
- [Manual Test Checklist](MANUAL_TEST.md) — Section 42 covers production hardening scenarios
- [NBomber Setup Guide](NBOMBER-SETUP.md) — load testing configuration and benchmarks
- [Setup Guide](SETUP.md) — deployment and environment configuration

---

*Sections 1-13 were distilled from 50+ hardening commits applied to API Combat over 10 days in February 2026. Sections 14-18 capture cross-project lessons from real-world client integration failures. Every item here prevented or would have prevented a real production issue.*
