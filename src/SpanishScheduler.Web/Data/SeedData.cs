using Microsoft.AspNetCore.Identity;
using SpanishScheduler.Web.Models.Entities;

namespace SpanishScheduler.Web.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Create roles
        string[] roles = { "Admin", "Student" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // Create admin user if not exists
        var adminEmail = "admin@spanishwithkaren.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "Karen",
                LastName = "Admin",
                EmailConfirmed = true,
                IsActive = true
            };

            // Default password - should be changed on first login
            var result = await userManager.CreateAsync(adminUser, "Admin123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }

        // Seed default packages if none exist
        if (!context.Packages.Any())
        {
            var packages = new List<Package>
            {
                new Package
                {
                    Name = "Single Class",
                    Description = "One 1-hour Spanish lesson",
                    ClassCount = 1,
                    Price = 25.00m,
                    IsActive = true
                },
                new Package
                {
                    Name = "5 Class Package",
                    Description = "Five 1-hour Spanish lessons (Save $10!)",
                    ClassCount = 5,
                    Price = 115.00m,
                    IsActive = true
                },
                new Package
                {
                    Name = "10 Class Package",
                    Description = "Ten 1-hour Spanish lessons (Save $30!)",
                    ClassCount = 10,
                    Price = 220.00m,
                    IsActive = true
                },
                new Package
                {
                    Name = "20 Class Package",
                    Description = "Twenty 1-hour Spanish lessons (Save $80!)",
                    ClassCount = 20,
                    Price = 420.00m,
                    IsActive = true
                }
            };

            context.Packages.AddRange(packages);
            await context.SaveChangesAsync();
        }
    }
}
