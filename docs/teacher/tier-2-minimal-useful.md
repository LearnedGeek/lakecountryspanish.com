# Teacher Role — Tier 2: Minimal-useful dashboard

**Status:** Planned, not started
**Estimated effort:** ~3 hours of focused work
**Recommended timing:** Friday 2026-05-29, before the Hetzner migration weekend, so prod launches with a real teacher experience
**Depends on:** Tier 1 (Cece's account on stg) — done out-of-band by Mark

## Goal

Move the teacher dashboard from "welcome + coming soon" to "welcome + working tools." After Tier 2 ships, Karen and Cece can both use the platform for real content work: browse the curriculum, generate binders, upload and pick media. Tier 2 does **not** include multi-tenancy / data scoping — both teachers see the same data. That's Tier 3.

## Scope boundary

Tier 2 is about **content** access (curriculum, media, binders). Tier 3 is about **business operations** scoping (students, payments, schedule, payouts).

A teacher in Tier 2 can do:

| Area | Teacher access |
|---|---|
| Curriculum browse (read) | ✅ |
| Curriculum drafting (Day-level via AI drafter) | ✅ |
| Curriculum block editor (advanced mode) | ✅ |
| Binder preview + generation | ✅ |
| Media library — browse | ✅ |
| Media library — upload | ✅ |
| Media library — delete (others' uploads) | ❌ |
| Pixabay search + import | ✅ |
| Student admin (list, edit, profile) | ❌ Admin-only |
| Payment / Subscription admin | ❌ Admin-only |
| Analytics dashboard | ❌ Admin-only |
| Scheduled-classes admin (blocked dates, time slots) | ❌ Admin-only |
| Account profile (password change, etc.) | ✅ Self-service |

The "❌ Admin-only" things stay where they are — guarded by `[Authorize(Roles = AppRoles.Admin)]`. The "✅" things either need to broaden their attribute to `Admin,Teacher` or get a Teacher-only entry point.

## Specific work items

### 1. Broaden `Authorize` attributes on content controllers

Currently most non-account controllers require `Admin`. For Tier 2, broaden the curriculum + media surface to include `Teacher`.

**Files to edit:**

- `Controllers/CurriculumController.cs` — change class-level `[Authorize(Roles = AppRoles.Admin)]` to `[Authorize(Roles = "Admin,Teacher")]` (or use an `AppRoles.AdminOrTeacher` const for tidiness).
- `Controllers/MediaController.cs` — same.

**Caution:** the `Delete` action on `MediaController` should stay Admin-only. Add `[Authorize(Roles = AppRoles.Admin)]` at the action level to override the class-level broadening.

### 2. Enhance the Teacher dashboard with action cards

**File:** `Views/Teacher/Dashboard.cshtml`

Replace the current "What's next" placeholder panel with three or four action cards (Tailwind-styled, similar to the existing admin tile pattern):

- **Browse curriculum** → `/Curriculum/Days`
- **Draft a lesson** → `/Curriculum/Days/New`
- **Generate a binder** → `/Curriculum/PreviewBinder?slug=los-colores` (or a binder-list page if we have time)
- **Media library** → `/Media`

Each card has a title, one-sentence description, and a CTA button. Tile width 1/2 on small screens, 1/4 on large.

**View model:** `TeacherDashboardViewModel` currently has Name + Email + JoinedDate. Optionally add:
- Count of Days the teacher has drafted (`int DraftsCount`)
- Most recently edited Day (`Day? RecentDay`)
- Both make the dashboard feel less empty

### 3. Add Teacher entries to the top navigation

**File:** `Views/Shared/_Navigation.cshtml`

Currently when `User.IsInRole("Teacher")`, the nav shows only "Teacher Dashboard." Add:
- **Curriculum** → `/Curriculum/Days`
- **Media** → `/Media`

Visible to both Admin and Teacher roles. Admin keeps "Admin Dashboard" on top of these.

### 4. Lock down the "owner-only" routes a teacher might try to navigate to

A teacher who types `/Admin/Students` should get a 403, not see the page. Currently `AdminController` is `[Authorize(Roles = AppRoles.Admin)]` at the class level, so this is already correct — just verify after the broadening above that no admin action accidentally allows Teacher.

**Quick test:** sign in as a Teacher, manually navigate to:
- `/Admin/Dashboard` → 403 (Forbidden)
- `/Admin/Students` → 403
- `/Admin/Payments` → 403
- `/Curriculum/Days` → 200
- `/Media` → 200
- `/Teacher/Dashboard` → 200

### 5. Update the "What's next" Coming-Soon panel

The current view says:

> Your full instructor dashboard is being built. Over the summer, you'll see the following here:
> - Your assigned classes and students for the current period
> - The Lake Country Spanish curriculum library to browse and pick lessons from
> - Tools to assemble and print binders for your in-person classes
> - Your monthly statements and payout history

Tier 2 delivers items 2 and 3. Update the panel to:

> **Coming soon (Tier 3):**
> - Your assigned classes and students for the current period
> - Your monthly statements and payout history
>
> The curriculum library and binder tools are now available above.

Or remove the panel entirely once the action cards land — it's redundant.

### 6. Verify on stg before the migration

Once Tier 2 ships to stg (via the existing `deploy-stg.yml` CI workflow), walk through it as Cece:

- [ ] Sign in as `cece@lakecountryspanish.com`
- [ ] Land on `/Teacher/Dashboard` — see welcome + action cards
- [ ] Click "Browse curriculum" — see the Days list
- [ ] Click "Draft a lesson" — fill in the brief, draft a sample lesson
- [ ] Click "Generate a binder" — render the Los Colores binder
- [ ] Click "Media library" — browse, search Pixabay
- [ ] Try to navigate to `/Admin/Dashboard` — get 403

If all check out, the same code rides through the prod migration over the weekend.

## What this does NOT include

- **Per-teacher data scoping.** Both Karen and Cece see the same curriculum, same media library, same drafts. That's Tier 3.
- **Class-attendance tracking.** Teachers don't yet mark students present/absent. That's a separate slice — depends on Tier 3's student-to-teacher relationship being in place first.
- **Payout statements.** Same — depends on Tier 3 revenue-split logic.
- **A "my drafts only" filter on the curriculum list.** Currently the Days list shows everything. Could add an optional `?author=me` filter as a small Tier 2.5 improvement.

## Open questions

1. **Should teachers be allowed to delete their own media uploads?** Tier 2 ships with no media delete for teachers. Tier 3 could add "delete if I uploaded it" via an author check on the entity.
2. **The Curriculum "Power-user mode" link is hidden in the default UI** (per a prior decision so Karen and Cece never see it). Tier 2 maintains that — teachers don't see Power User Mode in any link, but the URL still works if they happen to know it. Confirm this is still the right call.

## Definition of done

- Cece can log in on prod and use the curriculum + binder + media features end-to-end.
- Cece cannot reach any admin-only route (every attempt = 403).
- The dashboard reflects what's actually available, not promises.
- Tests pass for the broadened `Authorize` attributes (add 2-3 controller tests that assert a Teacher gets 200 on `/Curriculum/Days` and 403 on `/Admin/Dashboard`).
