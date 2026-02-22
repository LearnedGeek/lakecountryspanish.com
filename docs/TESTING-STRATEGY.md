# Testing Strategy Playbook

> **Why this exists:** An AI assistant was asked to improve test coverage on a Blazor/MAUI project. It implemented shell-based database testing with `sqlcmd` on the CI runner (which only existed inside the container), then "fixed" it by calling `Database.Migrate()` directly (which didn't account for schema creation), then each subsequent fix created a new problem. The root cause: the assistant didn't understand the established test infrastructure before proposing changes. This playbook documents the testing patterns that work, the anti-patterns that don't, and the principles that prevent AI-assisted testing from going off the rails.

---

## Table of Contents

1. [The Core Principle](#1-the-core-principle)
2. [Anti-Patterns — What NOT To Do](#2-anti-patterns--what-not-to-do)
3. [Test Architecture — The Three Layers](#3-test-architecture--the-three-layers)
4. [Layer 1: Unit Tests](#4-layer-1-unit-tests)
5. [Layer 2: Integration Tests](#5-layer-2-integration-tests)
6. [Layer 3: E2E / Smoke Tests](#6-layer-3-e2e--smoke-tests)
7. [The IntegrationTestBase Pattern](#7-the-integrationtestbase-pattern)
8. [The TestDbContextFactory Pattern](#8-the-testdbcontextfactory-pattern)
9. [The Robot Client Pattern (Spec-Driven E2E)](#9-the-robot-client-pattern-spec-driven-e2e)
10. [Blazor Component Testing with bUnit](#10-blazor-component-testing-with-bunit)
11. [Playwright + TestServer (Browser E2E)](#11-playwright--testserver-browser-e2e)
12. [Auth Testing Patterns](#12-auth-testing-patterns)
13. [Middleware Control in Tests](#13-middleware-control-in-tests)
14. [Database Strategies for Tests](#14-database-strategies-for-tests)
15. [CI Pipeline Testing](#15-ci-pipeline-testing)
16. [Coverage Gap Prioritization](#16-coverage-gap-prioritization)
17. [Applying This to a Blazor/MAUI Stack](#17-applying-this-to-a-blazormaui-stack)
18. [Quick Reference for AI Assistants](#18-quick-reference-for-ai-assistants)

---

## 1. The Core Principle

**Understand the existing test infrastructure before writing a single test.**

Every project has established patterns — base classes, fixtures, factories, CI workflows. New tests must follow these patterns. If you don't understand them, you'll reinvent the wheel badly.

Before proposing any test changes, answer these questions:

1. **What test framework is in use?** (xUnit, NUnit, MSTest)
2. **What test infrastructure exists?** (base classes, fixtures, factories)
3. **How is the database handled in tests?** (InMemory, SQLite, LocalDB, Respawn)
4. **How is auth handled in tests?** (test JWT helpers, mock auth, cookie injection)
5. **What does the CI pipeline expect?** (coverage format, test loggers, container services)
6. **What middleware needs to be disabled?** (rate limiting, CORS, caching)

If you can't answer all six, read the existing test code first. Don't guess.

---

## 2. Anti-Patterns — What NOT To Do

These patterns were all attempted by AI assistants and caused cascading failures. Each one sounds reasonable in isolation but breaks something downstream.

### Shell-Based API Testing

```bash
# BAD: Using curl/shell scripts to test authenticated API endpoints in CI
curl -X POST https://localhost:5001/api/auth/login -d '{"email":"test@test.com","password":"test123"}'
```

**Why it fails:**
- Requires the app to be running on a real port (TestServer doesn't expose ports)
- Auth tokens, cookies, and CSRF tokens are hard to manage in shell scripts
- No assertions beyond status codes — you're testing "does it not crash," not "does it work"
- Fragile: depends on seed data, port availability, timing

**What to do instead:** Use `WebApplicationFactory<Program>` + `HttpClient` from xUnit. The test framework handles all of this.

### Direct Database Manipulation in CI

```bash
# BAD: Using sqlcmd or direct SQL in CI to set up test data
sqlcmd -S localhost -d TestDb -Q "INSERT INTO Players VALUES (...)"
```

**Why it fails:**
- `sqlcmd` may not be available on the CI runner (only inside the SQL container)
- Schema may not match — migrations might not have run
- Bypasses EF Core's change tracking and validation
- Tightly couples tests to database schema (column renames break tests)

**What to do instead:** Use EF Core's `DbContext` to seed data within the test fixture. The test infrastructure handles migrations and cleanup.

### Calling `Database.Migrate()` Outside the Test Framework

```csharp
// BAD: Raw migration call without considering schema, connection, or environment
using var context = new AppDbContext(options);
await context.Database.MigrateAsync(); // Which connection? Which schema? What if it's already migrated?
```

**Why it fails:**
- Doesn't account for schema-based environment separation
- May target the wrong database
- May conflict with migrations that already ran
- No cleanup/rollback strategy

**What to do instead:** Use the established fixture (e.g., `SqlLocalDbFixture`) which handles migrations, schema selection, and cleanup via Respawn.

### Writing Tests That Require External Services

```csharp
// BAD: Test that calls a real external API
[Fact]
public async Task StripeWebhook_CreatesSubscription()
{
    var client = new StripeClient("sk_live_..."); // Real Stripe!
    // ...
}
```

**Why it fails:**
- Flaky: depends on network, API availability, rate limits
- Slow: network round-trips add seconds per test
- Dangerous: may create real charges, send real emails
- Non-deterministic: external state changes between runs

**What to do instead:** Mock external services. Use `Moq` for service interfaces. For webhook testing, construct the webhook payload yourself and POST it to your endpoint.

### "Fixing" Test Infrastructure to Work Around Symptoms

```
Attempt 1: Add sqlcmd step to CI → fails (tool not on runner)
Attempt 2: Remove sqlcmd, use Database.Migrate() → fails (wrong schema)
Attempt 3: Skip schema, use default → fails (tables don't exist)
Attempt 4: Add CREATE TABLE statements → fails (schema drift)
```

**Why it fails:** Each "fix" addresses a symptom, not the root cause. The root cause is always: **the test isn't using the established test infrastructure.**

**What to do instead:** Find the working test fixture. Read it. Use it. If it doesn't support your scenario, extend it — don't bypass it.

---

## 3. Test Architecture — The Three Layers

Every test should be in exactly one of these layers. If you're not sure which layer a test belongs in, use this decision tree:

```
Does it test pure business logic with no I/O?
  → YES: Unit Test (Layer 1)
  → NO: Does it need an HTTP endpoint or database?
    → YES: Does it need a real browser?
      → YES: E2E Test (Layer 3)
      → NO: Integration Test (Layer 2)
```

| Layer | Speed | Scope | Database | HTTP | Browser |
|-------|-------|-------|----------|------|---------|
| **Unit** | ms | Single method/class | No (or in-memory) | No | No |
| **Integration** | 100ms | Endpoint + service + DB | In-memory or LocalDB | TestServer | No |
| **E2E / Smoke** | seconds | Full stack | Real (or seeded) | Real TCP | Yes (Playwright) |

**Target ratio:** ~60% unit, ~30% integration, ~10% E2E. E2E tests are expensive to write and maintain — use them for critical user flows only.

---

## 4. Layer 1: Unit Tests

**Purpose:** Test business logic in isolation. No HTTP, no database (or in-memory only).

### When to Write Unit Tests

- Business rules and calculations
- Validation logic
- Data transformations (DTO mapping, formatting)
- State machines
- Pure functions
- Service methods with mockable dependencies

### Pattern

```csharp
public class BattleEngineTests
{
    [Fact]
    public void CalculateDamage_CriticalHit_DoublesBaseDamage()
    {
        var engine = new BattleEngine();
        var attacker = new Unit { Attack = 50 };
        var defender = new Unit { Defense = 20 };

        var damage = engine.CalculateDamage(attacker, defender, isCritical: true);

        Assert.Equal(60, damage); // (50 - 20) * 2 = 60
    }
}
```

### Unit Tests with In-Memory DB

When a service method touches the database but you want fast, isolated tests:

```csharp
[Fact]
public async Task CreateGuild_FreeTierPlayer_ThrowsInvalidOperation()
{
    using var context = TestDbContextFactory.Create();
    var player = TestDbContextFactory.CreatePlayer(context, tier: SubscriptionTier.Free);
    var service = new GuildService(context, Mock.Of<ILogger<GuildService>>());

    var ex = await Assert.ThrowsAsync<InvalidOperationException>(
        () => service.CreateGuildAsync(player.Id, "Guild", "TAG", "desc"));

    Assert.Contains("Premium", ex.Message);
}
```

### Mocking External Dependencies

```csharp
[Fact]
public async Task SendNotification_EmailServiceDown_LogsWarningAndContinues()
{
    var mockEmail = new Mock<IEmailService>();
    mockEmail.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
        .ThrowsAsync(new SmtpException("Connection refused"));

    var mockLogger = new Mock<ILogger<NotificationService>>();
    var service = new NotificationService(mockEmail.Object, mockLogger.Object);

    await service.NotifyPlayerAsync(playerId, "Test"); // Should not throw

    mockLogger.Verify(l => l.Log(
        LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
        It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.Once);
}
```

---

## 5. Layer 2: Integration Tests

**Purpose:** Test the full request pipeline — HTTP endpoint → middleware → controller → service → database → response. Catches contract violations, auth issues, and data flow bugs.

### When to Write Integration Tests

- API endpoint correctness (status codes, response shapes)
- Authentication and authorization flows
- Database CRUD through the API
- Multi-service workflows (register → login → perform action)
- Error response formatting
- Pagination, filtering, sorting

### Pattern

```csharp
public class AuthTests : IntegrationTestBase
{
    public AuthTests(WebApplicationFactory<Program> factory)
        : base(factory, "Auth") { }

    [Fact]
    public async Task Register_ValidCredentials_Returns201WithToken()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            username = $"test_{Guid.NewGuid():N}",
            email = $"test_{Guid.NewGuid():N}@test.com",
            password = "SecurePass123!"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("token", out var token));
        Assert.False(string.IsNullOrEmpty(token.GetString()));
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var (client, _) = await RegisterPlayer();
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "registered@test.com",
            password = "WrongPassword!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }
}
```

### Key Principles

1. **Use anonymous objects for requests** (`new { username = "..." }`), not internal DTOs. This catches serialization/contract issues.
2. **Use `JsonElement` for responses**, not strongly-typed deserialization. This simulates what an external client sees.
3. **One logical assertion per test.** Test one behavior, not the entire API.
4. **Inherit from `IntegrationTestBase`** — don't reinvent factory setup.

---

## 6. Layer 3: E2E / Smoke Tests

**Purpose:** Verify that the full application works from a user's perspective — browser rendering, JavaScript execution, page navigation, form submission.

### When to Write E2E Tests

- Critical user flows (login, registration, primary feature)
- Public pages render correctly
- JavaScript-dependent functionality
- Cross-browser compatibility
- Visual regression (screenshot comparison)

### When NOT to Write E2E Tests

- Testing individual API endpoints (use integration tests)
- Testing business logic (use unit tests)
- Testing every edge case (too slow, too fragile)
- Testing things that aren't visible to users

### CI Smoke Tests — The Right Way

The lightweight check that your binary starts and serves requests:

```yaml
# CI: Verify the app starts and serves the OpenAPI spec
- name: Smoke Test - API starts
  run: |
    dotnet run --project src/MyApp.Api &
    sleep 10
    curl --fail http://localhost:5000/health
    curl --fail http://localhost:5000/swagger/v1/swagger.json
    kill %1
```

This is acceptable because:
- It tests the **binary** (compiled output), not business logic
- No database setup, no auth, no shell-based assertions
- It catches deployment-breaking issues (missing DLLs, bad config, startup exceptions)

What's NOT acceptable: using `curl` to test authenticated endpoints, database operations, or business flows. That's what the test framework is for.

---

## 7. The IntegrationTestBase Pattern

This is the **most important pattern** in the playbook. Every integration test inherits from this base class.

### Implementation

```csharp
public abstract class IntegrationTestBase : IClassFixture<WebApplicationFactory<Program>>
{
    protected readonly WebApplicationFactory<Program> Factory;

    protected IntegrationTestBase(WebApplicationFactory<Program> factory, string dbPrefix)
    {
        IntegrationTestSetup.DisableRateLimiting();

        var dbName = $"TestDb_{dbPrefix}_{Guid.NewGuid()}";

        Factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove production DB
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                // Add isolated in-memory DB
                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase(dbName)
                        .ConfigureWarnings(w =>
                            w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
            });
        });
    }

    protected HttpClient CreateClient() => Factory.CreateClient();

    protected async Task<(HttpClient Client, JsonElement Auth)> RegisterPlayer(
        string? username = null, string? email = null)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            username = username ?? $"player_{Guid.NewGuid():N}",
            email = email ?? $"player_{Guid.NewGuid():N}@test.com",
            password = "TestPass123!"
        });

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = auth.GetProperty("token").GetString()!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return (client, auth);
    }

    protected async Task<(HttpClient Client, JsonElement Auth)> CreateAuthenticatedClient()
        => await RegisterPlayer();
}
```

### Why It Works

| Design Decision | Why |
|---|---|
| `IClassFixture<WebApplicationFactory<Program>>` | One factory per test class — xUnit manages lifecycle |
| `$"TestDb_{dbPrefix}_{Guid.NewGuid()}"` | Unique DB per test class — no cross-contamination |
| `WithWebHostBuilder` to swap DB | Proper DI override — not hacky reflection |
| `InMemoryEventId.TransactionIgnoredWarning` ignored | EF Core in-memory doesn't support transactions — suppress the noise |
| `RegisterPlayer()` returns `(HttpClient, JsonElement)` | One-liner test setup with ready-to-use authenticated client |
| `DisableRateLimiting()` called first | Middleware doesn't interfere with test assertions |

### Alternative: SqlLocalDbFixture (Real Database)

When you need real SQL behavior (transactions, constraints, triggers):

```csharp
public class SqlLocalDbFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private string _dbName = null!;
    private Respawner _respawner = null!;

    public async Task InitializeAsync()
    {
        _dbName = $"TestDb_{Guid.NewGuid()}";
        // Create real LocalDB database, run migrations
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();

        // Configure Respawn for fast cleanup
        _respawner = await Respawner.CreateAsync(connectionString,
            new RespawnerOptions
            {
                TablesToIgnore = new[] { "__EFMigrationsHistory" }
            });
    }

    public async Task ResetDatabaseAsync()
    {
        await _respawner.ResetAsync(connectionString);
    }

    public async Task DisposeAsync()
    {
        // Drop test database
    }
}
```

**Use InMemory when:** tests are fast, logic-focused, and don't need real SQL features.
**Use LocalDB/Respawn when:** tests need transactions, constraints, raw SQL, or schema validation.

---

## 8. The TestDbContextFactory Pattern

For unit tests that need a database context but don't need HTTP:

```csharp
public static class TestDbContextFactory
{
    public static AppDbContext Create(string? dbName = null)
    {
        dbName ??= $"TestDb_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    // Domain-specific builders
    public static Player CreatePlayer(AppDbContext context,
        string username = "testplayer",
        string email = "test@test.com",
        SubscriptionTier tier = SubscriptionTier.Free)
    {
        var player = new Player
        {
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPass123!"),
            Tier = tier,
            CreatedAt = DateTime.UtcNow
        };
        context.Players.Add(player);
        context.SaveChanges();
        return player;
    }
}
```

**Usage:**

```csharp
[Fact]
public async Task CalculateLeaderboard_ReturnsTopPlayers()
{
    using var context = TestDbContextFactory.Create();
    var player1 = TestDbContextFactory.CreatePlayer(context, "Alice", rating: 1500);
    var player2 = TestDbContextFactory.CreatePlayer(context, "Bob", rating: 1200);

    var service = new LeaderboardService(context);
    var result = await service.GetTopPlayersAsync(10);

    Assert.Equal("Alice", result.First().Username);
}
```

---

## 9. The Robot Client Pattern (Spec-Driven E2E)

This pattern catches the exact class of bug that broke the Android demo: the client and server disagree about the API contract.

### How It Works

A "robot client" navigates your API by reading the OpenAPI spec and following HATEOAS links, using **no internal DTOs**. It sees your API exactly as an external client does.

```csharp
public class RobotClientTests : IntegrationTestBase
{
    [Fact]
    public async Task RobotClient_FullJourney_AllLinksValid()
    {
        var client = CreateClient();

        // Load the OpenAPI spec
        var specResponse = await client.GetAsync("/openapi/v1.json");
        var spec = await specResponse.Content.ReadFromJsonAsync<JsonElement>();

        // Register using raw JSON (no C# DTOs)
        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            username = $"robot_{Guid.NewGuid():N}",
            email = $"robot_{Guid.NewGuid():N}@test.com",
            password = "RobotPass123!"
        });
        var body = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();

        // Follow every _link in the response
        if (body.TryGetProperty("_links", out var links))
        {
            foreach (var link in links.EnumerateObject())
            {
                var href = link.Value.GetProperty("href").GetString()!;
                var method = link.Value.GetProperty("method").GetString()!;

                if (method == "GET")
                {
                    var linkResponse = await client.GetAsync(href);
                    Assert.True(linkResponse.IsSuccessStatusCode,
                        $"Link '{link.Name}' at {href} returned {linkResponse.StatusCode}");
                }
            }
        }
    }
}
```

### What It Catches

| Bug | How Robot Client Catches It |
|---|---|
| Renamed endpoint | Link returns 404 |
| Changed response shape | `GetProperty()` throws on missing field |
| Auth scheme mismatch | Link returns 401 |
| Broken HATEOAS link | Link target doesn't exist |
| Enum value changed | Spec validation fails |

### When to Use It

- You expose a REST API that external clients consume
- You have multiple client apps (web, mobile, third-party)
- You use HATEOAS or hypermedia links
- You've been burned by "the API changed and the client didn't know"

---

## 10. Blazor Component Testing with bUnit

For projects using Blazor (WebAssembly, Server, or MAUI Hybrid), bUnit is the standard for component testing.

### Setup

```xml
<!-- In your test .csproj -->
<PackageReference Include="bunit" Version="2.*" />
<PackageReference Include="Moq" Version="4.*" />
```

### Testing a Simple Component

```csharp
public class CounterTests : TestContext
{
    [Fact]
    public void Counter_ClickButton_IncrementsCount()
    {
        var cut = RenderComponent<Counter>();

        cut.Find("button").Click();

        cut.Find("p").MarkupMatches("<p>Current count: 1</p>");
    }
}
```

### Testing a Component with Service Dependencies

```csharp
public class DashboardTests : TestContext
{
    [Fact]
    public void Dashboard_LoadsData_DisplaysCrewCount()
    {
        var mockService = new Mock<ICrewService>();
        mockService.Setup(s => s.GetCrewCountAsync())
            .ReturnsAsync(42);

        Services.AddSingleton(mockService.Object);

        var cut = RenderComponent<Dashboard>();

        cut.Find("[data-testid='crew-count']").TextContent
            .MarkupMatches("42");
    }
}
```

### Testing a Component with Cascading Parameters

```csharp
public class ProtectedPageTests : TestContext
{
    [Fact]
    public void ProtectedPage_Unauthenticated_ShowsLoginPrompt()
    {
        var authState = Task.FromResult(
            new AuthenticationState(new ClaimsPrincipal()));

        Services.AddSingleton<AuthenticationStateProvider>(
            new FakeAuthStateProvider(authState));

        var cut = RenderComponent<ProtectedPage>();

        cut.Find("[data-testid='login-prompt']").ShouldNotBeNull();
    }
}
```

### What to Test with bUnit

| Test | Example |
|---|---|
| Rendering | Component shows correct data |
| Interaction | Button click triggers expected behavior |
| Conditional display | Admin panel hidden for non-admin users |
| Form validation | Invalid input shows error message |
| Service integration | Component calls service and displays result |
| Error states | Component handles service failure gracefully |

### What NOT to Test with bUnit

- CSS styling (use visual regression tools if needed)
- Third-party component internals (test your usage, not their code)
- Full page navigation (use Playwright for that)

### Shared UI Library (RCL) Testing Strategy

For projects with a shared Razor Class Library:

```
MyApp.UI/                    ← Shared Blazor components (RCL)
MyApp.UI.Tests/              ← bUnit tests for shared components
MyApp.Web/                   ← Web-specific pages
MyApp.Web.Tests/             ← bUnit tests for web-specific pages + integration tests
MyApp.Maui/                  ← MAUI-specific pages
MyApp.Maui.Tests/            ← bUnit tests for MAUI-specific components
```

**The RCL tests are the highest-value tests** — one test covers behavior for both Web and MAUI.

---

## 11. Playwright + TestServer (Browser E2E)

When you need real browser testing with `WebApplicationFactory`:

### The Problem

`WebApplicationFactory` uses `TestServer` (in-process). Playwright needs a real TCP port. The WAF casts `IServer` to `TestServer` internally, so you can't swap Kestrel in.

### The Solution: Kestrel Proxy

```csharp
public class PlaywrightWebApplicationFactory : WebApplicationFactory<Program>
{
    private HttpMessageInvoker _invoker = null!;
    private WebApplication _proxyApp = null!;
    public string BaseUrl { get; private set; } = null!;

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var testHost = base.CreateHost(builder);
        var testServer = (TestServer)testHost.Services
            .GetRequiredService<IServer>();
        _invoker = new HttpMessageInvoker(testServer.CreateHandler());

        // Start real Kestrel proxy
        var proxyBuilder = WebApplication.CreateBuilder();
        _proxyApp = proxyBuilder.Build();

        _proxyApp.Run(async context =>
        {
            var targetUri = new Uri($"http://localhost{context.Request.Path}{context.Request.QueryString}");
            var proxyReq = new HttpRequestMessage(
                new HttpMethod(context.Request.Method), targetUri);

            // Copy headers — CRITICAL: set Host for auth redirects
            foreach (var header in context.Request.Headers)
                proxyReq.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            proxyReq.Headers.Host = context.Request.Host.ToString();

            // Copy body
            if (context.Request.ContentLength > 0)
                proxyReq.Content = new StreamContent(context.Request.Body);

            var response = await _invoker.SendAsync(proxyReq, context.RequestAborted);

            context.Response.StatusCode = (int)response.StatusCode;
            foreach (var header in response.Headers.Concat(response.Content.Headers))
                context.Response.Headers[header.Key] = header.Value.ToArray();

            await response.Content.CopyToAsync(context.Response.Body);
        });

        _proxyApp.Urls.Add("http://127.0.0.1:0"); // Random port
        _proxyApp.Start();
        BaseUrl = _proxyApp.Urls.First();

        return testHost;
    }

    protected override void Dispose(bool disposing)
    {
        _proxyApp?.StopAsync().GetAwaiter().GetResult();
        _invoker?.Dispose();
        base.Dispose(disposing);
    }
}
```

### Usage in Playwright Tests

```csharp
public class PublicPageTests : IClassFixture<PlaywrightWebApplicationFactory>, IAsyncLifetime
{
    private readonly PlaywrightWebApplicationFactory _factory;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;

    public PublicPageTests(PlaywrightWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync();
    }

    [Fact]
    public async Task HomePage_Renders_WithTitle()
    {
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_factory.BaseUrl);

        var title = await page.TitleAsync();
        Assert.Contains("My App", title);
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }
}
```

### Gotcha: `proxyReq.Headers.Host`

Without setting the `Host` header on the proxy request, auth redirects (login, OAuth callbacks) will point to `localhost:TestServerPort` instead of `127.0.0.1:ProxyPort`. This causes infinite redirect loops. Always set:

```csharp
proxyReq.Headers.Host = context.Request.Host.ToString();
```

---

## 12. Auth Testing Patterns

### JWT Authentication

```csharp
// Helper in IntegrationTestBase
protected async Task<(HttpClient Client, JsonElement Auth)> CreateAuthenticatedClient()
{
    var client = CreateClient();
    var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
    {
        username = $"user_{Guid.NewGuid():N}",
        email = $"user_{Guid.NewGuid():N}@test.com",
        password = "TestPass123!"
    });
    response.EnsureSuccessStatusCode();

    var auth = await response.Content.ReadFromJsonAsync<JsonElement>();
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", auth.GetProperty("token").GetString()!);

    return (client, auth);
}
```

### API Key Authentication

```csharp
protected async Task<HttpClient> CreateApiKeyClient()
{
    var (client, auth) = await CreateAuthenticatedClient();

    // Create API key via the authenticated client
    var keyResponse = await client.PostAsJsonAsync("/api/v1/api-keys", new { name = "Test Key" });
    var keyBody = await keyResponse.Content.ReadFromJsonAsync<JsonElement>();
    var apiKey = keyBody.GetProperty("key").GetString()!;

    // Create new client with API key
    var apiKeyClient = CreateClient();
    apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    return apiKeyClient;
}
```

### Role-Based Auth Testing

```csharp
protected async Task<(HttpClient Client, JsonElement Auth)> CreateAdminClient()
{
    var (client, auth) = await CreateAuthenticatedClient();
    var playerId = auth.GetProperty("playerId").GetInt32();

    // Directly set admin flag in DB
    using var scope = Factory.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var player = await context.Players.FindAsync(playerId);
    player!.IsAdmin = true;
    await context.SaveChangesAsync();

    // Re-login to get admin claims in token
    var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new
    {
        email = player.Email,
        password = "TestPass123!"
    });
    var newAuth = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", newAuth.GetProperty("token").GetString()!);

    return (client, newAuth);
}
```

### Auth Scenarios to Test

Every project should have these integration tests:

- [ ] Register with valid credentials → 201
- [ ] Register with duplicate email → 409 or 400
- [ ] Login with correct password → 200 with token
- [ ] Login with wrong password → 401 (generic message, no user existence leak)
- [ ] Access protected endpoint without token → 401
- [ ] Access protected endpoint with expired token → 401
- [ ] Access admin endpoint with non-admin token → 403
- [ ] Access endpoint with wrong auth scheme → 401
- [ ] Token refresh with valid refresh token → new token
- [ ] Token refresh with deleted account → 401

---

## 13. Middleware Control in Tests

### The Static Toggle Pattern

```csharp
// In your middleware
public class RateLimitingMiddleware
{
    public static bool Enabled { get; set; } = true;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!Enabled)
        {
            await _next(context);
            return;
        }
        // ... normal rate limiting logic
    }
}
```

```csharp
// In your test setup
public static class IntegrationTestSetup
{
    public static void DisableRateLimiting()
    {
        RateLimitingMiddleware.Enabled = false;
    }
}

// In your test base constructor
public IntegrationTestBase(WebApplicationFactory<Program> factory, string dbPrefix)
{
    IntegrationTestSetup.DisableRateLimiting(); // ← Call first
    // ...
}
```

### Which Middleware to Disable

| Middleware | Disable in Tests? | Why |
|---|---|---|
| Rate limiting | Yes | Tests would hit limits during rapid execution |
| CORS | Usually no | Test client doesn't send Origin headers |
| Auth | No | Test auth behavior explicitly |
| Exception handler | No | Want to see real errors in test output |
| Caching | Sometimes | Can cause stale data in sequential tests |
| Logging | No | Useful for debugging test failures |

---

## 14. Database Strategies for Tests

### Decision Matrix

| Strategy | Speed | Fidelity | Isolation | Use When |
|---|---|---|---|---|
| **EF Core InMemory** | Fastest | Low | Per test class | Logic tests, no SQL features needed |
| **SQLite In-Memory** | Fast | Medium | Per test class | Need basic SQL but not SQL Server-specific features |
| **LocalDB + Respawn** | Slower | High | Per test (via Respawn) | Need real SQL Server behavior, transactions, constraints |
| **Docker SQL Server** | Slowest | Highest | Per test run | CI environment without LocalDB |

### EF Core InMemory (Default Choice)

```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
```

**Limitations:**
- No transactions (warns unless suppressed)
- No referential integrity enforcement
- No raw SQL support
- No stored procedures
- No schema validation

### LocalDB + Respawn (When You Need Real SQL)

```csharp
public class SqlFixture : IAsyncLifetime
{
    private Respawner _respawner = null!;

    public async Task InitializeAsync()
    {
        // Create database and run migrations
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        // Configure Respawn (fast cleanup)
        _respawner = await Respawner.CreateAsync(ConnectionString,
            new RespawnerOptions
            {
                TablesToIgnore = new[] { "__EFMigrationsHistory" },
                SchemasToInclude = new[] { "dbo" }
            });
    }

    public async Task ResetAsync() => await _respawner.ResetAsync(ConnectionString);
    public async Task DisposeAsync() { /* Drop database */ }
}
```

### CI Database Strategy

```yaml
# In CI workflow
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    env:
      SA_PASSWORD: TestPassword123!
      ACCEPT_EULA: Y
    ports:
      - 1433:1433

# Test step uses the container
- name: Run tests
  env:
    ConnectionStrings__DefaultConnection: "Server=localhost;Database=TestDb;User=sa;Password=TestPassword123!;TrustServerCertificate=True"
  run: dotnet test
```

**Important:** The `sqlcmd` tool is inside the container, NOT on the runner host. Never use `sqlcmd` in CI workflow steps. Use EF Core migrations from within the test fixture instead.

---

## 15. CI Pipeline Testing

### What Belongs in CI

```yaml
steps:
  # 1. Build
  - run: dotnet build --configuration Release

  # 2. Unit + Integration tests (these are your real tests)
  - run: dotnet test --configuration Release --collect:"XPlat Code Coverage"

  # 3. Smoke test (binary starts correctly)
  - run: |
      dotnet run --project src/MyApp.Api &
      sleep 10
      curl --fail http://localhost:5000/health
      curl --fail http://localhost:5000/openapi/v1.json
      kill %1

  # 4. Coverage reporting
  - uses: codecov/codecov-action@v4
```

### What Does NOT Belong in CI

- Shell-based database manipulation (`sqlcmd`, `psql`)
- Authenticated API testing via `curl`
- Manual HTTP testing scripts
- Anything that duplicates what xUnit tests already cover

### Coverage Configuration

```xml
<!-- coverage.runsettings -->
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat Code Coverage">
        <Configuration>
          <Format>cobertura</Format>
          <ExcludeByFile>**/Migrations/**</ExcludeByFile>
          <ExcludeByAttribute>ExcludeFromCodeCoverage</ExcludeByAttribute>
          <SkipAutoProps>true</SkipAutoProps>
          <IncludeTestAssembly>false</IncludeTestAssembly>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

---

## 16. Coverage Gap Prioritization

Not all coverage gaps are equal. Prioritize by **risk × frequency**:

### Priority 1: High Risk, High Frequency

- **Auth flows** — login, registration, token refresh, role checks
- **Payment/financial operations** — charges, refunds, balance changes
- **Data mutations** — create, update, delete operations
- **API contract** — response shapes, status codes, error formats

### Priority 2: High Risk, Lower Frequency

- **Admin operations** — privilege escalation, data modification
- **Webhook handlers** — external service callbacks
- **Background jobs** — scheduled tasks, queues
- **Race conditions** — concurrent access to shared resources

### Priority 3: Shared UI Components (High Leverage)

- **Shared component library (RCL)** — one test covers behavior for Web AND Mobile
- **Form components** — validation, submission, error display
- **Auth-gated components** — conditional rendering based on role

### Priority 4: Platform-Specific UI

- **Web-specific pages** — admin dashboards, reports
- **Mobile-specific pages** — offline mode, GPS, camera
- **Layout/navigation** — sidebar, header, routing

### Priority 5: Infrastructure

- **Middleware** — exception handler, correlation IDs, logging
- **Configuration** — settings providers, environment detection
- **Utilities** — extension methods, helpers

### What to Skip

- Auto-generated code (migrations, designer files)
- Simple property getters/setters
- Third-party library wrappers with no custom logic
- CSS/styling (not testable via code coverage)

---

## 17. Applying This to a Blazor/MAUI Stack

For a project with layers like **Api, Data, Shared, UI, Web, Maui**:

### Test Project Mapping

```
src/
  MyApp.Api/          → tests/MyApp.Api.Tests/        (integration + unit)
  MyApp.Data/         → tests/MyApp.Data.Tests/        (repository unit tests)
  MyApp.Shared/       → tests/MyApp.Shared.Tests/      (model/DTO validation)
  MyApp.UI/           → tests/MyApp.UI.Tests/           (bUnit component tests) ← HIGHEST VALUE
  MyApp.Web/          → tests/MyApp.Web.Tests/          (bUnit page tests + Playwright)
  MyApp.Maui/         → tests/MyApp.Maui.Tests/         (bUnit component tests)
```

### Per-Layer Testing Strategy

| Layer | Test Type | Framework | What to Test |
|-------|-----------|-----------|-------------|
| **Api** | Integration | xUnit + WebApplicationFactory | Endpoints, auth, error responses |
| **Api** | Unit | xUnit + Moq | Service logic, calculations |
| **Data** | Unit | xUnit + InMemory/LocalDB | Repository methods, queries |
| **Shared** | Unit | xUnit | Model validation, DTO mapping, enums |
| **UI (RCL)** | Component | xUnit + bUnit + Moq | Shared components (highest leverage) |
| **Web** | Component | xUnit + bUnit + Moq | Web-specific pages, admin views |
| **Web** | E2E | Playwright | Critical user flows (login, primary feature) |
| **Maui** | Component | xUnit + bUnit + Moq | Mobile-specific components |
| **Maui** | Unit | xUnit + Moq | Mobile services (GPS, offline, sync) |

### The UI (RCL) Testing Priority

If your shared component library has ~90% of the UI code and 0% test coverage, **start here**. Every bUnit test you write for the RCL covers behavior for both Web and MAUI simultaneously.

```csharp
// tests/MyApp.UI.Tests/Components/WorkOrderCardTests.cs
public class WorkOrderCardTests : TestContext
{
    [Fact]
    public void WorkOrderCard_WithOverdueDate_ShowsWarningBadge()
    {
        var workOrder = new WorkOrderDto
        {
            Title = "Fix HVAC",
            DueDate = DateTime.UtcNow.AddDays(-1),
            Status = WorkOrderStatus.InProgress
        };

        var cut = RenderComponent<WorkOrderCard>(
            parameters => parameters.Add(p => p.WorkOrder, workOrder));

        cut.Find("[data-testid='overdue-badge']").ShouldNotBeNull();
    }
}
```

### Coverage Improvement Roadmap

| Week | Focus | Expected Coverage Impact |
|------|-------|--------------------------|
| 1 | UI (RCL) shared components | +15-20% (covers Web + MAUI) |
| 2 | Api integration tests for uncovered endpoints | +5-10% |
| 3 | Web admin/supervisor page tests | +5-8% |
| 4 | Middleware, background services, state management | +3-5% |

---

## 18. Quick Reference for AI Assistants

**Read this section first before writing any tests for this project.**

### Before You Start

1. **Find the existing test base classes.** Search for `IntegrationTestBase`, `TestBase`, `Fixture` in the test projects.
2. **Find the existing CI workflow.** Read `.github/workflows/*.yml` to understand what runs and how.
3. **Find the coverage configuration.** Look for `*.runsettings`, `codecov.yml`, or coverage config in the CI workflow.
4. **Run the existing tests first.** `dotnet test` should pass before you change anything.
5. **Read 3-5 existing tests** to understand the project's conventions.

### Rules

1. **Use the established test infrastructure.** Don't bypass fixtures, base classes, or factories.
2. **Use xUnit + the existing framework** (bUnit, Moq, WebApplicationFactory). Don't introduce new test frameworks.
3. **Never use shell commands (curl, sqlcmd) to test API behavior.** Use `HttpClient` from the test framework.
4. **Never call `Database.Migrate()` outside the established fixture.** The fixture handles migrations.
5. **Never write tests that depend on external services** (real Stripe, real SMTP, real databases outside the fixture).
6. **Follow the existing test naming convention.** Look at existing tests for the pattern.
7. **One logical assertion per test.** Don't test 5 things in one method.
8. **Use anonymous objects for API requests** (`new { ... }`), not internal DTOs.
9. **Mark tests as `[Fact]` (no parameters) or `[Theory]` (parameterized).** Use `[InlineData]` for simple cases.
10. **If you're not sure how to test something, find a similar existing test and follow its pattern.**

### When Coverage Is Low

Don't try to boil the ocean. Start with the **highest-leverage tests**:

1. **Shared UI components (RCL)** — one test covers both web and mobile
2. **API endpoints without tests** — integration tests catch contract issues
3. **Auth flows** — most common failure point in multi-client systems
4. **Error handling paths** — ensure errors are caught, logged, and returned cleanly

### What Good Tests Look Like

```csharp
// GOOD: Clear setup, single behavior, meaningful assertion
[Fact]
public async Task GetCrewMembers_AsAdmin_ReturnsAllMembers()
{
    var (client, _) = await CreateAdminClient();
    var response = await client.GetAsync("/api/v1/crew");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var crew = await response.Content.ReadFromJsonAsync<JsonElement>();
    Assert.True(crew.GetProperty("items").GetArrayLength() > 0);
}
```

```csharp
// BAD: Multiple behaviors, unclear intent, implementation-coupled
[Fact]
public async Task TestCrewEndpoints()
{
    var client = CreateClient();
    // Register, login, get crew, create crew, update crew, delete crew...
    // 50 lines of assertions
}
```

### When Things Go Wrong

If your test doesn't work:

1. **Check the existing tests first.** Does the same pattern work elsewhere?
2. **Check the error message.** It usually tells you exactly what's wrong.
3. **Don't add workarounds.** If the fixture doesn't support your scenario, extend the fixture properly.
4. **Don't disable or skip tests** to make CI pass. Fix the test or fix the code.
5. **If you're going in circles, stop.** Describe what you've tried and ask for direction.

---

## References

- [HARDENING.md](HARDENING.md) — companion playbook for production hardening
- [Sections 14-18 of HARDENING.md](HARDENING.md#14-requestresponse-tracing) — request tracing, contract testing, environment parity, triage
- [xUnit Documentation](https://xunit.net/docs/getting-started/netcore/cmdline)
- [bUnit Documentation](https://bunit.dev/docs/getting-started/)
- [WebApplicationFactory Documentation](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)
- [Respawn](https://github.com/jbogard/Respawn) — fast database reset for integration tests

---

*This playbook was written after an AI assistant implemented shell-based database testing, direct migration calls, and curl-based API testing in CI — each "fix" creating a new problem. Every anti-pattern documented here was actually attempted. Every recommended pattern was proven in production with 600+ passing tests.*
