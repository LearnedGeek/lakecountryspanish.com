# Code Smells Playbook

> **Why this exists:** An AI assistant was asked to add sync functionality to a MAUI service. It added 5 new dependencies to a constructor that already had 5, creating a 10-parameter monster. It then added 200 lines of sync logic to the same service, mixing HTTP calls, local database writes, GPS tracking, and UI state management in a single method. The service "worked" but was untestable, unmaintainable, and hid three bugs that didn't surface until production. The root cause: nobody told the assistant what code smells to watch for, so it optimized for "does it compile" instead of "is it maintainable."

This playbook documents the code smells that matter most for our stack — ASP.NET Core, Blazor, .NET MAUI, EF Core. Each smell includes real examples from our projects, a detection pattern, a fix pattern, and severity rating. It's designed to be referenced by both human developers and AI assistants before writing or reviewing code.

---

## Table of Contents

1. [The Core Principle](#1-the-core-principle)
2. [God Class](#2-god-class)
3. [Long Parameter List](#3-long-parameter-list)
4. [Long Method](#4-long-method)
5. [God Component](#5-god-component)
6. [Magic Strings](#6-magic-strings)
7. [N+1 Queries](#7-n1-queries)
8. [Silent Exception Swallowing](#8-silent-exception-swallowing)
9. [Primitive Obsession](#9-primitive-obsession)
10. [Data Clumps](#10-data-clumps)
11. [Duplicate Constants](#11-duplicate-constants)
12. [Dead Code and TODO Graveyards](#12-dead-code-and-todo-graveyards)
13. [Speculative Generality](#13-speculative-generality)
14. [Feature Envy](#14-feature-envy)
15. [Middleman](#15-middleman)
16. [Framework Assumption Mismatch](#16-framework-assumption-mismatch)
17. [Severity Reference](#17-severity-reference)
18. [Quick Reference for AI Assistants](#18-quick-reference-for-ai-assistants)

---

## 1. The Core Principle

**Code smells are not bugs.** The code compiles. The tests pass. But the structure makes future bugs more likely, debugging harder, and changes riskier. A code smell is a signal that something *could* be better — not a mandate to refactor everything immediately.

**When to act:**
- You're already modifying the file → fix smells you touch
- A smell is causing real problems (test failures, debugging difficulty, merge conflicts) → fix it
- You're adding new code → don't introduce new smells

**When NOT to act:**
- The smell is in stable code you're not touching → leave it
- Fixing it requires a major refactor with no current benefit → document it, move on
- You're chasing perfection instead of shipping → stop

---

## 2. God Class

**Severity: HIGH** | **Impact: Testability, maintainability, merge conflicts**

A class that accumulates too many responsibilities. The clearest signal: you can't describe what the class does in one sentence without using "and."

### Detection

- Class exceeds ~300 lines (services) or ~200 lines (controllers)
- Constructor takes 5+ dependencies
- Class name is vague: `Manager`, `Helper`, `Service` (without a specific noun)
- Multiple unrelated methods in the same class

### Real Examples

**MAUI AuthService (695 lines, 5 dependencies):**
Handles login, logout, token storage, offline fallback, GPS startup, biometric prompts, and session persistence. That's at least 4 responsibilities.

**API SyncService (615 lines):**
Handles event processing, work order syncing, conflict detection, retry logic, and status updates.

### Fix Pattern

Extract responsibilities into focused collaborators:

```csharp
// BEFORE: AuthService does everything
public class AuthService : IAuthService
{
    // Login, logout, token refresh, offline auth, biometric auth,
    // GPS startup, session persistence... 695 lines
}

// AFTER: Each class has one job
public class AuthService : IAuthService           // Login, logout, token refresh
public class OfflineAuthService : IOfflineAuth     // Credential caching, offline validation
public class SessionService : ISessionService      // Persist/restore session state
public class BiometricService : IBiometricService  // Biometric prompt, device capability check
```

### Rule of Thumb

If a service needs more than 4 constructor dependencies, it's probably doing too much. Extract a collaborator.

---

## 3. Long Parameter List

**Severity: HIGH** | **Impact: Readability, testability, fragility**

Methods or constructors with more than 5-6 parameters are hard to call correctly, hard to test, and signal that the method is doing too much or the data should be grouped.

### Detection

- Constructor with 6+ injected dependencies
- Method with 5+ parameters
- You see the same group of parameters passed to multiple methods

### Real Example

```csharp
// A controller constructor with too many dependencies signals
// that the controller is handling too many concerns
public SyncController(
    ISyncService syncService,
    IWorkOrderService workOrderService,
    IConflictResolver conflictResolver,
    ILocationService locationService,
    IPhotoService photoService,
    ITimeEntryService timeEntryService,
    ILogger<SyncController> logger)
```

### Fix Patterns

**Option 1: Introduce a facade** — Group related dependencies behind a single interface.

```csharp
// Group related operations
public interface ISyncOrchestrator
{
    Task<SyncResult> ProcessEventsAsync(IEnumerable<SyncEvent> events);
    Task<SyncResult> PullChangesAsync(Guid userId, DateTime since);
}

public SyncController(ISyncOrchestrator orchestrator, ILogger<SyncController> logger)
```

**Option 2: Use a parameter object** — Replace primitive parameters with a grouped DTO.

```csharp
// BEFORE
Task CreateWorkOrder(string title, string description, Guid jobSiteId,
    DateTime scheduledDate, Guid assignedCrewId, Priority priority)

// AFTER
Task CreateWorkOrder(CreateWorkOrderRequest request)
```

---

## 4. Long Method

**Severity: HIGH** | **Impact: Readability, testability, bug density**

Methods over ~30 lines (excluding boilerplate) are doing too much. The longer a method, the higher the likelihood of hidden bugs, especially in error handling paths.

### Detection

- Method exceeds ~40 lines of logic
- Method has multiple levels of nesting (3+ indent levels)
- Method has multiple `try/catch` blocks
- You need to scroll to see the whole method

### Real Example

**MAUI AuthService.LoginAsync() — 78 lines:**
Handles API call, offline fallback, token storage, local session save, GPS startup, and biometric prompt logic. Each of those is a separate concern.

**CoachViewModel.SendMessageAsync() — 113 lines:**
Handles SSE stream parsing, JSON deserialization, and UI updates in a single method.

### Fix Pattern

Extract steps into well-named private methods. Each method should do one thing at one level of abstraction.

```csharp
// BEFORE: LoginAsync does everything
public async Task<bool> LoginAsync(string email, string password)
{
    // 78 lines: API call, offline fallback, token storage,
    // session save, GPS startup, biometric prompt...
}

// AFTER: LoginAsync orchestrates, each step is testable
public async Task<bool> LoginAsync(string email, string password)
{
    var response = await AuthenticateAsync(email, password);
    if (response is null)
        return await TryOfflineLoginAsync(email, password);

    await StoreTokensAsync(response);
    await SaveSessionAsync(response.User);
    await StartLocationTrackingIfEnabledAsync();
    return true;
}
```

### Rule of Thumb

If you can't name what the method does without using "and," it's too long.

---

## 5. God Component

**Severity: HIGH** | **Impact: Reusability, testability, load times**

A Blazor component (`.razor` file) that exceeds ~250 lines and mixes rendering, business logic, and data fetching. These become impossible to test with bUnit because the test setup requires mocking 7+ services.

### Detection

- Razor file exceeds 250 lines
- `@inject` count exceeds 5
- `@code` block has 15+ private fields
- Component handles CRUD, filtering, modals, and validation in one file

### Real Examples

| Component | Lines | Injected Services | Responsibilities |
|-----------|-------|-------------------|------------------|
| Support.razor | 646 | 7 | Health checks, log search, SignalR test, offline toggle, diagnostics |
| TaskCompletePage.razor | 610 | 6+ | Task completion, photo capture, GPS, notes, sync events |
| Crew.razor | 606 | 5+ | List, edit, delete, filter, sort, modal |
| Reports.razor | 553 | 5+ | Stats, date filter, export, charts |

### Fix Pattern

**Extract child components** for distinct UI sections. **Extract services** for business logic.

```
// BEFORE: One 600-line file
Support.razor (646 lines)
  └── health check UI + log viewer + SignalR tester + offline toggle + diagnostics

// AFTER: Composed from focused components
Support.razor (80 lines — layout and orchestration only)
  ├── HealthCheckPanel.razor (100 lines)
  ├── LogViewer.razor (120 lines)
  ├── SignalRTester.razor (80 lines)
  └── DiagnosticsPanel.razor (90 lines)
```

### MAUI Variant: God ViewModel

The same smell appears in .NET MAUI as oversized ViewModels. A ViewModel that handles navigation, data loading, form validation, and business logic in one file becomes untestable and fragile.

**Detection:**
- ViewModel exceeds 300 lines
- ViewModel injects 5+ services
- ViewModel handles multiple unrelated user actions (e.g., logging sets, managing timers, navigating, syncing)

**Fix Pattern:**

Extract sub-ViewModels for distinct UI sections:

```csharp
// BEFORE: ActiveSessionViewModel (450 lines)
// Handles exercise selection, set logging, rest timer, session completion,
// progressive overload defaults, notes, navigation... everything

// AFTER: Composed from focused ViewModels
ActiveSessionViewModel          // Orchestrates, owns session state
  ├── SetInputViewModel         // Weight, reps, RPE, RIR input + validation
  ├── RestTimerViewModel        // Countdown, skip, auto-start logic
  └── ExercisePickerViewModel   // Search, filter, selection
```

### Rule of Thumb

A component should have **one reason to re-render**. If clicking a filter button re-renders an unrelated chart, the component is doing too much. For MAUI ViewModels, if you can't describe the ViewModel's job without using "and," extract a sub-ViewModel.

---

## 6. Magic Strings

**Severity: MEDIUM** | **Impact: Refactoring safety, typo bugs, discoverability**

Hardcoded string literals scattered through the codebase. When a value changes, you have to find-and-replace across files and hope you didn't miss one.

### Detection

- Role names like `"Owner"`, `"Supervisor"`, `"Crew"` used inline
- API routes like `"/api/v1/sync/push"` duplicated across services and tests
- Config keys like `"Jwt:Key"` used as raw strings
- Entity type identifiers like `"WorkoutSession"` in switch statements

### Real Examples

**Role names scattered across projects:**
```csharp
// Program.cs
policy.RequireRole("Owner", "Supervisor")

// IAuthService.cs
public bool IsOwner => Role.Equals("Owner", StringComparison.OrdinalIgnoreCase);

// Multiple Razor components
if (authState.Role == "Owner")
```

**API routes duplicated:**
```csharp
// SyncService.cs
var response = await _httpClient.PostAsJsonAsync("/api/v1/sync/push", payload);

// SyncEndpointTests.cs
var response = await client.PostAsJsonAsync("/api/v1/sync/push", payload);
```

**Entity type strings repeated:**
```csharp
// SyncEndpoints.cs
case "WorkoutSession": ...
case "BodyMeasurement": ...

// SyncService.cs
EntityType = "WorkoutSession"
```

### Fix Pattern

Centralize in a constants class or use enums:

```csharp
// Roles — use a static class in the Shared project
public static class Roles
{
    public const string Owner = "Owner";
    public const string Supervisor = "Supervisor";
    public const string Crew = "Crew";
}

// Usage
policy.RequireRole(Roles.Owner, Roles.Supervisor);
public bool IsOwner => Role.Equals(Roles.Owner, StringComparison.OrdinalIgnoreCase);

// API routes — use a static class
public static class ApiRoutes
{
    public const string SyncPush = "/api/v1/sync/push";
    public const string SyncPull = "/api/v1/sync/pull";
}

// Entity types — use an enum
public enum SyncEntityType { WorkoutSession, BodyMeasurement }
```

---

## 7. N+1 Queries

**Severity: CRITICAL** | **Impact: Performance, database load, response times**

Loading a list of entities, then issuing a separate database query for each one in a loop. With 100 items, this becomes 101 queries instead of 1-2.

### Detection

- `await` inside a `foreach` loop that operates on a query result
- EF Core `.Find()` or `.FirstOrDefaultAsync()` called inside a loop
- Missing `.Include()` on navigation properties that are accessed later
- Response times that scale linearly with data volume

### Real Example

```csharp
// BAD: N+1 — one query per crew member
public async Task<IEnumerable<CrewLocationDto>> GetAllCrewLocationsAsync()
{
    var crewMembers = await _context.CrewMembers
        .Where(cm => cm.IsActive && cm.CurrentLatitude.HasValue)
        .ToListAsync();

    var result = new List<CrewLocationDto>();
    foreach (var cm in crewMembers)
    {
        if (cm.CurrentWorkOrderId.HasValue)
        {
            // This executes a query for EACH crew member
            var workOrder = await _context.WorkOrders
                .Include(w => w.JobSite)
                .FirstOrDefaultAsync(w => w.Id == cm.CurrentWorkOrderId.Value);
            jobSiteName = workOrder?.JobSite?.Name;
        }
        result.Add(MapToDto(cm, jobSiteName));
    }
    return result;
}
```

### Fix Pattern

Use `.Include()` or a single projected query:

```csharp
// GOOD: Single query with join
public async Task<IEnumerable<CrewLocationDto>> GetAllCrewLocationsAsync()
{
    return await _context.CrewMembers
        .Where(cm => cm.IsActive && cm.CurrentLatitude.HasValue)
        .Select(cm => new CrewLocationDto
        {
            CrewMemberId = cm.Id,
            Name = $"{cm.FirstName} {cm.LastName}",
            Latitude = cm.CurrentLatitude!.Value,
            Longitude = cm.CurrentLongitude!.Value,
            JobSiteName = cm.CurrentWorkOrder != null
                ? cm.CurrentWorkOrder.JobSite!.Name
                : null
        })
        .ToListAsync();
}
```

### Rule of Thumb

If you see `await` inside `foreach`, you probably have an N+1. Restructure to a single query with `.Include()` or `.Select()` projection.

---

## 8. Silent Exception Swallowing

**Severity: CRITICAL** | **Impact: Debugging, reliability, silent data loss**

Catching exceptions and returning null, false, or an empty result without logging. The system "works" until it doesn't, and you have zero diagnostic information.

### Detection

- Empty `catch` blocks
- `catch { return false; }` or `catch { return null; }`
- `catch (Exception)` with no logging
- `catch` blocks that only contain a comment

### Real Examples

```csharp
// CRITICAL: JWT validation silently fails — impossible to debug auth issues
catch
{
    return null;  // Which exception? Token expired? Invalid signature? Key mismatch?
}

// DANGEROUS: Login failure swallowed — user gets "Login failed" with no server trace
catch
{
    return false;
}

// MISLEADING: Comment doesn't replace logging
catch (JsonException)
{
    // Malformed SSE data line -- skip
}
```

### Fix Pattern

Always log, then decide whether to re-throw or return a safe default:

```csharp
// GOOD: Log the exception, return safe default
catch (SecurityTokenExpiredException ex)
{
    _logger.LogInformation("Token expired for user {UserId}: {Message}", userId, ex.Message);
    return null;
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "Unexpected error validating token");
    return null;
}
```

### Variant: Middleware That Hides Exceptions from Tests

Exception-handling middleware can create a subtler version of this smell: the middleware catches unhandled exceptions, returns a generic 500 response, and the actual exception is invisible to integration tests.

```csharp
// GlobalExceptionMiddleware returns a generic error
catch (Exception ex)
{
    _logger.LogError(ex, "Unhandled exception. SupportId={SupportId}", supportId);
    context.Response.StatusCode = 500;
    await context.Response.WriteAsync(JsonSerializer.Serialize(new
    {
        success = false,
        error = "An unexpected error occurred.",
        supportId
    }));
}

// Integration test sees: 500 + "An unexpected error occurred."
// But the REAL error was: DbUpdateConcurrencyException — EF generated
// UPDATE instead of INSERT because of a sentinel check issue.
// Without checking server logs, you'd never know the root cause.
```

**Fix:** In test environments, configure middleware to include exception details in the response, or inspect `ITestOutputHelper`/server logs alongside the HTTP response.

### Rule of Thumb

**If a catch block has zero lines of logging, it's a bug.** Every exception is diagnostic information you'll wish you had when debugging production. And if your integration tests can only see a generic error message, you're debugging blind.

---

## 9. Primitive Obsession

**Severity: MEDIUM** | **Impact: Validation gaps, API contract fragility**

Using primitive types (`string`, `double`, `decimal`) to represent domain concepts that have rules or constraints. The validation lives nowhere, so invalid values flow through the system unchecked.

### Detection

- `double latitude, double longitude` as separate method parameters
- `string email` without validation at the boundary
- `decimal quantity` that could be negative but shouldn't be
- The same primitive validation repeated in multiple places

### Real Examples

```csharp
// Latitude and longitude as raw doubles — no range validation
public async Task<ActionResult<object>> CheckGeofence(
    Guid jobSiteId,
    [FromQuery] double latitude,    // Could be 999.0 — no validation
    [FromQuery] double longitude)   // Could be -999.0 — no validation

// Quantity fields without constraints
public decimal QuantityLoaded { get; set; }     // Can be negative?
public decimal? QuantityDelivered { get; set; }  // Can exceed loaded?
public decimal? QuantityReturned { get; set; }   // No validation
```

### Fix Pattern

Introduce value objects for domain concepts with rules:

```csharp
// Value object enforces invariants at construction
public readonly record struct GpsCoordinate(double Latitude, double Longitude)
{
    public GpsCoordinate
    {
        if (Latitude is < -90 or > 90)
            throw new ArgumentOutOfRangeException(nameof(Latitude));
        if (Longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(Longitude));
    }
}

// Usage: impossible to pass invalid coordinates
public async Task<bool> IsWithinGeofenceAsync(GpsCoordinate location, Guid jobSiteId)
```

### When NOT to Fix

Don't create value objects for primitives that have no domain rules. A `string name` is fine — it doesn't need a `PersonName` type unless you have specific formatting/validation requirements.

---

## 10. Data Clumps

**Severity: MEDIUM** | **Impact: Duplication, missed encapsulation**

The same group of variables appearing together in multiple places — parameter lists, class fields, method calls. This is a value object waiting to be born.

### Detection

- `latitude`, `longitude`, `accuracy` appearing together in 3+ places
- `firstName`, `lastName`, `email` passed as a group to multiple methods
- Multiple DTOs with the same subset of fields

### Real Example

The latitude/longitude/accuracy trio appears across CrewTrack in:
- `SyncService` (event processing)
- `LocationService` (geofence checking)
- `ConflictResolver` (coordinate validation)
- Multiple DTOs: `LocationUpdateDto`, `CrewLocationDto`, `SyncEventDto`
- Entity fields: `CrewMember.CurrentLatitude`, `CrewMember.CurrentLongitude`, `CrewMember.CurrentLocationAccuracy`

### Fix Pattern

Extract into a value object and use it everywhere:

```csharp
public readonly record struct GpsLocation(
    double Latitude,
    double Longitude,
    double? AccuracyMeters = null);

// Note: CrewTrack already HAS a GpsLocation class —
// the smell is that it's not used consistently everywhere
```

---

## 11. Duplicate Constants

**Severity: MEDIUM** | **Impact: Drift, inconsistency, subtle bugs**

The same constant values defined in multiple places. When one copy changes and the other doesn't, behavior silently diverges.

### Detection

- `const string` with the same value in multiple files
- Magic numbers that represent the same thing in different places
- Event type strings defined in both publisher and consumer

### Real Example

```csharp
// SyncService.cs — defines event constants
private const string EVENT_TASK_COMPLETED = "TASK_COMPLETED";
private const string EVENT_TASK_STARTED = "TASK_STARTED";
private const string EVENT_TASK_SKIPPED = "TASK_SKIPPED";

// ConflictResolver.cs — defines the SAME constants again
private const string EVENT_TASK_COMPLETED = "TASK_COMPLETED";
private const string EVENT_TASK_STARTED = "TASK_STARTED";
private const string EVENT_TASK_SKIPPED = "TASK_SKIPPED";
```

**SSE parsing duplicated across layers:**
```csharp
// AnthropicChatService.cs (Infrastructure)
if (!line.StartsWith("data: ")) continue;
var data = line["data: ".Length..];

// CoachViewModel.cs (UI)
if (!line.StartsWith("data: ")) continue;
var data = line["data: ".Length..];
```

**Entity type strings repeated across sync layer:**
```csharp
// SyncEndpoints.cs
case "WorkoutSession": ...
case "BodyMeasurement": ...

// SyncService.cs
EntityType = "WorkoutSession",
ChangeType = "Create"

// SyncPushCommandHandler.cs
EntityType = "WorkoutSession",
ChangeType = "Update"
```

These strings should be an enum or constants class in the Shared project — a typo in any one of these causes silent sync failures.

### Fix Pattern

Single source of truth in the Shared project:

```csharp
// In Shared project — referenced by all layers
public static class SyncEventTypes
{
    public const string TaskCompleted = "TASK_COMPLETED";
    public const string TaskStarted = "TASK_STARTED";
    public const string TaskSkipped = "TASK_SKIPPED";
}
```

---

## 12. Dead Code and TODO Graveyards

**Severity: LOW** | **Impact: Confusion, false leads during debugging**

Commented-out code, unused methods, and `TODO` comments that have been sitting for weeks. Dead code confuses new developers and AI assistants who can't tell if it's intentional or abandoned.

### Detection

- `// TODO:` comments older than 2 weeks
- Methods with zero callers
- `#if false` blocks
- Commented-out code blocks

### Real Examples

```csharp
// TODO that's been there for weeks — is anyone going to do this?
ThumbnailUrl = null, // TODO: Implement thumbnail generation

// Stub that returns hardcoded false — is this feature coming or abandoned?
// TODO: Implement full biometric detection using AndroidX.Biometric package
// For now, return false to indicate biometrics are not yet supported
await Task.CompletedTask;
return false;

// Incomplete feature with no tracking
DeletedWorkOrderIds = new List<Guid>() // TODO: Track deleted work orders
```

### Fix Pattern

**Option 1:** Do the TODO → implement it and delete the comment.
**Option 2:** Track it → create a GitHub issue and reference it in the comment: `// TODO(#123): Implement thumbnail generation`
**Option 3:** Delete it → if nobody's touched it in a month, the feature isn't happening. Remove the dead code.

### Rule of Thumb

A `TODO` without a linked issue is a wish, not a plan. Either link it or delete it.

---

## 13. Speculative Generality

**Severity: LOW** | **Impact: Complexity, cognitive load**

Code written for hypothetical future requirements that don't exist yet. Abstract factories for one implementation, config options nobody uses, extension points with zero extensions.

### Detection

- Interface with exactly one implementation and no plans for a second
- Generic type parameters that are always the same concrete type
- Config options with only one valid value
- "Strategy" or "Factory" patterns with a single strategy/product

### Fix Pattern

Delete it. When you need the abstraction, you'll know — and the actual requirements will inform a better design than your guess today.

```csharp
// BEFORE: Speculative
public interface INotificationStrategy { }
public class EmailNotificationStrategy : INotificationStrategy { }
public class NotificationStrategyFactory
{
    public INotificationStrategy Create(string type) =>
        type switch { "email" => new EmailNotificationStrategy(), _ => throw ... };
}

// AFTER: Just use the class directly until you need a second implementation
public class EmailNotifier { ... }
```

### Important Distinction: Convention vs. Speculation

If the project convention is **interface-per-service** (e.g., `IRepository<T>` + `Repository<T>`, `INavigationService` + `NavigationService`), follow it — even when there's only one implementation today. These interfaces serve testability (mocking) and Clean Architecture layer boundaries, not speculative second implementations.

This smell applies to **gratuitous abstractions beyond that convention**: factory patterns for one product, strategy patterns for one strategy, or generic type parameters that are always the same concrete type. The test is: "Does this abstraction serve a current need (testability, DI, layer isolation), or am I guessing at a future requirement?"

---

## 14. Feature Envy

**Severity: MEDIUM** | **Impact: Misplaced logic, fragile coupling**

A method that spends more time accessing another object's data than its own. The logic belongs on the object it's interrogating.

### Detection

- Method chains like `order.Customer.Address.City` used in calculations
- A service method that pulls 5+ properties from an entity to compute something
- Logic about an entity that lives in a controller instead of the entity itself

### Fix Pattern

Move the logic to the object that owns the data:

```csharp
// BEFORE: Controller computes material discrepancy
var discrepancy = material.QuantityLoaded
    - (material.QuantityDelivered ?? 0)
    - (material.QuantityReturned ?? 0);
var hasIssue = Math.Abs(discrepancy) > 0.01m;

// AFTER: Entity owns its own business rule
// (CrewTrack already does this correctly with Material.Discrepancy)
public decimal Discrepancy =>
    QuantityLoaded - (QuantityDelivered ?? 0) - (QuantityReturned ?? 0);
public bool HasDiscrepancy => Math.Abs(Discrepancy) > 0.01m;
```

---

## 15. Middleman

**Severity: LOW** | **Impact: Indirection, complexity without value**

A class that exists solely to delegate to another class. Every method just calls the same method on another object with no additional logic.

### Detection

- Class where every method is a one-liner calling another service
- "Wrapper" classes that add no behavior
- Service that just forwards to a repository with no business logic

### Fix Pattern

Remove the middleman. Let the caller talk directly to the dependency.

```csharp
// BEFORE: Middleman adds nothing
public class UserService
{
    private readonly IUserRepository _repo;
    public Task<User> GetByIdAsync(int id) => _repo.GetByIdAsync(id);
    public Task<List<User>> GetAllAsync() => _repo.GetAllAsync();
    public Task CreateAsync(User user) => _repo.CreateAsync(user);
}

// AFTER: Controller injects repository directly
// (Only add a service layer when there's actual business logic to encapsulate)
```

### When to Keep the Middleman

If the service **will** have business logic (validation, authorization checks, event publishing) — even if it's thin now — the abstraction layer is justified. Use judgment.

---

## 16. Framework Assumption Mismatch

**Severity: HIGH** | **Impact: Silent data corruption, wrong SQL, hours of debugging**

Code that works against the framework's conventions or assumptions instead of with them. The code compiles, may even pass simple tests, but produces wrong behavior at runtime because the framework's internal heuristics don't match how the code is structured.

### Detection

- Framework generates wrong SQL (UPDATE instead of INSERT, or vice versa)
- Runtime exceptions from the ORM that don't correspond to your logic
- Behavior changes when you add/remove `.AsNoTracking()`, change key types, or reorder operations
- "It works if I do X first" — a sign that ordering matters to the framework in ways your code doesn't account for

### Real Example: EF Core Sentinel Check + Client-Generated Guids

EF Core uses a "sentinel check" to determine if an entity is new (needs INSERT) or existing (needs UPDATE). For Guid keys, the sentinel is `Guid.Empty` — if the key is empty, the entity is new; if non-empty, it's existing.

```csharp
// BaseEntity generates a Guid at construction — ALWAYS non-empty
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}
```

When entities are added through navigation properties (not `DbSet.AddAsync`), EF discovers them via `DetectChanges` and applies the sentinel check. Since `Id` is never `Guid.Empty`, EF treats every new entity as existing:

```csharp
// Handler adds a new ExerciseSet through a navigation property
sessionExercise.Sets.Add(new ExerciseSet { /* ... */ });
await _sessions.SaveChangesAsync();

// EF sees: ExerciseSet.Id = 7a3f... (non-empty) → must be existing → UPDATE
// Database: no row with that Id → "Expected to affect 1 row(s), but actually affected 0 row(s)"
// Result: DbUpdateConcurrencyException
```

### Why This Is Insidious

The code looks correct. The entity is genuinely new. The exception message ("concurrency exception") is misleading — there's no actual concurrency conflict. And the "fix" that AI assistants gravitate toward (adding explicit `AddAsync` calls, removing tracking, or adding dedicated repository methods per entity type) treats the symptom, not the cause.

### Fix Pattern

Explicitly tell EF the entity is new when the sentinel check can't determine it:

```csharp
// Repository exposes a generic "mark as new" method
public void MarkAsNew(BaseEntity entity)
    => _context.Entry(entity).State = EntityState.Added;

// Handler uses it after adding through navigation property
sessionExercise.Sets.Add(set);
_sessions.MarkAsNew(set);
```

**Long-term fix:** Let EF generate Guids server-side (remove `= Guid.NewGuid()` from `BaseEntity` and configure `ValueGeneratedOnAdd`). This is a larger migration but eliminates the mismatch entirely.

### Other Common Mismatches

| Framework | Assumption | Common Mismatch |
|-----------|-----------|-----------------|
| EF Core | Tracked entities get change detection | Using `AsNoTracking()` on queries that feed write handlers |
| EF Core | Empty FK = new relationship | Client-generated Guids are never empty |
| MAUI | `Shell.Current` available from ViewModels | Background threads or unit tests have no Shell |
| ASP.NET Core | Middleware sees all exceptions | Global exception middleware hides root causes from tests |

### Rule of Thumb

When the framework produces unexpected behavior, **check if your code violates the framework's assumptions** before adding workarounds. Read the framework's source or docs on key generation, change tracking, or lifecycle hooks. A 5-minute investigation beats a 5-hour debugging session caused by a workaround that masks the real issue.

---

## 17. Severity Reference

| Severity | Meaning | Action |
|----------|---------|--------|
| **CRITICAL** | Active performance or reliability risk. Fix on contact. | Fix immediately when you touch the file |
| **HIGH** | Significant maintainability drag. Causes real friction. | Fix when modifying the file, or create a tracking issue |
| **MEDIUM** | Quality improvement. Reduces future bug surface. | Fix opportunistically, don't make a special trip |
| **LOW** | Cleanliness. Nice to fix, not urgent. | Fix only if you're already refactoring the area |

### Summary by Smell

| # | Smell | Severity | Primary Risk |
|---|-------|----------|-------------|
| 2 | God Class | HIGH | Untestable, merge conflicts |
| 3 | Long Parameter List | HIGH | Readability, fragility |
| 4 | Long Method | HIGH | Hidden bugs, untestable |
| 5 | God Component | HIGH | Untestable, performance |
| 6 | Magic Strings | MEDIUM | Typo bugs, refactoring |
| 7 | N+1 Queries | CRITICAL | Performance, database load |
| 8 | Silent Exception Swallowing | CRITICAL | Silent failures, no diagnostics |
| 9 | Primitive Obsession | MEDIUM | Validation gaps |
| 10 | Data Clumps | MEDIUM | Duplication |
| 11 | Duplicate Constants | MEDIUM | Drift, inconsistency |
| 12 | Dead Code / TODOs | LOW | Confusion |
| 13 | Speculative Generality | LOW | Unnecessary complexity |
| 14 | Feature Envy | MEDIUM | Misplaced logic |
| 15 | Middleman | LOW | Pointless indirection |
| 16 | Framework Assumption Mismatch | HIGH | Wrong SQL, silent corruption |

---

## 18. Quick Reference for AI Assistants

**Read this section before writing or reviewing any code for this project.**

### Before You Write Code

1. **Check constructor parameter count.** If it's already at 4-5, don't add another — extract a collaborator instead.
2. **Check file length.** If the file is already 300+ lines, don't add more — extract a new class or component.
3. **Check method length.** If your new method will exceed 30 lines of logic, split it before writing.

### While You Write Code

4. **Never use magic strings.** Check if a constant class already exists. If not, create one in the Shared project.
5. **Never swallow exceptions silently.** Every `catch` block must log something. Use `_logger.LogWarning(ex, "context")` at minimum.
6. **Never query inside a loop.** If you need data for each item in a collection, use `.Include()` or a single projected query.
7. **Never pass the same 3+ parameters together.** Create a parameter object or value object.

### After You Write Code

8. **Count your dependencies.** If a class needs 6+ constructor parameters, refactor before committing.
9. **Count your lines.** If a method exceeds 40 lines, extract helper methods.
10. **Search for your string literals.** If a string appears in more than one file, extract to a constant.
11. **Check framework assumptions.** If you're adding entities through navigation properties with client-generated Guids, use `MarkAsNew`. If a query feeds a write handler, don't use `AsNoTracking`. When the framework misbehaves, investigate its conventions before adding workarounds.

### Red Flags That Demand Immediate Action

| Red Flag | Smell | Action |
|----------|-------|--------|
| `await` inside `foreach` | N+1 Query | Restructure to single query |
| `catch { return null; }` | Silent Swallowing | Add logging |
| Constructor with 6+ params | God Class | Extract collaborator |
| Razor file > 400 lines | God Component | Extract child components |
| Same constant in 2+ files | Duplicate Constants | Move to Shared constants class |
| EF generates UPDATE for new entity | Framework Mismatch | Check sentinel values, use `MarkAsNew` |
| `AsNoTracking` on write-path query | Framework Mismatch | Remove it — tracked entities need change detection |

---

## References

- [TESTING-STRATEGY.md](TESTING-STRATEGY.md) — Testing patterns and anti-patterns
- [HARDENING.md](../HARDENING.md) — Production hardening playbook
- [ARCHITECTURE_PATTERNS.md](ARCHITECTURE_PATTERNS.md) — Architecture invariants and conventions
- [Legitimate Security - Code Smells](https://www.legitsecurity.com/aspm-knowledge-base/code-smells) — General code smell taxonomy

---

*Every example in this guide was found in a real codebase. The smells weren't introduced by bad developers — they accumulated through feature pressure, AI-assisted code generation, and the natural entropy of iterative development. The purpose of this guide isn't to shame past decisions; it's to give every contributor (human and AI) a shared vocabulary and clear thresholds for action.*
