# Migrating lakecountryspanish.com from SmarterASP to Hetzner — Runbook

**Status:** Plan, not yet executed
**Goal:** Move production lakecountryspanish.com off SmarterASP onto the existing Hetzner VPS, sitting alongside the already-running stg.lakecountryspanish.com and Mark's other sites (LearnedGeek, Allevo).
**Why:** SmarterASP's certbot-managed SSL is a recurring operational pain. Hetzner + Caddy auto-renews certs without intervention. Same VPS already runs LearnedGeek and Allevo so there's no new hosting bill for LCS prod.
**Approach:** Fresh-start migration — no student/payment data preserved (per Mark, 2026-05-27: "no students, history is just our testing"). This dramatically simplifies the runbook.

## Pre-migration decisions (locked)

| Decision | Choice | Rationale |
|---|---|---|
| Data preservation | **Fresh start** | No real student data on the SmarterASP prod yet; testing data only |
| Hosting | **Hetzner VPS** (same box as stg, LearnedGeek, Allevo) | Cost = $0 incremental; ops surface already familiar |
| Reverse proxy | **Caddy** | Auto Let's Encrypt; same pattern as the other sites |
| Database | **Postgres** (new `lakecountryspanish` DB on existing Postgres 18.4) | Matches stg + Allevo + LearnedGeek; no SQL Server dependency |
| Cloudflare proxy | **DNS-only (grey cloud)** | Caddy needs direct HTTP access for ACME challenges; matches stg |
| Workspace email | **Decide separately** | Not blocking the migration; the new prod can ship with Site4Now SMTP and change later |

## Architecture target

```
                       lakecountryspanish.com  →  Cloudflare DNS
                                                       │
                                          (A record, DNS-only)
                                                       ▼
                                          Hetzner VPS (5.161.205.21)
                                                       │
                                                     Caddy
                                          ┌────────────┼────────────┐
                                          ▼            ▼            ▼
                              lakecountryspanish    stg.lcs       (others)
                                 :5029              :5028
                                 systemd:           systemd:
                                 lakecountry-       lakecountry-
                                 spanish.service    spanish-stg.service
                                                       │            │
                                                       └────────────┴── Postgres 18.4
                                                                       (separate DBs)
```

## Stage 1 — Stand up prod-Hetzner infrastructure (no DNS changes yet)

This stage is **safe to do anytime** — nothing visible to public traffic until Stage 4.

### 1.1 — Create the Postgres DB + role

```bash
ssh learnedgeek-host
sudo -u postgres psql <<EOF
CREATE ROLE lakecountryspanish WITH LOGIN PASSWORD '<NEW STRONG PASSWORD>';
CREATE DATABASE lakecountryspanish OWNER lakecountryspanish;
EOF
```

Generate the password with `openssl rand -base64 24 | tr -d '/+=' | head -c 32`. Save it to a GitHub secret called `PROD_PG_PASSWORD` in the SpanishScheduler repo.

### 1.2 — Create the app directory

```bash
sudo install -d -o www-data -g www-data -m 755 /var/www/lakecountryspanish
```

### 1.3 — Create the systemd unit

`/etc/systemd/system/lakecountryspanish.service`:

```ini
[Unit]
Description=Lake Country Spanish — lakecountryspanish.com
After=network.target postgresql.service
Wants=postgresql.service

[Service]
WorkingDirectory=/var/www/lakecountryspanish
ExecStart=/usr/bin/dotnet /var/www/lakecountryspanish/LakeCountrySpanish.Web.dll
Restart=on-failure
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=lakecountryspanish
User=www-data
Group=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5029
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
Environment=DOTNET_NOLOGO=true

[Install]
WantedBy=multi-user.target
```

Note port **5029**, separate from stg (5028).

```bash
sudo systemctl daemon-reload
sudo systemctl enable lakecountryspanish.service
```

Don't start the service yet — it will fail without the app deployed.

### 1.4 — Caddy site block on a temporary hostname (cert pre-warm)

We can't issue a Let's Encrypt cert for `lakecountryspanish.com` until DNS points to Hetzner. But we want to verify the site works on Hetzner BEFORE flipping DNS. Solution: pre-deploy under a temporary hostname that already resolves to Hetzner (e.g. `new.lakecountryspanish.com`).

Add to `/etc/caddy/Caddyfile`:

```caddyfile
new.lakecountryspanish.com {
    reverse_proxy localhost:5029
}
```

Add a Cloudflare A record: `new.lakecountryspanish.com` → `5.161.205.21`, DNS-only, Auto TTL.

```bash
sudo caddy validate --config /etc/caddy/Caddyfile
sudo systemctl reload caddy
```

Caddy will obtain a Let's Encrypt cert for `new.lakecountryspanish.com` on the first request. Watch the journal to confirm:

```bash
sudo journalctl -u caddy -n 50
```

### 1.5 — Create the deploy workflow

New file `.github/workflows/deploy-prod.yml` modeled on `deploy-stg.yml`:

- `name: Deploy LCS to Hetzner (prod)`
- Triggers: **`workflow_dispatch` only** for now (manual-only). Add `push: branches: [main]` only after the first successful manual deploy is verified.
- Target: `/var/www/lakecountryspanish/` (no `-stg`), `lakecountryspanish.service`, port 5029.
- Smoke test URL: `https://new.lakecountryspanish.com` during pre-cutover, change to `https://lakecountryspanish.com` after cutover.
- Renders `appsettings.Production.json` from the existing template with `${PROD_PG_PASSWORD}`, `${CLAUDE_API_KEY}`, `${PIXABAY_API_KEY}` — same secrets system used for stg.

### 1.6 — Required GitHub secrets (in addition to existing stg ones)

| Secret | Value |
|---|---|
| `PROD_PG_PASSWORD` | The new password from step 1.1 |
| (reuse) `DEPLOY_SSH_KEY` | Same keypair as stg — already on Hetzner's authorized_keys |
| (reuse) `DEPLOY_HOST` | Same |
| (reuse) `DEPLOY_USER` | Same |
| (reuse) `CLAUDE_API_KEY` | Same as stg (or new prod-specific key — your call) |
| (reuse) `PIXABAY_API_KEY` | Same |

The `appsettings.Production.json.template` needs minor edits: parameterize the DB name (`lakecountryspanish_stg` → `lakecountryspanish` for prod) via the existing `${VAR}` machinery, OR keep two templates. Simplest is one template using a `${PG_DATABASE}` placeholder, with a workflow `env` setting it differently per workflow file.

### 1.7 — First deploy

Run `workflow_dispatch` on `deploy-prod.yml` from the GitHub Actions tab. Watch for:
- Build + tests pass
- Tar-over-SSH succeeds
- systemd unit starts
- Smoke test against `https://new.lakecountryspanish.com` returns 2xx

If that all works, stop and move to Stage 2.

## Stage 2 — Pre-cutover validation on the temporary hostname

Open `https://new.lakecountryspanish.com` in a browser and run through:

- [ ] Homepage loads with current branding + pricing
- [ ] Login flow works (admin@lakecountryspanish.com, new admin password)
- [ ] Contact form submits + email arrives (verify SMTP works)
- [ ] Admin dashboard loads
- [ ] Curriculum tab → can draft a sample lesson via the AI drafter
- [ ] Media tab → can browse Pixabay
- [ ] Stripe pages render (even if not transacting yet)
- [ ] Pricing tiers display correctly ($28 / $25 / $23 from the seeded data)
- [ ] No 500s in `journalctl -u lakecountryspanish`

**This is the dress rehearsal.** Anything missing or broken gets fixed here, while traffic is still going to SmarterASP.

## Stage 3 — DNS prep (24 hours before cutover)

In Cloudflare, edit the existing `lakecountryspanish.com` A record and the `www` CNAME record. **Set TTL to 1 minute** (Auto by default — change to 60s explicitly). Don't change the IP yet.

Why: Cloudflare's default TTL is fast, but lowering it explicitly to 60s ensures the moment we flip the A record value, every resolver picks up the new IP within a minute or two. Without this step, some users might see the old SmarterASP IP for hours after the flip.

Wait at least the previous TTL period (probably ~1 hour) before the cutover. 24 hours is safer.

## Stage 4 — Cutover

This is the only public-facing step. Total downtime: under 5 minutes if everything goes well.

### 4.1 — Add the real hostnames to Caddy

Edit `/etc/caddy/Caddyfile`. Change the temporary block:

```caddyfile
new.lakecountryspanish.com {
    reverse_proxy localhost:5029
}
```

To:

```caddyfile
lakecountryspanish.com, www.lakecountryspanish.com, new.lakecountryspanish.com {
    reverse_proxy localhost:5029
}
```

(Keep `new.lakecountryspanish.com` in the list for now as a fallback for testing.)

```bash
sudo caddy validate --config /etc/caddy/Caddyfile
sudo systemctl reload caddy
```

Caddy will obtain new certs for the apex + www on first request. **It can't get those certs until DNS points here.** That's the next step.

### 4.2 — Flip Cloudflare A records

In Cloudflare:
- Change `lakecountryspanish.com` A record from `204.188.228.135` → `5.161.205.21`
- Change `www.lakecountryspanish.com` — confirm it's still a CNAME to `lakecountryspanish.com` (it points to the apex, so changes follow automatically)

DNS propagates within ~60 seconds with the lowered TTL.

### 4.3 — Watch Caddy obtain the certs

```bash
sudo journalctl -u caddy -f
```

You should see ACME flow for `lakecountryspanish.com` and `www.lakecountryspanish.com` complete within 10-15 seconds.

### 4.4 — Smoke test the live URL

```bash
curl -sI https://lakecountryspanish.com/
curl -sI https://www.lakecountryspanish.com/
```

Both should return `HTTP/2 200`. If either returns an error, see rollback below.

## Stage 5 — Post-cutover verification

Open `https://lakecountryspanish.com` in a browser and confirm:

- [ ] Cert is valid (browser shows green padlock, issued by Let's Encrypt R10/R11)
- [ ] Homepage loads with current branding
- [ ] Pricing displays $28 / $25 / $23
- [ ] Contact form submits + email arrives
- [ ] Admin login works
- [ ] **External CDN-cached references all resolve** (look at the network tab for any 404s — old SmarterASP-specific paths could break)

### Update external references

- [ ] **Stripe webhook URL** (if there's a live webhook): update from `lakecountryspanish.com/api/...` → same URL but now Hetzner serves it. Verify webhook delivery with a Stripe test event.
- [ ] **Google Search Console**: re-verify the property (file-based verification might break if file location changed; meta-tag verification carries over).
- [ ] **Email link references**: any emails sent from the old site referenced absolute URLs to `lakecountryspanish.com` — those keep working since the domain is the same.

## Stage 6 — Decommission SmarterASP

**Wait 1-2 weeks** of quiet operation on Hetzner before pulling the SmarterASP plug. Two weeks is enough to catch any:
- DNS caching edge cases
- Stripe webhook delivery issues
- Email deliverability problems
- Search engine recrawl glitches

After the quiet period:

- [ ] Cancel the SmarterASP hosting subscription
- [ ] Archive any data backups locally (just for safety)
- [ ] Delete the broken `.github/workflows/deploy-prod-smarterasp.yml` workflow from the repo
- [ ] Update `docs/migration/lakecountryspanish-prod-to-hetzner.md` to mark "✅ COMPLETE" so future-Mark knows this happened

## Rollback plan

If Stage 4 (cutover) goes badly:

1. **Revert the Cloudflare A record** to `204.188.228.135` (SmarterASP). DNS propagates in 60s.
2. **No Caddy changes needed** — the Hetzner config remains, just stops being hit.
3. **Investigate at leisure.** SmarterASP keeps serving until you flip again.

If something subtler is broken (e.g., a specific feature 500s):
- Check `journalctl -u lakecountryspanish -n 200`
- Roll forward (fix the bug + redeploy) rather than back, unless the issue is critical
- The `lakecountryspanish.prev` directory holds the previous deploy for quick `mv`-based rollback at the app level (same pattern as stg/Allevo/LearnedGeek)

## Cost summary

| Item | Old (SmarterASP) | New (Hetzner) |
|---|---|---|
| Hosting | ~$10-15/month | $0 incremental (existing VPS) |
| SSL | Pain (certbot manual) | Free, auto-renewed by Caddy |
| Database | SQL Server included | Postgres on same VPS |
| Email | Site4Now (separate, kept) | Same (no change) |
| **Net savings** | | **~$10-15/month** |

Plus: removing the SQL-Server-only constraint frees up future development (everything builds against Postgres now).

## After this migration: future cleanup

Mark mentioned (2026-05-27) wanting to consolidate off SmarterASP for all his certbot-managed sites — except CrewTrack which has SmarterASP-specific features. The pattern from this LCS migration is repeatable:

- For each site: Hetzner Caddy block + systemd unit + Postgres DB (or just static files via Caddy if no app).
- Same `deploy-prod.yml` workflow pattern, parameterized per site.
- DNS flip last; staging via temporary hostname first.

LCS is the proof-of-pattern. After it's stable, the next migration takes 2-3 hours instead of a planned evening.

---

## Open questions to resolve before execution

1. **Workspace email decision** — does the prod `appsettings.Production.json` ship with Site4Now SMTP (current) or Workspace SMTP (future)? Defaultable to current for now.
2. **Claude API key** — share the existing stg key for prod, or generate a separate prod-only key for blast-radius? Either works; separate keys let us track prod usage independently.
3. **Production admin password** — `Admin123!` is the seeded default. Change to a real password before exposing prod to the world.
4. **Production student data** — confirmed fresh-start, but if Karen wants the existing pricing page copy / about page / etc. ported, that's all already in the codebase (Razor views) — no separate "content migration" step needed.

## Recommended timing

- **Stages 1 + 2 (infrastructure stand-up + dress rehearsal):** one evening, ~2 hours of focused work.
- **Stage 3 (DNS prep):** 5 minutes, do it 24 hours before cutover.
- **Stage 4 + 5 (cutover + verify):** 30 minutes, schedule for a low-traffic moment.
- **Stage 6 (decommission):** 2 weeks later, 30 minutes.

Total active work: ~3 hours across the timeline.

---

*This runbook intentionally errs toward caution — staged validation, TTL preparation, rollback path, decommission delay. The LCS prod traffic is low, so most caution is overkill, but the same runbook will guide future site migrations where traffic IS high. Better to over-prepare here and reuse the pattern.*
