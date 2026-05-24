using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Services;
using LakeCountrySpanish.Web.Services.Curriculum;
using LakeCountrySpanish.Web.Services.Curriculum.Blocks;
using LakeCountrySpanish.Web.Services.Curriculum.Drafter;
using LakeCountrySpanish.Web.Services.Media;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

// Load appsettings.Local.json (gitignored) AFTER appsettings.{Environment}.json so
// that local secrets override committed placeholders without ever being tracked.
// This is the LCS convention — preferred over user-secrets because it doesn't
// require per-machine setup and is uniform with the appsettings.Production.json
// pattern used in deployment.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Configure logging to suppress noisy CookieTempDataProvider warnings
// These occur when a stale TempData cookie (encrypted with old keys) can't be decrypted
// This is expected behavior after app restarts in development - the cookie is simply ignored
builder.Logging.AddFilter("Microsoft.AspNetCore.Mvc.ViewFeatures.CookieTempDataProvider", LogLevel.Error);

// Configure Stripe
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

// Add DbContext (PostgreSQL via Npgsql)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure cookie settings
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});

// Add services
builder.Services.AddScoped<IScheduleService, ScheduleService>();
builder.Services.AddScoped<IClassSchedulingService, ClassSchedulingService>();
builder.Services.AddScoped<IPaymentService, StripePaymentService>();
builder.Services.AddScoped<ISubscriptionService, StripeSubscriptionService>();
builder.Services.AddScoped<ITokenService, LakeCountrySpanish.Web.Services.TokenService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<IGamificationService, GamificationService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<IClaudeApiService, ClaudeApiService>();
builder.Services.AddScoped<IAssignmentService, AssignmentService>();
builder.Services.AddScoped<IPlacementTestService, PlacementTestService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddSingleton<IDocumentRenderingService, DocumentRenderingService>();

// Media library: image processing primitives + storage orchestration + source adapters.
// PixabaySettings binds the gitignored appsettings.Local.json "Pixabay" section.
builder.Services.Configure<PixabaySettings>(builder.Configuration.GetSection(PixabaySettings.SectionName));
builder.Services.AddSingleton<IImageProcessingService, ImageProcessingService>();
builder.Services.AddScoped<IMediaService, MediaService>();
builder.Services.AddHttpClient<PixabayImageSourceAdapter>();
builder.Services.AddScoped<IImageSourceAdapter, PixabayImageSourceAdapter>();

// Curriculum authoring services.
builder.Services.AddScoped<ICurriculumDayService, CurriculumDayService>();
builder.Services.AddSingleton<IBlockCompiler, BlockCompiler>();

// AI-assisted curriculum drafter — wraps Anthropic with the LCS component
// vocabulary baked into the system prompt. See
// docs/curriculum-system/ai-assisted-authoring.md.
builder.Services.AddScoped<DraftingPromptBuilder>();
builder.Services.AddHttpClient<ICurriculumDrafter, CurriculumDrafter>(client =>
{
    // Anthropic drafter calls can take 10-15s; default 100s timeout is fine
    // but make it explicit so we know what we're committing to.
    client.Timeout = TimeSpan.FromSeconds(45);
});

builder.Services.AddScoped<INotificationScheduler, NotificationScheduler>();
builder.Services.AddHostedService<NotificationBackgroundService>();
builder.Services.AddHttpClient();

// Add MVC
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Apply pending database migrations automatically
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            logger.LogInformation("Applying {Count} pending database migration(s)...", pendingMigrations.Count());
            await context.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while applying database migrations.");
        throw; // Fail fast - don't start app with incompatible database
    }

    // Seed roles, admin user, and development test data
    await SeedData.InitializeAsync(services, app.Environment.IsDevelopment());
}

app.Run();
