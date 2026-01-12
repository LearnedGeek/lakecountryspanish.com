using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider, bool isDevelopment)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

        // Apply any pending migrations (this also creates the database if it doesn't exist)
        await context.Database.MigrateAsync();

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
        var adminEmail = "admin@lakecountryspanish.com";
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
                IsActive = true,
                MustChangePassword = false  // Admin doesn't need to change password on first login
            };

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

        // Seed default badges if none exist
        if (!context.Badges.Any())
        {
            await SeedBadgesAsync(context);
        }

        // Only seed test data in development environment
        if (isDevelopment)
        {
            await SeedDevelopmentDataAsync(userManager, context);
        }
    }

    private static async Task SeedBadgesAsync(ApplicationDbContext context)
    {
        var badges = new List<Badge>
        {
            // Milestone badges (points-based)
            new Badge
            {
                Name = "First Steps",
                Description = "Earn your first 50 points",
                Category = BadgeCategory.Milestone,
                RequirementType = BadgeRequirementType.Points,
                RequirementValue = 50,
                BonusPoints = 10,
                IsActive = true,
                DisplayOrder = 1
            },
            new Badge
            {
                Name = "Century Club",
                Description = "Earn 100 total points",
                Category = BadgeCategory.Milestone,
                RequirementType = BadgeRequirementType.Points,
                RequirementValue = 100,
                BonusPoints = 25,
                IsActive = true,
                DisplayOrder = 2
            },
            new Badge
            {
                Name = "Point Master",
                Description = "Earn 500 total points",
                Category = BadgeCategory.Milestone,
                RequirementType = BadgeRequirementType.Points,
                RequirementValue = 500,
                BonusPoints = 50,
                IsActive = true,
                DisplayOrder = 3
            },
            new Badge
            {
                Name = "Point Champion",
                Description = "Earn 1,000 total points",
                Category = BadgeCategory.Milestone,
                RequirementType = BadgeRequirementType.Points,
                RequirementValue = 1000,
                BonusPoints = 100,
                IsActive = true,
                DisplayOrder = 4
            },

            // Consistency badges (streak-based)
            new Badge
            {
                Name = "Getting Started",
                Description = "Maintain a 3-day streak",
                Category = BadgeCategory.Consistency,
                RequirementType = BadgeRequirementType.Streak,
                RequirementValue = 3,
                BonusPoints = 15,
                IsActive = true,
                DisplayOrder = 1
            },
            new Badge
            {
                Name = "Weekly Warrior",
                Description = "Maintain a 7-day streak",
                Category = BadgeCategory.Consistency,
                RequirementType = BadgeRequirementType.Streak,
                RequirementValue = 7,
                BonusPoints = 30,
                IsActive = true,
                DisplayOrder = 2
            },
            new Badge
            {
                Name = "Committed Learner",
                Description = "Maintain a 14-day streak",
                Category = BadgeCategory.Consistency,
                RequirementType = BadgeRequirementType.Streak,
                RequirementValue = 14,
                BonusPoints = 50,
                IsActive = true,
                DisplayOrder = 3
            },
            new Badge
            {
                Name = "Monthly Master",
                Description = "Maintain a 30-day streak",
                Category = BadgeCategory.Consistency,
                RequirementType = BadgeRequirementType.Streak,
                RequirementValue = 30,
                BonusPoints = 100,
                IsActive = true,
                DisplayOrder = 4
            },

            // Class completion badges
            new Badge
            {
                Name = "First Class",
                Description = "Complete your first class",
                Category = BadgeCategory.Milestone,
                RequirementType = BadgeRequirementType.ClassesCompleted,
                RequirementValue = 1,
                BonusPoints = 20,
                IsActive = true,
                DisplayOrder = 5
            },
            new Badge
            {
                Name = "Dedicated Student",
                Description = "Complete 10 classes",
                Category = BadgeCategory.Milestone,
                RequirementType = BadgeRequirementType.ClassesCompleted,
                RequirementValue = 10,
                BonusPoints = 50,
                IsActive = true,
                DisplayOrder = 6
            },
            new Badge
            {
                Name = "Class Veteran",
                Description = "Complete 25 classes",
                Category = BadgeCategory.Milestone,
                RequirementType = BadgeRequirementType.ClassesCompleted,
                RequirementValue = 25,
                BonusPoints = 100,
                IsActive = true,
                DisplayOrder = 7
            },
            new Badge
            {
                Name = "Spanish Scholar",
                Description = "Complete 50 classes",
                Category = BadgeCategory.Milestone,
                RequirementType = BadgeRequirementType.ClassesCompleted,
                RequirementValue = 50,
                BonusPoints = 200,
                IsActive = true,
                DisplayOrder = 8
            },

            // CEFR Level badges
            new Badge
            {
                Name = "A1 Achieved",
                Description = "Reach CEFR level A1 (Beginner)",
                Category = BadgeCategory.LevelProgress,
                RequirementType = BadgeRequirementType.CefrLevel,
                RequirementValue = 1,
                RequirementContext = "A1",
                BonusPoints = 50,
                IsActive = true,
                DisplayOrder = 1
            },
            new Badge
            {
                Name = "A2 Achieved",
                Description = "Reach CEFR level A2 (Elementary)",
                Category = BadgeCategory.LevelProgress,
                RequirementType = BadgeRequirementType.CefrLevel,
                RequirementValue = 2,
                RequirementContext = "A2",
                BonusPoints = 100,
                IsActive = true,
                DisplayOrder = 2
            },
            new Badge
            {
                Name = "B1 Achieved",
                Description = "Reach CEFR level B1 (Intermediate)",
                Category = BadgeCategory.LevelProgress,
                RequirementType = BadgeRequirementType.CefrLevel,
                RequirementValue = 3,
                RequirementContext = "B1",
                BonusPoints = 150,
                IsActive = true,
                DisplayOrder = 3
            },
            new Badge
            {
                Name = "B2 Achieved",
                Description = "Reach CEFR level B2 (Upper Intermediate)",
                Category = BadgeCategory.LevelProgress,
                RequirementType = BadgeRequirementType.CefrLevel,
                RequirementValue = 4,
                RequirementContext = "B2",
                BonusPoints = 200,
                IsActive = true,
                DisplayOrder = 4
            },

            // Special badges (manually awarded)
            new Badge
            {
                Name = "Star Student",
                Description = "Awarded for exceptional effort and progress",
                Category = BadgeCategory.Special,
                RequirementType = BadgeRequirementType.Custom,
                RequirementValue = 0,
                BonusPoints = 75,
                IsActive = true,
                DisplayOrder = 1
            },
            new Badge
            {
                Name = "Referral Champion",
                Description = "Referred a friend who became a student",
                Category = BadgeCategory.Special,
                RequirementType = BadgeRequirementType.Custom,
                RequirementValue = 0,
                BonusPoints = 100,
                IsActive = true,
                DisplayOrder = 2
            }
        };

        context.Badges.AddRange(badges);
        await context.SaveChangesAsync();
    }

    private static async Task SeedDevelopmentDataAsync(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
    {
        // Seed test students if none exist
        if (!await userManager.Users.AnyAsync(u => u.Email != "admin@lakecountryspanish.com"))
        {
            var testStudents = new[]
            {
                new { Email = "john.doe@test.com", FirstName = "John", LastName = "Doe", ClassroomUrl = "https://zoom.us/j/1234567890" },
                new { Email = "jane.smith@test.com", FirstName = "Jane", LastName = "Smith", ClassroomUrl = "https://meet.google.com/abc-defg-hij" },
                new { Email = "bob.wilson@test.com", FirstName = "Bob", LastName = "Wilson", ClassroomUrl = (string?)null },
            };

            foreach (var student in testStudents)
            {
                var user = new ApplicationUser
                {
                    UserName = student.Email,
                    Email = student.Email,
                    FirstName = student.FirstName,
                    LastName = student.LastName,
                    EmailConfirmed = true,
                    IsActive = true,
                    ClassroomUrl = student.ClassroomUrl,
                    MustChangePassword = false  // Test users don't need to change password
                };

                var result = await userManager.CreateAsync(user, "Student123!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, "Student");
                }
            }
        }

        // Seed time slots if none exist
        if (!context.TimeSlots.Any())
        {
            var timeSlots = new List<TimeSlot>
            {
                // Monday slots
                new TimeSlot { DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), IsRecurring = true, IsActive = true },
                new TimeSlot { DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(14, 0, 0), EndTime = new TimeSpan(15, 0, 0), IsRecurring = true, IsActive = true },
                // Tuesday slots
                new TimeSlot { DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 0, 0), IsRecurring = true, IsActive = true },
                new TimeSlot { DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeSpan(15, 0, 0), EndTime = new TimeSpan(16, 0, 0), IsRecurring = true, IsActive = true },
                // Wednesday slots
                new TimeSlot { DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), IsRecurring = true, IsActive = true },
                new TimeSlot { DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeSpan(13, 0, 0), EndTime = new TimeSpan(14, 0, 0), IsRecurring = true, IsActive = true },
                // Thursday slots
                new TimeSlot { DayOfWeek = DayOfWeek.Thursday, StartTime = new TimeSpan(11, 0, 0), EndTime = new TimeSpan(12, 0, 0), IsRecurring = true, IsActive = true },
                new TimeSlot { DayOfWeek = DayOfWeek.Thursday, StartTime = new TimeSpan(16, 0, 0), EndTime = new TimeSpan(17, 0, 0), IsRecurring = true, IsActive = true },
                // Friday slots
                new TimeSlot { DayOfWeek = DayOfWeek.Friday, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 0, 0), IsRecurring = true, IsActive = true },
                new TimeSlot { DayOfWeek = DayOfWeek.Friday, StartTime = new TimeSpan(14, 0, 0), EndTime = new TimeSpan(15, 0, 0), IsRecurring = true, IsActive = true },
            };

            context.TimeSlots.AddRange(timeSlots);
            await context.SaveChangesAsync();
        }

        // Seed blocked dates if none exist
        if (!context.BlockedDates.Any())
        {
            var blockedDates = new List<BlockedDate>
            {
                new BlockedDate
                {
                    StartDate = DateTime.Today.AddDays(14),
                    EndDate = DateTime.Today.AddDays(14),
                    Reason = "Teacher Conference",
                    CreatedAt = DateTime.UtcNow
                },
                new BlockedDate
                {
                    StartDate = DateTime.Today.AddDays(30),
                    EndDate = DateTime.Today.AddDays(32),
                    Reason = "Holiday Break",
                    CreatedAt = DateTime.UtcNow
                }
            };

            context.BlockedDates.AddRange(blockedDates);
            await context.SaveChangesAsync();
        }

        // Give test students some package credits
        var johnDoe = await userManager.FindByEmailAsync("john.doe@test.com");
        var janeSmith = await userManager.FindByEmailAsync("jane.smith@test.com");
        var bobWilson = await userManager.FindByEmailAsync("bob.wilson@test.com");

        if (johnDoe != null && !context.StudentPackages.Any(sp => sp.StudentId == johnDoe.Id))
        {
            var tenPackage = await context.Packages.FirstOrDefaultAsync(p => p.ClassCount == 10);
            if (tenPackage != null)
            {
                context.StudentPackages.Add(new StudentPackage
                {
                    StudentId = johnDoe.Id,
                    PackageId = tenPackage.Id,
                    ClassesRemaining = 8,
                    PurchaseDate = DateTime.UtcNow.AddDays(-30),
                    ExpirationDate = DateTime.UtcNow.AddDays(335)
                });
            }
        }

        if (janeSmith != null && !context.StudentPackages.Any(sp => sp.StudentId == janeSmith.Id))
        {
            var fivePackage = await context.Packages.FirstOrDefaultAsync(p => p.ClassCount == 5);
            if (fivePackage != null)
            {
                context.StudentPackages.Add(new StudentPackage
                {
                    StudentId = janeSmith.Id,
                    PackageId = fivePackage.Id,
                    ClassesRemaining = 3,
                    PurchaseDate = DateTime.UtcNow.AddDays(-14),
                    ExpirationDate = DateTime.UtcNow.AddDays(351)
                });
            }
        }

        if (bobWilson != null && !context.StudentPackages.Any(sp => sp.StudentId == bobWilson.Id))
        {
            var singlePackage = await context.Packages.FirstOrDefaultAsync(p => p.ClassCount == 1);
            if (singlePackage != null)
            {
                context.StudentPackages.Add(new StudentPackage
                {
                    StudentId = bobWilson.Id,
                    PackageId = singlePackage.Id,
                    ClassesRemaining = 1,
                    PurchaseDate = DateTime.UtcNow.AddDays(-7),
                    ExpirationDate = DateTime.UtcNow.AddDays(358)
                });
            }
        }

        await context.SaveChangesAsync();

        // Seed some scheduled classes if none exist
        if (!context.ScheduledClasses.Any() && johnDoe != null && janeSmith != null)
        {
            var mondaySlot = await context.TimeSlots.FirstOrDefaultAsync(ts => ts.DayOfWeek == DayOfWeek.Monday && ts.StartTime.Hours == 9);
            var tuesdaySlot = await context.TimeSlots.FirstOrDefaultAsync(ts => ts.DayOfWeek == DayOfWeek.Tuesday && ts.StartTime.Hours == 10);
            var wednesdaySlot = await context.TimeSlots.FirstOrDefaultAsync(ts => ts.DayOfWeek == DayOfWeek.Wednesday && ts.StartTime.Hours == 9);

            var scheduledClasses = new List<ScheduledClass>();

            // Find next occurrence of each day
            var today = DateTime.Today;
            var nextMonday = today.AddDays((int)DayOfWeek.Monday - (int)today.DayOfWeek + (today.DayOfWeek >= DayOfWeek.Monday ? 7 : 0));
            var nextTuesday = today.AddDays((int)DayOfWeek.Tuesday - (int)today.DayOfWeek + (today.DayOfWeek >= DayOfWeek.Tuesday ? 7 : 0));
            var nextWednesday = today.AddDays((int)DayOfWeek.Wednesday - (int)today.DayOfWeek + (today.DayOfWeek >= DayOfWeek.Wednesday ? 7 : 0));

            var johnPackage = await context.StudentPackages.FirstOrDefaultAsync(sp => sp.StudentId == johnDoe.Id);
            var janePackage = await context.StudentPackages.FirstOrDefaultAsync(sp => sp.StudentId == janeSmith.Id);

            if (mondaySlot != null && johnPackage != null)
            {
                // John has class this coming Monday and next Monday
                scheduledClasses.Add(new ScheduledClass
                {
                    StudentId = johnDoe.Id,
                    TimeSlotId = mondaySlot.Id,
                    ClassDateTime = nextMonday.Add(mondaySlot.StartTime),
                    Status = ClassStatus.Scheduled,
                    PaymentStatus = PaymentStatus.PartOfPackage,
                    StudentPackageId = johnPackage.Id,
                    CreatedAt = DateTime.UtcNow
                });
                scheduledClasses.Add(new ScheduledClass
                {
                    StudentId = johnDoe.Id,
                    TimeSlotId = mondaySlot.Id,
                    ClassDateTime = nextMonday.AddDays(7).Add(mondaySlot.StartTime),
                    Status = ClassStatus.Scheduled,
                    PaymentStatus = PaymentStatus.PartOfPackage,
                    StudentPackageId = johnPackage.Id,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (tuesdaySlot != null && janePackage != null)
            {
                // Jane has class this coming Tuesday
                scheduledClasses.Add(new ScheduledClass
                {
                    StudentId = janeSmith.Id,
                    TimeSlotId = tuesdaySlot.Id,
                    ClassDateTime = nextTuesday.Add(tuesdaySlot.StartTime),
                    Status = ClassStatus.Scheduled,
                    PaymentStatus = PaymentStatus.PartOfPackage,
                    StudentPackageId = janePackage.Id,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (wednesdaySlot != null && johnPackage != null)
            {
                // Completed classes from recent weeks (for feedback testing)
                scheduledClasses.Add(new ScheduledClass
                {
                    StudentId = johnDoe.Id,
                    TimeSlotId = wednesdaySlot.Id,
                    ClassDateTime = today.AddDays(-3).Add(wednesdaySlot.StartTime),
                    Status = ClassStatus.Completed,
                    PaymentStatus = PaymentStatus.PartOfPackage,
                    StudentPackageId = johnPackage.Id,
                    TeacherNotes = "Reviewed verb conjugations. Good progress on past tense. Homework: Practice irregular verbs.",
                    CreatedAt = DateTime.UtcNow.AddDays(-10)
                });

                scheduledClasses.Add(new ScheduledClass
                {
                    StudentId = johnDoe.Id,
                    TimeSlotId = wednesdaySlot.Id,
                    ClassDateTime = today.AddDays(-10).Add(wednesdaySlot.StartTime),
                    Status = ClassStatus.Completed,
                    PaymentStatus = PaymentStatus.PartOfPackage,
                    StudentPackageId = johnPackage.Id,
                    TeacherNotes = "Introduction to subjunctive mood. Student grasped concepts well.",
                    CreatedAt = DateTime.UtcNow.AddDays(-17)
                });
            }

            // Add completed classes for Jane too
            if (tuesdaySlot != null && janePackage != null)
            {
                scheduledClasses.Add(new ScheduledClass
                {
                    StudentId = janeSmith.Id,
                    TimeSlotId = tuesdaySlot.Id,
                    ClassDateTime = today.AddDays(-5).Add(tuesdaySlot.StartTime),
                    Status = ClassStatus.Completed,
                    PaymentStatus = PaymentStatus.PartOfPackage,
                    StudentPackageId = janePackage.Id,
                    TeacherNotes = "Conversational practice - ordering food at a restaurant. Great pronunciation!",
                    CreatedAt = DateTime.UtcNow.AddDays(-12)
                });
            }

            context.ScheduledClasses.AddRange(scheduledClasses);
            await context.SaveChangesAsync();
        }

        // Seed a sample approved testimonial for the homepage
        if (!context.ClassFeedbacks.Any() && johnDoe != null)
        {
            var completedClass = await context.ScheduledClasses
                .FirstOrDefaultAsync(sc => sc.StudentId == johnDoe.Id && sc.Status == ClassStatus.Completed);

            if (completedClass != null)
            {
                var sampleFeedback = new ClassFeedback
                {
                    ScheduledClassId = completedClass.Id,
                    StudentId = johnDoe.Id,
                    Rating = 5,
                    PrivateComment = "Really enjoyed the lesson structure!",
                    PublicTestimonial = "Karen is an amazing teacher! Her lessons are well-structured and she makes learning Spanish fun. I've made more progress in a few weeks than I did in years of self-study.",
                    AllowPublicDisplay = true,
                    IsApproved = true,
                    IsFeatured = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-5)
                };

                context.ClassFeedbacks.Add(sampleFeedback);
                await context.SaveChangesAsync();
            }
        }
    }
}
