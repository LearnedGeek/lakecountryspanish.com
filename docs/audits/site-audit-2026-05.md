# LCS Site Audit — May 2026

**Audit date:** 2026-05-27
**Auditor:** Claude, with Mark
**Scope:** lakecountryspanish.com codebase as of commit `f24a342`, in preparation for the SmarterASP → Hetzner production migration scheduled for the weekend of 2026-05-30.
**Method:** Code survey + claude-recall prior context + dependency scan + grep-based pattern checks. Not a full pen-test or external security review.

## Executive summary

The codebase is in **good shape overall**. No vulnerable packages, no async/await footguns, no empty catch blocks, no hardcoded secrets, no SQL injection patterns. Authorization attributes are applied correctly across the controller surface. Stripe webhooks verify signatures. ReCaptcha protects the contact form.

**Two findings would meaningfully affect the Hetzner migration and should be addressed before cutover:**

1. **DataProtection keys are not persisted** — every restart of the systemd unit invalidates all session cookies, antiforgery tokens, and identity cookies. Users get logged out on every deploy. On stg we already saw the journal warning: *"Using an in-memory repository. Keys will not be persisted to storage."*
2. **ForwardedHeaders middleware is missing** — the app needs to trust `X-Forwarded-Proto` from Caddy so HttpsRedirection works correctly, cookie security policies behave, and generated URLs return `https://` instead of `http://`.

Both are small code changes that ship in the migration commit. Detailed remediation below.

A handful of medium/low-priority hardening items are also called out — not blocking the migration, but worth picking up over the next few sprints.

## Site at a glance

| Dimension | Count / state |
|---|---|
| Entities | 45 (Identity + scheduling + payments + gamification + assignments + Phase 3 curriculum + media) |
| Controllers | 14 (Account, Admin, Assignment, Contact, Curriculum, Home, Media, Payment, PlacementTest, ScheduledTasks, Sitemap, Student, Subscription, Teacher, Token) |
| Services | ~30 (top-level) + Curriculum/Drafter/ + Curriculum/Blocks/ + Media/ subfolders |
| Migrations | 3 (InitialSchema, SeedWisconsinStandards, AddDayBodyBlocksJson) |
| Test methods | ~462 across Controllers, Services, Models, Integration |
| Tests passing | 504 / 509 (5 known Stripe E2E failures need real test key) |
| Target framework | net10.0 |
| Database | PostgreSQL 18.4 (recently migrated from SQL Server LocalDB) |
| AI integration | Anthropic API via `IClaudeApiService` (assignments + placement) and `ICurriculumDrafter` (lesson drafting) |
| Media | Local filesystem `wwwroot/uploads/media/` + Pixabay search via API |
| Auth | ASP.NET Identity, cookie-based, ReCaptcha on contact, Stripe webhook signing |

## Findings

### High priority — fix before Hetzner migration

#### 1. DataProtection keys are in-memory only

**Symptom:** Every restart of `lakecountryspanish.service` invalidates auth cookies, antiforgery tokens, and TempData encryption. Users sign out involuntarily; in-flight form submits fail with antiforgery errors.

**Where:** `Program.cs` — no `AddDataProtection()` configuration. The default keystore is in-memory.

**Evidence:** Stg journal contains:
```
warn: Microsoft.AspNetCore.DataProtection.Repositories.EphemeralXmlRepository[50]
      Using an in-memory repository. Keys will not be persisted to storage.
warn: Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager[59]
      Neither user profile nor HKLM registry available. Using an ephemeral key repository.
```

**Fix:**

```csharp
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/var/www/lakecountryspanish/keys"))
    .SetApplicationName("LakeCountrySpanish");
```

Plus ensure `/var/www/lakecountryspanish/keys/` exists, is owned by `www-data`, and is NOT wiped by the tar-over-SSH atomic swap during deploys. The deploy script needs to either preserve the keys directory or store keys outside the deploy target.

**Recommended layout:** keys in `/var/lib/lakecountryspanish/keys/` (outside `/var/www/`), which the deploy script never touches. App config references that path.

**Severity:** High — affects every authenticated user every deploy.

#### 2. ForwardedHeaders middleware not configured

**Symptom:** Behind Caddy (or any reverse proxy), the app sees requests as `http://` because Caddy terminates TLS. Without `UseForwardedHeaders`, `HttpsRedirection` looks at the inner scheme and may redirect-loop, `Url.Action()` generates `http://` URLs, and cookies marked `SecurePolicy=Always` won't be set.

**Where:** `Program.cs` — middleware pipeline does not include `UseForwardedHeaders`.

**Fix:**

```csharp
// Before any middleware that reads the scheme/host:
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Trust only the local Caddy proxy.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    options.KnownProxies.Add(IPAddress.Loopback);
});

// And in the request pipeline, FIRST middleware:
app.UseForwardedHeaders();
```

This is mandatory for any ASP.NET app behind a reverse proxy. The SmarterASP setup may have masked this because IIS handles it differently.

**Severity:** High — affects HTTPS enforcement and Stripe redirect URLs.

### Medium priority — address soon after migration

#### 3. No rate limiting on `/Account/Login`, `/Contact`, or admin endpoints

**Symptom:** A bot or determined attacker can submit unlimited login attempts. ReCaptcha protects `/Contact` against spam but `/Account/Login` has no such protection.

**Fix:** Wire up `AddRateLimiter` with a token-bucket policy for the auth surface. ~30 lines of `Program.cs` configuration. Low risk; well-documented pattern.

**Severity:** Medium — Karen's site is small enough that targeted attacks are unlikely, but lockout-bot prevention is cheap.

#### 4. Cookie SecurePolicy not explicit

**Symptom:** Cookie security policy defaults to `Auto`, which is *usually* right but not guaranteed when behind a proxy whose forwarded-headers we don't trust yet (see Finding 2).

**Fix:**

```csharp
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
    options.Secure = CookieSecurePolicy.Always;     // prod-only behavior wanted
    options.HttpOnly = HttpOnlyPolicy.Always;
});
app.UseCookiePolicy();
```

**Severity:** Medium — depends on Finding 2 being fixed first.

#### 5. Razor `@Html.Raw` rendering of `Model.QuestionsJson` / `Model.AnswersJson` in Assignment views

**Symptom:** The Assignment views inject assignment question JSON directly into `<script>` blocks via `@Html.Raw(Model.QuestionsJson)`. If a malicious admin or compromised admin account writes malformed JSON containing `</script>...` payloads, an XSS payload could execute against students taking the assignment.

**Where:** `Views/Admin/ReviewAssignment.cshtml`, `Views/Admin/ViewAssignment.cshtml`, `Views/Assignment/Take.cshtml`.

**Fix:** Use `@Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.QuestionsJson))` (the pattern already used in `ViewAssignment.cshtml` line 243) and wrap consistently. This is the proper "JSON-escape for a JS context" idiom and prevents script-context breakouts.

**Severity:** Medium — admin-controlled content, real risk is small, but the pattern is inconsistent and one of the three views is wrong.

### Low priority — quality-of-life hardening

#### 6. No CSP / X-Frame-Options / Referrer-Policy headers

The site doesn't emit security headers. For a small site this is fine; for a paid-customer site, worth adding.

#### 7. No health endpoint

There's no `/health` or `/healthz` endpoint a monitoring system could hit. Cheap to add (10 lines). Becomes important if Mark wants Uptime-Robot-style monitoring or Caddy active health checks.

#### 8. The `*.prev` rollback directory pattern isn't used yet on stg

Allevo and LearnedGeek workflows preserve `/var/www/{name}.prev/` for one-`mv` rollback. The stg deploy workflow doesn't yet — manual rollback would require redeploying from an earlier commit. Not urgent for stg; **must** be in place for prod.

#### 9. Test coverage gaps

The 462 test methods miss:
- `CurriculumController` (Phase 3 work — no tests yet)
- `MediaController` + `MediaService` + `PixabayImageSourceAdapter` (Phase 3)
- `DocumentRenderingService` (the Markdig pipeline)
- `BlockCompiler` (the block → Markdown compiler)
- `CurriculumDrafter` (the AI lesson drafter)

All recent Phase 3 work is uncovered. The pre-Phase-3 platform has reasonable coverage.

**Recommendation:** Backfill tests for `BlockCompiler` and `CurriculumDrafter` (the pure-logic pieces with no external dependencies). Defer media/curriculum controller tests since they depend on filesystem + Anthropic API.

## Migration-critical checklist

The Hetzner migration runbook ([docs/migration/lakecountryspanish-prod-to-hetzner.md](../migration/lakecountryspanish-prod-to-hetzner.md)) should add these items to its pre-cutover validation:

- [ ] Apply Finding 1 (DataProtection key persistence). Verify auth cookies survive `systemctl restart lakecountryspanish.service`.
- [ ] Apply Finding 2 (ForwardedHeaders). Verify `Url.Action()` returns `https://` URLs and HttpsRedirection doesn't loop.
- [ ] Verify Stripe webhook URL points to the new domain post-cutover (`https://lakecountryspanish.com/api/webhook/stripe` or similar).
- [ ] Confirm the prod admin password is changed from the seeded `Admin123!`.
- [ ] Apply Finding 8 (`*.prev` rollback) to the `deploy-prod.yml` workflow.

## What the Hetzner migration explicitly resolves

For completeness, listing what the SmarterASP → Hetzner move makes better, not just what it requires:

- **SSL renewal:** Caddy auto-renews; no more certbot pain.
- **Database:** SQL Server → Postgres. Cheaper, lighter, no LocalDB-style file lock issues.
- **Deploy:** GitHub Actions push-to-deploy; no more WebDeploy + msdeploy.exe.
- **Cost:** $0 incremental (existing VPS).
- **.NET version:** SmarterASP was pinned to .NET 8; Hetzner runs .NET 10.
- **Operational visibility:** `journalctl -u lakecountryspanish` for logs; SmarterASP logs were harder to reach.

## What this audit explicitly did NOT do

- External pen-test or fuzz-testing
- Full database performance audit (no slow-query review, no index audit)
- Mobile/tablet UX testing
- Browser compatibility matrix
- Load testing
- Accessibility audit (WCAG)
- Privacy / GDPR / COPPA review (relevant since the platform serves K4-Grade 8 students)

These are worth doing in their own focused passes. The migration doesn't depend on any of them.

## Filed as GitHub issues

| Finding | Issue # | Priority |
|---|---|---|
| 1. DataProtection key persistence | [#4](https://github.com/LearnedGeek/lakecountryspanish.com/issues/4) | High — blocks migration |
| 2. ForwardedHeaders middleware | [#5](https://github.com/LearnedGeek/lakecountryspanish.com/issues/5) | High — blocks migration |
| 3. Rate limiting | [#6](https://github.com/LearnedGeek/lakecountryspanish.com/issues/6) | Medium |
| 4. Cookie SecurePolicy | [#7](https://github.com/LearnedGeek/lakecountryspanish.com/issues/7) | Medium |
| 5. Assignment Razor XSS | [#8](https://github.com/LearnedGeek/lakecountryspanish.com/issues/8) | Medium |
| 6. Security headers | (deferred — low priority) | Low |
| 7. Health endpoint | [#9](https://github.com/LearnedGeek/lakecountryspanish.com/issues/9) | Low |
| 8. `*.prev` rollback in workflows | (handled inline in migration runbook) | Low |
| 9. Test coverage gaps | (deferred — open with no issue yet) | Low |

Plus pre-existing open issues:
- [#2](https://github.com/LearnedGeek/lakecountryspanish.com/issues/2) Backend DateTimes must be UTC
- [#3](https://github.com/LearnedGeek/lakecountryspanish.com/issues/3) SubscriptionTier admin UI
