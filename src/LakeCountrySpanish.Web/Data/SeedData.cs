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

        // Seed curriculum topics if none exist
        if (!context.CurriculumTopics.Any())
        {
            await SeedCurriculumTopicsAsync(context);
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

    private static async Task SeedCurriculumTopicsAsync(ApplicationDbContext context)
    {
        var topics = new List<CurriculumTopic>
        {
            // A1 - Beginner Topics
            new CurriculumTopic
            {
                Name = "Greetings & Introductions",
                Description = "Basic greetings, introducing yourself, and asking about others",
                CefrLevel = "A1",
                Type = TopicType.Vocabulary,
                DisplayOrder = 1,
                IsActive = true,
                Keywords = "hola, buenos dias, como te llamas, mucho gusto, introductions, greetings"
            },
            new CurriculumTopic
            {
                Name = "Numbers 1-100",
                Description = "Counting and using numbers in everyday situations",
                CefrLevel = "A1",
                Type = TopicType.Vocabulary,
                DisplayOrder = 2,
                IsActive = true,
                Keywords = "numbers, uno, dos, tres, counting, numeros"
            },
            new CurriculumTopic
            {
                Name = "Present Tense - Regular Verbs",
                Description = "Conjugating -ar, -er, -ir verbs in present tense",
                CefrLevel = "A1",
                Type = TopicType.Grammar,
                DisplayOrder = 3,
                IsActive = true,
                Keywords = "present tense, -ar verbs, -er verbs, -ir verbs, hablar, comer, vivir, conjugation"
            },
            new CurriculumTopic
            {
                Name = "Ser vs Estar",
                Description = "Understanding when to use ser and estar",
                CefrLevel = "A1",
                Type = TopicType.Grammar,
                DisplayOrder = 4,
                IsActive = true,
                Keywords = "ser, estar, to be, permanent, temporary, location, characteristics"
            },
            new CurriculumTopic
            {
                Name = "Family & Relationships",
                Description = "Family members and describing relationships",
                CefrLevel = "A1",
                Type = TopicType.Vocabulary,
                DisplayOrder = 5,
                IsActive = true,
                Keywords = "familia, madre, padre, hermano, sister, family members, relationships"
            },
            new CurriculumTopic
            {
                Name = "Colors & Basic Adjectives",
                Description = "Describing things with colors and simple adjectives",
                CefrLevel = "A1",
                Type = TopicType.Vocabulary,
                DisplayOrder = 6,
                IsActive = true,
                Keywords = "colors, rojo, azul, verde, grande, pequeno, adjectives, colores"
            },
            new CurriculumTopic
            {
                Name = "Days, Months & Time",
                Description = "Expressing dates, days of the week, and telling time",
                CefrLevel = "A1",
                Type = TopicType.Vocabulary,
                DisplayOrder = 7,
                IsActive = true,
                Keywords = "days, months, lunes, enero, que hora es, time, calendar"
            },

            // A2 - Elementary Topics
            new CurriculumTopic
            {
                Name = "Present Tense - Irregular Verbs",
                Description = "Common irregular verbs like ir, tener, hacer, poder",
                CefrLevel = "A2",
                Type = TopicType.Grammar,
                DisplayOrder = 1,
                IsActive = true,
                Keywords = "irregular verbs, ir, tener, hacer, poder, querer, venir, saber"
            },
            new CurriculumTopic
            {
                Name = "Reflexive Verbs",
                Description = "Daily routines with reflexive verbs",
                CefrLevel = "A2",
                Type = TopicType.Grammar,
                DisplayOrder = 2,
                IsActive = true,
                Keywords = "reflexive, me, te, se, levantarse, ducharse, vestirse, daily routine"
            },
            new CurriculumTopic
            {
                Name = "Food & Restaurant Vocabulary",
                Description = "Ordering food, describing meals, restaurant situations",
                CefrLevel = "A2",
                Type = TopicType.Vocabulary,
                DisplayOrder = 3,
                IsActive = true,
                Keywords = "food, comida, restaurante, menu, ordering, desayuno, almuerzo, cena"
            },
            new CurriculumTopic
            {
                Name = "Direct & Indirect Object Pronouns",
                Description = "Using lo, la, le, les in sentences",
                CefrLevel = "A2",
                Type = TopicType.Grammar,
                DisplayOrder = 4,
                IsActive = true,
                Keywords = "object pronouns, lo, la, le, les, me, te, direct, indirect"
            },
            new CurriculumTopic
            {
                Name = "Past Tense - Preterite",
                Description = "Talking about completed past actions",
                CefrLevel = "A2",
                Type = TopicType.Grammar,
                DisplayOrder = 5,
                IsActive = true,
                Keywords = "preterite, past tense, ayer, completed actions, -e, -iste, -o"
            },
            new CurriculumTopic
            {
                Name = "Travel & Transportation",
                Description = "Vocabulary for traveling, directions, and transportation",
                CefrLevel = "A2",
                Type = TopicType.Vocabulary,
                DisplayOrder = 6,
                IsActive = true,
                Keywords = "travel, viajar, aeropuerto, tren, autobus, directions, transportation"
            },

            // B1 - Intermediate Topics
            new CurriculumTopic
            {
                Name = "Past Tense - Imperfect",
                Description = "Describing ongoing past actions and habits",
                CefrLevel = "B1",
                Type = TopicType.Grammar,
                DisplayOrder = 1,
                IsActive = true,
                Keywords = "imperfect, imperfecto, -aba, -ia, used to, was doing, habitual past"
            },
            new CurriculumTopic
            {
                Name = "Preterite vs Imperfect",
                Description = "Choosing between preterite and imperfect tenses",
                CefrLevel = "B1",
                Type = TopicType.Grammar,
                DisplayOrder = 2,
                IsActive = true,
                Keywords = "preterite, imperfect, past tenses, when to use, completed vs ongoing"
            },
            new CurriculumTopic
            {
                Name = "Future Tense",
                Description = "Expressing future plans and predictions",
                CefrLevel = "B1",
                Type = TopicType.Grammar,
                DisplayOrder = 3,
                IsActive = true,
                Keywords = "future tense, futuro, -e, -as, -a, will, predictions, plans"
            },
            new CurriculumTopic
            {
                Name = "Conditional Tense",
                Description = "Expressing hypothetical situations and polite requests",
                CefrLevel = "B1",
                Type = TopicType.Grammar,
                DisplayOrder = 4,
                IsActive = true,
                Keywords = "conditional, condicional, -ia, would, hypothetical, polite requests"
            },
            new CurriculumTopic
            {
                Name = "Subjunctive - Introduction",
                Description = "Basic uses of the subjunctive mood",
                CefrLevel = "B1",
                Type = TopicType.Grammar,
                DisplayOrder = 5,
                IsActive = true,
                Keywords = "subjunctive, subjuntivo, que, quiero que, espero que, emotions, wishes"
            },
            new CurriculumTopic
            {
                Name = "Health & Body",
                Description = "Medical vocabulary, describing symptoms and ailments",
                CefrLevel = "B1",
                Type = TopicType.Vocabulary,
                DisplayOrder = 6,
                IsActive = true,
                Keywords = "health, salud, doctor, symptoms, body parts, cuerpo, enfermo"
            },
            new CurriculumTopic
            {
                Name = "Work & Professions",
                Description = "Job vocabulary, workplace conversations",
                CefrLevel = "B1",
                Type = TopicType.Vocabulary,
                DisplayOrder = 7,
                IsActive = true,
                Keywords = "work, trabajo, profesion, office, job interview, career"
            },

            // B2 - Upper Intermediate Topics
            new CurriculumTopic
            {
                Name = "Subjunctive - Advanced Uses",
                Description = "Complex subjunctive constructions and triggers",
                CefrLevel = "B2",
                Type = TopicType.Grammar,
                DisplayOrder = 1,
                IsActive = true,
                Keywords = "subjunctive, aunque, para que, antes de que, advanced subjunctive"
            },
            new CurriculumTopic
            {
                Name = "Past Subjunctive",
                Description = "Imperfect subjunctive for hypotheticals and past wishes",
                CefrLevel = "B2",
                Type = TopicType.Grammar,
                DisplayOrder = 2,
                IsActive = true,
                Keywords = "past subjunctive, imperfect subjunctive, -ara, -iera, si clauses"
            },
            new CurriculumTopic
            {
                Name = "Passive Voice & Se Constructions",
                Description = "Passive structures and impersonal se",
                CefrLevel = "B2",
                Type = TopicType.Grammar,
                DisplayOrder = 3,
                IsActive = true,
                Keywords = "passive voice, se pasivo, se impersonal, fue hecho, se habla"
            },
            new CurriculumTopic
            {
                Name = "Idiomatic Expressions",
                Description = "Common Spanish idioms and colloquial expressions",
                CefrLevel = "B2",
                Type = TopicType.Vocabulary,
                DisplayOrder = 4,
                IsActive = true,
                Keywords = "idioms, expressions, modismos, colloquial, slang, native speaker"
            },
            new CurriculumTopic
            {
                Name = "News & Current Events",
                Description = "Reading and discussing news articles",
                CefrLevel = "B2",
                Type = TopicType.Reading,
                DisplayOrder = 5,
                IsActive = true,
                Keywords = "news, noticias, current events, politics, economia, reading comprehension"
            },
            new CurriculumTopic
            {
                Name = "Hispanic Culture & Traditions",
                Description = "Cultural practices across Spanish-speaking countries",
                CefrLevel = "B2",
                Type = TopicType.Culture,
                DisplayOrder = 6,
                IsActive = true,
                Keywords = "culture, traditions, holidays, fiestas, customs, Hispanic countries"
            },

            // C1 - Advanced Topics
            new CurriculumTopic
            {
                Name = "Advanced Verb Tenses",
                Description = "Perfect tenses, pluperfect, future perfect",
                CefrLevel = "C1",
                Type = TopicType.Grammar,
                DisplayOrder = 1,
                IsActive = true,
                Keywords = "perfect tenses, pluperfect, habia, future perfect, habra"
            },
            new CurriculumTopic
            {
                Name = "Literary Spanish",
                Description = "Reading and analyzing Spanish literature",
                CefrLevel = "C1",
                Type = TopicType.Reading,
                DisplayOrder = 2,
                IsActive = true,
                Keywords = "literature, literatura, novels, poetry, authors, literary analysis"
            },
            new CurriculumTopic
            {
                Name = "Academic Writing",
                Description = "Formal writing, essays, and academic style",
                CefrLevel = "C1",
                Type = TopicType.Writing,
                DisplayOrder = 3,
                IsActive = true,
                Keywords = "academic writing, essays, formal style, thesis, argumentation"
            },
            new CurriculumTopic
            {
                Name = "Regional Dialects",
                Description = "Variations in Spanish across different regions",
                CefrLevel = "C1",
                Type = TopicType.Culture,
                DisplayOrder = 4,
                IsActive = true,
                Keywords = "dialects, regional Spanish, voseo, Castilian, Latin American Spanish"
            },

            // C2 - Mastery Topics
            new CurriculumTopic
            {
                Name = "Nuanced Expression",
                Description = "Subtle meanings, register, and stylistic choices",
                CefrLevel = "C2",
                Type = TopicType.Grammar,
                DisplayOrder = 1,
                IsActive = true,
                Keywords = "nuance, register, formal, informal, stylistic, subtle meanings"
            },
            new CurriculumTopic
            {
                Name = "Professional Spanish",
                Description = "Business, legal, and technical Spanish",
                CefrLevel = "C2",
                Type = TopicType.Vocabulary,
                DisplayOrder = 2,
                IsActive = true,
                Keywords = "business Spanish, legal, technical, professional, specialized vocabulary"
            },
            new CurriculumTopic
            {
                Name = "Translation & Interpretation",
                Description = "Techniques for translating between English and Spanish",
                CefrLevel = "C2",
                Type = TopicType.Writing,
                DisplayOrder = 3,
                IsActive = true,
                Keywords = "translation, interpretation, bilingual, false friends, equivalence"
            }
        };

        context.CurriculumTopics.AddRange(topics);
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

        // Set CEFR levels for test students
        if (johnDoe != null && string.IsNullOrEmpty(johnDoe.CefrLevel))
        {
            johnDoe.CefrLevel = "B1";
            await userManager.UpdateAsync(johnDoe);
        }
        if (janeSmith != null && string.IsNullOrEmpty(janeSmith.CefrLevel))
        {
            janeSmith.CefrLevel = "A2";
            await userManager.UpdateAsync(janeSmith);
        }
        if (bobWilson != null && string.IsNullOrEmpty(bobWilson.CefrLevel))
        {
            // Bob has no level - good for testing placement test
            bobWilson.CefrLevel = null;
            await userManager.UpdateAsync(bobWilson);
        }

        // Seed approved assignments for content library testing
        var adminUser = await userManager.FindByEmailAsync("admin@lakecountryspanish.com");
        if (!context.Assignments.Any() && adminUser != null)
        {
            await SeedApprovedAssignmentsAsync(context, adminUser.Id);
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

    private static async Task SeedApprovedAssignmentsAsync(ApplicationDbContext context, string adminId)
    {
        // Get curriculum topics for linking
        var a1GrammarTopic = await context.CurriculumTopics.FirstOrDefaultAsync(
            t => t.CefrLevel == "A1" && t.Type == TopicType.Grammar);
        var a1VocabTopic = await context.CurriculumTopics.FirstOrDefaultAsync(
            t => t.CefrLevel == "A1" && t.Type == TopicType.Vocabulary);
        var a2GrammarTopic = await context.CurriculumTopics.FirstOrDefaultAsync(
            t => t.CefrLevel == "A2" && t.Type == TopicType.Grammar);
        var b1GrammarTopic = await context.CurriculumTopics.FirstOrDefaultAsync(
            t => t.CefrLevel == "B1" && t.Type == TopicType.Grammar);

        var assignments = new List<Assignment>
        {
            // A1 Multiple Choice - Approved (content library)
            new Assignment
            {
                Title = "A1 Ser vs Estar Quiz",
                Description = "Practice distinguishing between ser and estar with common scenarios.",
                CefrLevel = "A1",
                Type = AssignmentType.MultipleChoice,
                Status = AssignmentStatus.Approved,
                QuestionsJson = @"[
                    { ""id"": 1, ""question"": ""Mi madre _____ doctora."", ""options"": [""es"", ""está"", ""ser"", ""estar""] },
                    { ""id"": 2, ""question"": ""El café _____ caliente."", ""options"": [""es"", ""está"", ""son"", ""están""] },
                    { ""id"": 3, ""question"": ""Nosotros _____ de España."", ""options"": [""somos"", ""estamos"", ""es"", ""está""] },
                    { ""id"": 4, ""question"": ""La fiesta _____ en mi casa."", ""options"": [""es"", ""está"", ""son"", ""están""] },
                    { ""id"": 5, ""question"": ""Mi hermana _____ cansada hoy."", ""options"": [""es"", ""está"", ""ser"", ""estar""] }
                ]",
                AnswersJson = @"[
                    { ""id"": 1, ""answer"": ""es"", ""explanation"": ""Use 'ser' for professions."" },
                    { ""id"": 2, ""answer"": ""está"", ""explanation"": ""Use 'estar' for temporary conditions like temperature."" },
                    { ""id"": 3, ""answer"": ""somos"", ""explanation"": ""Use 'ser' for origin/nationality."" },
                    { ""id"": 4, ""answer"": ""es"", ""explanation"": ""Use 'ser' for event locations."" },
                    { ""id"": 5, ""answer"": ""está"", ""explanation"": ""Use 'estar' for temporary feelings/states."" }
                ]",
                EstimatedMinutes = 10,
                TotalPoints = 25,
                BonusPoints = 5,
                CurriculumTopicId = a1GrammarTopic?.Id,
                CreatedById = adminId,
                ReviewedById = adminId,
                ReviewedAt = DateTime.UtcNow.AddDays(-7),
                ReviewNotes = "Good variety of ser vs estar usage scenarios.",
                CreatedAt = DateTime.UtcNow.AddDays(-14),
                IsFromLibrary = false
            },

            // A1 Fill in the Blank - Approved (content library)
            new Assignment
            {
                Title = "A1 Basic Greetings Practice",
                Description = "Complete the greetings with the correct Spanish words.",
                CefrLevel = "A1",
                Type = AssignmentType.FillInTheBlank,
                Status = AssignmentStatus.Approved,
                QuestionsJson = @"[
                    { ""id"": 1, ""question"": ""_____ días! ¿Cómo estás?"", ""hint"": ""Good (masculine plural)"" },
                    { ""id"": 2, ""question"": ""Me _____ Juan."", ""hint"": ""I call myself"" },
                    { ""id"": 3, ""question"": ""_____ gusto en conocerte."", ""hint"": ""A lot / Much"" },
                    { ""id"": 4, ""question"": ""¿De dónde _____?"", ""hint"": ""Are you (informal) from?"" }
                ]",
                AnswersJson = @"[
                    { ""id"": 1, ""answer"": ""Buenos"", ""acceptableAnswers"": [""buenos"", ""Buenos""] },
                    { ""id"": 2, ""answer"": ""llamo"", ""acceptableAnswers"": [""llamo"", ""Llamo""] },
                    { ""id"": 3, ""answer"": ""Mucho"", ""acceptableAnswers"": [""mucho"", ""Mucho""] },
                    { ""id"": 4, ""answer"": ""eres"", ""acceptableAnswers"": [""eres"", ""es""] }
                ]",
                EstimatedMinutes = 8,
                TotalPoints = 20,
                BonusPoints = 4,
                CurriculumTopicId = a1VocabTopic?.Id,
                CreatedById = adminId,
                ReviewedById = adminId,
                ReviewedAt = DateTime.UtcNow.AddDays(-5),
                ReviewNotes = "Clear and appropriate for A1 level.",
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                IsFromLibrary = false
            },

            // A2 Multiple Choice - Approved (content library)
            new Assignment
            {
                Title = "A2 Preterite Tense Quiz",
                Description = "Practice conjugating regular verbs in the preterite (past) tense.",
                CefrLevel = "A2",
                Type = AssignmentType.MultipleChoice,
                Status = AssignmentStatus.Approved,
                QuestionsJson = @"[
                    { ""id"": 1, ""question"": ""Ayer yo _____ al supermercado. (ir)"", ""options"": [""fui"", ""fue"", ""iba"", ""voy""] },
                    { ""id"": 2, ""question"": ""Ellos _____ la cena anoche. (preparar)"", ""options"": [""prepararon"", ""preparaban"", ""preparan"", ""preparar""] },
                    { ""id"": 3, ""question"": ""María _____ un libro la semana pasada. (leer)"", ""options"": [""leyó"", ""leía"", ""lee"", ""leyeron""] },
                    { ""id"": 4, ""question"": ""Nosotros _____ español en la escuela. (estudiar)"", ""options"": [""estudiamos"", ""estudiábamos"", ""estudian"", ""estudió""] },
                    { ""id"": 5, ""question"": ""¿Tú _____ la película? (ver)"", ""options"": [""viste"", ""veías"", ""ves"", ""vio""] }
                ]",
                AnswersJson = @"[
                    { ""id"": 1, ""answer"": ""fui"", ""explanation"": ""'Fui' is first person singular preterite of 'ir'."" },
                    { ""id"": 2, ""answer"": ""prepararon"", ""explanation"": ""Third person plural preterite of -ar verb."" },
                    { ""id"": 3, ""answer"": ""leyó"", ""explanation"": ""Third person singular preterite of 'leer'."" },
                    { ""id"": 4, ""answer"": ""estudiamos"", ""explanation"": ""First person plural preterite of -ar verb."" },
                    { ""id"": 5, ""answer"": ""viste"", ""explanation"": ""Second person singular preterite of 'ver'."" }
                ]",
                EstimatedMinutes = 12,
                TotalPoints = 30,
                BonusPoints = 5,
                CurriculumTopicId = a2GrammarTopic?.Id,
                CreatedById = adminId,
                ReviewedById = adminId,
                ReviewedAt = DateTime.UtcNow.AddDays(-3),
                ReviewNotes = "Good mix of regular and irregular preterite forms.",
                CreatedAt = DateTime.UtcNow.AddDays(-7),
                IsFromLibrary = false
            },

            // B1 Multiple Choice - Approved (content library)
            new Assignment
            {
                Title = "B1 Preterite vs Imperfect",
                Description = "Choose the correct past tense based on context.",
                CefrLevel = "B1",
                Type = AssignmentType.MultipleChoice,
                Status = AssignmentStatus.Approved,
                QuestionsJson = @"[
                    { ""id"": 1, ""question"": ""Cuando era niño, _____ al parque todos los días."", ""options"": [""iba"", ""fui"", ""fue"", ""ir""] },
                    { ""id"": 2, ""question"": ""Ayer _____ al médico porque me sentía mal."", ""options"": [""fui"", ""iba"", ""fue"", ""ir""] },
                    { ""id"": 3, ""question"": ""Mientras yo _____ la televisión, sonó el teléfono."", ""options"": [""veía"", ""vi"", ""vio"", ""ver""] },
                    { ""id"": 4, ""question"": ""Mi abuela siempre _____ galletas para nosotros."", ""options"": [""hacía"", ""hizo"", ""hace"", ""hacer""] },
                    { ""id"": 5, ""question"": ""El año pasado _____ a España por primera vez."", ""options"": [""viajé"", ""viajaba"", ""viajo"", ""viajar""] }
                ]",
                AnswersJson = @"[
                    { ""id"": 1, ""answer"": ""iba"", ""explanation"": ""Imperfect for habitual past actions."" },
                    { ""id"": 2, ""answer"": ""fui"", ""explanation"": ""Preterite for completed action at specific time."" },
                    { ""id"": 3, ""answer"": ""veía"", ""explanation"": ""Imperfect for ongoing background action."" },
                    { ""id"": 4, ""answer"": ""hacía"", ""explanation"": ""Imperfect for habitual actions with 'siempre'."" },
                    { ""id"": 5, ""answer"": ""viajé"", ""explanation"": ""Preterite for single completed event."" }
                ]",
                EstimatedMinutes = 15,
                TotalPoints = 35,
                BonusPoints = 7,
                CurriculumTopicId = b1GrammarTopic?.Id,
                CreatedById = adminId,
                ReviewedById = adminId,
                ReviewedAt = DateTime.UtcNow.AddDays(-2),
                ReviewNotes = "Excellent contrast between preterite and imperfect contexts.",
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                IsFromLibrary = false
            },

            // A1 Reading - Pending Review (not in library)
            new Assignment
            {
                Title = "A1 Mi Familia Reading",
                Description = "Read about a Spanish family and answer comprehension questions.",
                CefrLevel = "A1",
                Type = AssignmentType.Reading,
                Status = AssignmentStatus.PendingReview,
                QuestionsJson = @"[
                    { ""id"": 0, ""type"": ""passage"", ""text"": ""Hola, me llamo Carlos. Tengo una familia pequeña. Mi madre se llama Ana y mi padre se llama Pedro. Tengo una hermana. Se llama María. María tiene doce años y yo tengo quince años. Nosotros vivimos en Madrid. Tenemos un perro. Se llama Toby."" },
                    { ""id"": 1, ""question"": ""¿Cómo se llama el padre de Carlos?"" },
                    { ""id"": 2, ""question"": ""¿Cuántos años tiene Carlos?"" },
                    { ""id"": 3, ""question"": ""¿Dónde vive la familia?"" }
                ]",
                AnswersJson = @"[
                    { ""id"": 1, ""answer"": ""Pedro"", ""acceptableAnswers"": [""pedro"", ""Pedro"", ""Se llama Pedro""] },
                    { ""id"": 2, ""answer"": ""quince"", ""acceptableAnswers"": [""15"", ""quince"", ""Quince""] },
                    { ""id"": 3, ""answer"": ""Madrid"", ""acceptableAnswers"": [""madrid"", ""Madrid"", ""En Madrid""] }
                ]",
                EstimatedMinutes = 12,
                TotalPoints = 25,
                BonusPoints = 5,
                CurriculumTopicId = a1VocabTopic?.Id,
                CreatedById = adminId,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                IsFromLibrary = false
            }
        };

        context.Assignments.AddRange(assignments);
        await context.SaveChangesAsync();
    }
}
