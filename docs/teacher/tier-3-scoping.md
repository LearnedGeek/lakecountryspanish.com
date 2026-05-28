# Teacher Role — Tier 3: Booth-rental multi-tenancy / data scoping

**Status:** Planned, not started
**Estimated effort:** 5-10 dev hours, broken into 3-4 focused sessions
**Recommended timing:** Mid-June 2026, after the Hetzner migration is stable and after Tier 2 is in production
**Depends on:** Tier 2 (minimal-useful dashboard) and the prod migration to Hetzner

## Goal

Make the booth-rental business model work in the data layer. Teachers see only their own students, their own schedule, their own revenue. Karen as LLC owner sees everything. The platform stops being a "single-instructor app with role guards" and becomes a real multi-tenant system within one LLC's customer base.

## Why this matters

The LCS business model (per the founding-instructors framework) is **booth rental**: each instructor runs their own teaching practice under the LCS brand. They bring their own students. LCS provides curriculum, payments, scheduling, brand. LCS takes a percentage of each lesson. Multiple instructors share the same platform but their teaching practices are commercially separate.

Without proper data scoping:
- Karen sees Cece's students; Cece sees Karen's students. Privacy fail.
- Cece can see Karen's revenue. Commercial sensitivity fail.
- Cece can edit Karen's scheduled classes by mistake. Operational fail.
- Future founding instructors (Flor, Norelis) extend the same problems.

Tier 3 fixes all of that.

## Scoping primitive — the architectural decision

Three candidate approaches. Pick one and apply consistently.

### Option A: Direct ownership — `ApplicationUser.TeacherId`

Add a nullable `string? TeacherId` foreign key on `ApplicationUser` (the student). A student belongs to one teacher. Null = unassigned / Karen's pool.

| Pros | Cons |
|---|---|
| Simple model — one column | Forces 1:1 student-to-teacher; doesn't support a student who tries Karen one week and Cece the next |
| Easy to filter (every query just adds `Where(s => s.TeacherId == currentTeacherId)`) | Period semantics get lost — when an LCS contract ends, students don't "expire" |
| Migration is easy (set TeacherId = Karen for all existing students) | Doesn't map cleanly to substitute-teaching or shared-students |

### Option B: Period-based — use `TeacherClassAssignment`

The entity already exists. A teacher gets a `TeacherClassAssignment` to teach a specific `LearningPath` during a specific `Period`. Students are enrolled in *class instances* tied to those assignments.

| Pros | Cons |
|---|---|
| Matches booth-rental semantics — assignments expire, print rights are scoped, watermarking comes from the same record | Significantly more complex — requires Period entity to be in active use + a way to enroll a student into a teacher's class |
| Already exists in the schema (designed for exactly this) | Requires a class-instance entity that joins student × teacher × period × scheduled class |
| Supports a student who switches teachers next period | More migration work for existing data |

### Option C: Per-class — `ScheduledClass.TeacherId`

Add `TeacherId` to `ScheduledClass` directly. Every class instance knows its teacher.

| Pros | Cons |
|---|---|
| Local to the class — flexible for substitutes, shared students | Doesn't help with "all of Cece's students" queries — have to aggregate from ScheduledClass |
| Easy fallback if Option B is too heavy | Doesn't connect to print rights, watermarking, etc. |

### Recommendation: Hybrid — A + C

**Choose Option A (`ApplicationUser.TeacherId`) as the primary scoping primitive** for the "list teachers' students" use case, with **Option C (`ScheduledClass.TeacherId`) for the per-class flexibility** (substitutes, family-of-siblings rules, etc.).

Defer Option B (full `TeacherClassAssignment` + Period machinery) until LCS actually runs multi-period semesters with print-rights expiration. That's a Phase 4+ concern.

**Why this hybrid:**

- Option A gives the simple "who's Cece's student" query that 90% of the UI needs.
- Option C handles the edge cases without requiring the full Period framework.
- Option B's entity stays in the schema as a future capability — when we need it, we light it up. No migration churn now.

## Specific work items

### 1. Schema changes

**File:** `Models/Entities/ApplicationUser.cs`

```csharp
/// <summary>
/// For Student users: the teacher this student is assigned to (booth-rental
/// model — student belongs to one instructor under the LCS umbrella).
/// Null for legacy students or admin-pool students. For non-Student users,
/// always null.
/// </summary>
public string? TeacherId { get; set; }
public virtual ApplicationUser? Teacher { get; set; }
```

**File:** `Models/Entities/ScheduledClass.cs`

```csharp
/// <summary>
/// Teacher who taught (or will teach) this class. Defaults to the student's
/// assigned teacher but can be overridden for substitutes or special sessions.
/// </summary>
public string? TeacherId { get; set; }
public virtual ApplicationUser? Teacher { get; set; }
```

**Migration:** EF auto-generates the two `Add Column` migrations. Add a data backfill step that sets `TeacherId = <Karen's user id>` for all existing students (per-app migration via `MigrationBuilder.Sql(...)` or via SeedData logic — Allevo `BaseMigration` pattern).

### 2. `ICurrentTeacherService` for per-request scoping

Create a small request-scoped service that knows the current user's teacher context:

```csharp
public interface ICurrentTeacherService
{
    // Returns the teacher whose scope applies to the current request.
    // For Admin: null (see all).
    // For Teacher: their own ApplicationUser.Id.
    // For Student: their assigned TeacherId (so they see their teacher's stuff).
    string? GetScopingTeacherId();

    // True when the current user is an Admin who can ignore scoping.
    bool IsUnscoped();
}
```

Registered in `Program.cs` as `AddScoped<ICurrentTeacherService, CurrentTeacherService>()`.

### 3. Scope every controller / service query

This is the biggest piece. Every method that returns a list of students, classes, payments, etc. checks `ICurrentTeacherService` and adds a `Where` clause when scoped.

**Highest-impact controllers:**

- `AdminController.Students` — currently returns all students. Filter by teacher when teacher is calling.
- `AdminController.Schedule` — same.
- `AdminController.Payments` — same.
- `StudentController.Dashboard` — already user-scoped.
- `ScheduleService.GetUpcomingClassesAsync` and similar — accept an optional `scopingTeacherId` parameter.

**Pattern:**

```csharp
var query = _context.Students.AsQueryable();
var scopingTeacherId = _currentTeacher.GetScopingTeacherId();
if (scopingTeacherId is not null)
{
    query = query.Where(s => s.TeacherId == scopingTeacherId);
}
return await query.ToListAsync();
```

Apply consistently. ~15-20 query sites to update.

### 4. Teacher dashboard goes from generic to scoped

Once scoping is in, the Teacher dashboard surfaces:

- Cece's students count + list
- Cece's upcoming classes (next 7 days)
- Cece's revenue this month (after LCS cut) + payout pending
- Cece's drafts in the curriculum library

The dashboard view already has placeholders ("Your assigned classes and students for the current period," "Your monthly statements and payout history") — these become real.

### 5. Booking flow assigns teacher

When a student signs up or books a class, the platform must know which teacher they're with.

**Options:**

- **Karen-side assignment:** admin assigns new student to a teacher after signup.
- **Self-selection at signup:** new students pick from a list of LCS teachers during registration.
- **Inferred from booking:** student books a class at a particular time slot; the time slot is teacher-scoped (see #6); booking auto-sets `student.TeacherId` if currently null.

Recommendation: start with **Karen-side assignment** (simplest), evolve to self-selection when the team grows.

### 6. Teacher-scoped time slots

Currently `TimeSlot` is global. To support multiple instructors, `TimeSlot` needs `TeacherId`:

```csharp
public class TimeSlot
{
    // ... existing fields ...
    public string? TeacherId { get; set; }  // null = legacy / Karen's pool
    public virtual ApplicationUser? Teacher { get; set; }
}
```

Each teacher manages their own slots. Cece's "Tuesday 4pm" doesn't conflict with Karen's "Tuesday 4pm" — they're separate slots owned by different teachers.

The scheduling UI shows the student which slots are available with which teacher.

### 7. Revenue split on payment records

The booth-rental cut (LCS takes X%; teacher gets 100-X%) needs to be recorded per payment.

**File:** `Models/Entities/Payment.cs`

```csharp
public decimal LcsCutPercentage { get; set; }      // e.g. 20.00
public decimal LcsCutAmount { get; set; }          // computed at payment time
public decimal TeacherPayout { get; set; }         // Amount - LcsCutAmount
public string? TeacherId { get; set; }             // who gets the payout
```

Two splits to support:
- **80/20** for instructor-brought students
- **60/40** for LCS-sourced students (LCS did the marketing)

Logic: `student.AcquisitionSource = TeacherBrought | LcsSourced` (new enum on ApplicationUser), determines the split when a payment processes.

### 8. Admin override for everything

Karen as LLC owner needs a "see everything" mode even when scoping is on. Two design options:

- **Implicit:** Admin role bypasses scoping (Admin's `GetScopingTeacherId()` returns null).
- **Explicit toggle:** Admin sees their own teacher view by default but has a "View as: All teachers / Karen / Cece" dropdown in the nav.

Recommendation: **implicit** for v1 (simpler). Add the toggle later if Karen wants to literally see Cece's view.

### 9. Reporting / Analytics tab

Karen's analytics dashboard should break out:

- Revenue by teacher
- Lesson counts by teacher
- Student counts by teacher
- LCS cut earned vs teacher payouts

This is Karen's view of "how is each teacher's practice going" without exposing per-student details.

### 10. Tests

Add scoping tests for every list endpoint:

- `Teacher A logs in → AdminController.Students returns only Teacher A's students`
- `Teacher B logs in → does not see Teacher A's students`
- `Admin logs in → sees all students`
- Same pattern for scheduled classes, payments, etc.

15-20 new test cases.

## Migration of existing data

Once the migration to Hetzner is done and prod is fresh, this is simpler:

- All existing students (testing data) get `TeacherId = <Karen's user id>` via the SQL backfill step.
- All existing ScheduledClass rows same.
- All existing TimeSlots get assigned to Karen.

The default if anything's null is "Karen's pool."

## Open architectural questions

1. **Heritage speakers track.** If LCS adds a heritage-speaker track with different curriculum, does that need its own teacher-scope considerations? Probably no — it's a curriculum path question, not a teacher-scope question.
2. **Substitute teaching.** If Cece is sick and Karen teaches her students for one day, does the payment go to Cece (because the student is hers) or Karen (because she taught)? Booth-rental convention: payment to Cece, sub fee paid out separately. Tier 3 supports the basic case; subbing is a Phase 4 detail.
3. **Family discount handling.** A family of two siblings might be split across teachers (one kid likes Karen, one likes Cece). The discount math then gets weird. Defer until it actually happens.

## Definition of done

- Karen logs in → sees all students, all classes, all revenue, all teachers' practices.
- Cece logs in → sees only her students, her classes, her revenue.
- A new student signup either auto-assigns to a teacher (if Karen sets a default) or shows in an "unassigned" list Karen triages.
- Revenue split is recorded on every Payment row.
- Test suite has scoping coverage at ~15 new test cases.

## What this does NOT include (deferred to Phase 4+)

- Full `TeacherClassAssignment` + `Period` activation (Option B above)
- Print-rights expiration when a period ends
- Watermarking on binders drawing from `TeacherClassAssignment`
- Substitute-teacher flows
- Cross-teacher student transfer ("I want to switch from Karen to Cece next month")

All of those are valid future work, but Tier 3 ships without them.
