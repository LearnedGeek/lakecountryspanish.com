using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Services;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

// Configure logging to suppress noisy CookieTempDataProvider warnings
// These occur when a stale TempData cookie (encrypted with old keys) can't be decrypted
// This is expected behavior after app restarts in development - the cookie is simply ignored
builder.Logging.AddFilter("Microsoft.AspNetCore.Mvc.ViewFeatures.CookieTempDataProvider", LogLevel.Error);

// Configure Stripe
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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
