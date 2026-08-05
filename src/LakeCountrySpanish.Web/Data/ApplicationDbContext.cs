using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();
    public DbSet<ScheduledClass> ScheduledClasses => Set<ScheduledClass>();
    public DbSet<Package> Packages => Set<Package>();
    public DbSet<StudentPackage> StudentPackages => Set<StudentPackage>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<StudentDocument> StudentDocuments => Set<StudentDocument>();
    public DbSet<ContactInquiry> ContactInquiries => Set<ContactInquiry>();
    public DbSet<BlockedDate> BlockedDates => Set<BlockedDate>();
    public DbSet<ClassFeedback> ClassFeedbacks => Set<ClassFeedback>();
    public DbSet<Tip> Tips => Set<Tip>();
    public DbSet<Testimonial> Testimonials => Set<Testimonial>();

    // Subscription entities
    public DbSet<SubscriptionTier> SubscriptionTiers => Set<SubscriptionTier>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<RecurringSchedule> RecurringSchedules => Set<RecurringSchedule>();
    public DbSet<SubscriptionHistory> SubscriptionHistory => Set<SubscriptionHistory>();

    // Token entities
    public DbSet<Token> Tokens => Set<Token>();
    public DbSet<TokenPurchasePermission> TokenPurchasePermissions => Set<TokenPurchasePermission>();
    public DbSet<TokenTransaction> TokenTransactions => Set<TokenTransaction>();

    // Gamification entities
    public DbSet<Badge> Badges => Set<Badge>();
    public DbSet<StudentBadge> StudentBadges => Set<StudentBadge>();
    public DbSet<StudentStreak> StudentStreaks => Set<StudentStreak>();
    public DbSet<PointTransaction> PointTransactions => Set<PointTransaction>();

    // Ticket entities
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketRedemption> TicketRedemptions => Set<TicketRedemption>();

    // Assignment entities
    public DbSet<CurriculumTopic> CurriculumTopics => Set<CurriculumTopic>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<StudentAssignment> StudentAssignments => Set<StudentAssignment>();
    public DbSet<AssignmentSubmission> AssignmentSubmissions => Set<AssignmentSubmission>();

    // Placement test entities
    public DbSet<PlacementTestSession> PlacementTestSessions => Set<PlacementTestSession>();

    // Notification entities
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

    // Curriculum entities (Phase 3)
    public DbSet<WisconsinStandard> WisconsinStandards => Set<WisconsinStandard>();
    public DbSet<Period> Periods => Set<Period>();
    public DbSet<LearningPath> LearningPaths => Set<LearningPath>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Day> Days => Set<Day>();
    public DbSet<ArtifactLibrary> ArtifactLibrary => Set<ArtifactLibrary>();
    public DbSet<TeacherClassAssignment> TeacherClassAssignments => Set<TeacherClassAssignment>();
    public DbSet<CurriculumVersion> CurriculumVersions => Set<CurriculumVersion>();
    public DbSet<BinderComposition> BinderCompositions => Set<BinderComposition>();
    public DbSet<BinderGeneration> BinderGenerations => Set<BinderGeneration>();

    // Media library entities (Phase 3)
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<MediaUsage> MediaUsages => Set<MediaUsage>();

    // Lesson video table + shortlink redirector (docx-upload pipeline).
    public DbSet<LessonVideo> LessonVideos => Set<LessonVideo>();
    public DbSet<Shortlink> Shortlinks => Set<Shortlink>();

    // Program enrollment (unlisted /join/{slug} landing pages for open houses).
    public DbSet<EnrollmentProgram> Programs => Set<EnrollmentProgram>();
    public DbSet<ProgramEnrollment> ProgramEnrollments => Set<ProgramEnrollment>();
    public DbSet<ProgramEnrollmentAuditEvent> ProgramEnrollmentAuditEvents => Set<ProgramEnrollmentAuditEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ApplicationUser configuration
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(e => e.CustomHourlyRate).HasColumnType("decimal(18,2)");
        });

        // TimeSlot configuration
        builder.Entity<TimeSlot>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        // ScheduledClass configuration
        builder.Entity<ScheduledClass>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Student)
                .WithMany(u => u.ScheduledClasses)
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TimeSlot)
                .WithMany(t => t.ScheduledClasses)
                .HasForeignKey(e => e.TimeSlotId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Payment)
                .WithMany(p => p.ScheduledClasses)
                .HasForeignKey(e => e.PaymentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.StudentPackage)
                .WithMany(sp => sp.ScheduledClasses)
                .HasForeignKey(e => e.StudentPackageId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Package configuration
        builder.Entity<Package>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
        });

        // StudentPackage configuration
        builder.Entity<StudentPackage>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Student)
                .WithMany(u => u.StudentPackages)
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Package)
                .WithMany(p => p.StudentPackages)
                .HasForeignKey(e => e.PackageId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Payment)
                .WithOne(p => p.StudentPackage)
                .HasForeignKey<StudentPackage>(e => e.PaymentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Payment configuration
        builder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");

            entity.HasOne(e => e.Student)
                .WithMany(u => u.Payments)
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Unique index on StripePaymentIntentId to prevent duplicate processing
            // Filtered to only include non-null values (allowing multiple null values)
            entity.HasIndex(e => e.StripePaymentIntentId)
                .IsUnique()
                .HasFilter("\"StripePaymentIntentId\" IS NOT NULL");

            // Unique index on StripeSessionId for efficient lookup
            entity.HasIndex(e => e.StripeSessionId)
                .HasFilter("\"StripeSessionId\" IS NOT NULL");
        });

        // Document configuration
        builder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        // StudentDocument configuration
        builder.Entity<StudentDocument>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Student)
                .WithMany(u => u.StudentDocuments)
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Document)
                .WithMany(d => d.StudentDocuments)
                .HasForeignKey(e => e.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ContactInquiry configuration
        builder.Entity<ContactInquiry>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        // BlockedDate configuration
        builder.Entity<BlockedDate>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        // ClassFeedback configuration
        builder.Entity<ClassFeedback>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.ScheduledClass)
                .WithMany()
                .HasForeignKey(e => e.ScheduledClassId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Student)
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Ensure one feedback per class per student
            entity.HasIndex(e => new { e.ScheduledClassId, e.StudentId }).IsUnique();
        });

        // Tip configuration
        builder.Entity<Tip>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");

            entity.HasOne(e => e.Student)
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ScheduledClass)
                .WithOne(c => c.Tip)
                .HasForeignKey<Tip>(e => e.ScheduledClassId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Testimonial configuration
        builder.Entity<Testimonial>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Student)
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ReviewedBy)
                .WithMany()
                .HasForeignKey(e => e.ReviewedById)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.RelatedClass)
                .WithMany()
                .HasForeignKey(e => e.RelatedClassId)
                .OnDelete(DeleteBehavior.SetNull);

            // Index for finding approved testimonials
            entity.HasIndex(e => new { e.Status, e.IsFeatured, e.DisplayOrder });
        });

        // SubscriptionTier configuration
        builder.Entity<SubscriptionTier>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MonthlyPrice).HasColumnType("decimal(18,2)");
            entity.Property(e => e.PointsMultiplier).HasColumnType("decimal(5,2)");
        });

        // Subscription configuration
        builder.Entity<Subscription>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Student)
                .WithMany(u => u.Subscriptions)
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Tier)
                .WithMany(t => t.Subscriptions)
                .HasForeignKey(e => e.SubscriptionTierId)
                .OnDelete(DeleteBehavior.Restrict);

            // Index for finding active subscription by student
            entity.HasIndex(e => new { e.StudentId, e.Status });

            // Unique index on StripeSubscriptionId to prevent duplicate subscription processing
            // Filtered to only include non-null values (allowing multiple null values for legacy records)
            entity.HasIndex(e => e.StripeSubscriptionId)
                .IsUnique()
                .HasFilter("\"StripeSubscriptionId\" IS NOT NULL");
        });

        // RecurringSchedule configuration
        builder.Entity<RecurringSchedule>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Subscription)
                .WithMany(s => s.RecurringSchedules)
                .HasForeignKey(e => e.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.TimeSlot)
                .WithMany()
                .HasForeignKey(e => e.TimeSlotId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // SubscriptionHistory configuration
        builder.Entity<SubscriptionHistory>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Subscription)
                .WithMany(s => s.History)
                .HasForeignKey(e => e.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.SubscriptionId, e.CreatedAt });
        });

        // Update ScheduledClass for subscription relationships
        // Using NoAction to avoid cascade path issues with SQL Server
        builder.Entity<ScheduledClass>(entity =>
        {
            entity.HasOne(e => e.Subscription)
                .WithMany(s => s.ScheduledClasses)
                .HasForeignKey(e => e.SubscriptionId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.RecurringSchedule)
                .WithMany(r => r.ScheduledClasses)
                .HasForeignKey(e => e.RecurringScheduleId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Update Payment for subscription relationship
        builder.Entity<Payment>(entity =>
        {
            entity.HasOne(e => e.Subscription)
                .WithMany(s => s.Payments)
                .HasForeignKey(e => e.SubscriptionId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // TokenPurchasePermission configuration
        builder.Entity<TokenPurchasePermission>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenPrice).HasColumnType("decimal(18,2)");

            entity.HasOne(e => e.Student)
                .WithMany(u => u.TokenPurchasePermissions)
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.GrantedBy)
                .WithMany()
                .HasForeignKey(e => e.GrantedById)
                .OnDelete(DeleteBehavior.SetNull);

            // Index for finding active permission by student
            entity.HasIndex(e => new { e.StudentId, e.IsEnabled, e.ExpiresAt });
        });

        // Token configuration
        builder.Entity<Token>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Student)
                .WithMany(u => u.Tokens)
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TokenPurchasePermission)
                .WithMany(p => p.Tokens)
                .HasForeignKey(e => e.TokenPurchasePermissionId)
                .OnDelete(DeleteBehavior.SetNull);

            // Index for finding active tokens by student
            entity.HasIndex(e => new { e.StudentId, e.Source, e.QuantityRemaining });
        });

        // TokenTransaction configuration
        builder.Entity<TokenTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Student)
                .WithMany(u => u.TokenTransactions)
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Token)
                .WithMany(t => t.Transactions)
                .HasForeignKey(e => e.TokenId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ScheduledClass)
                .WithMany()
                .HasForeignKey(e => e.ScheduledClassId)
                .OnDelete(DeleteBehavior.SetNull);

            // Index for transaction history by student
            entity.HasIndex(e => new { e.StudentId, e.CreatedAt });
        });

        // Badge configuration
        builder.Entity<Badge>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.Category, e.IsActive, e.DisplayOrder });
        });

        // StudentBadge configuration
        builder.Entity<StudentBadge>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Student)
                .WithMany(u => u.StudentBadges)
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Badge)
                .WithMany(b => b.StudentBadges)
                .HasForeignKey(e => e.BadgeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Each student can only earn each badge once
            entity.HasIndex(e => new { e.StudentId, e.BadgeId }).IsUnique();
        });

        // StudentStreak configuration
        builder.Entity<StudentStreak>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Student)
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            // One streak record per student
            entity.HasIndex(e => e.StudentId).IsUnique();
        });

        // PointTransaction configuration
        builder.Entity<PointTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Multiplier).HasColumnType("decimal(5,2)");

            entity.HasOne(e => e.Student)
                .WithMany(u => u.PointTransactions)
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ScheduledClass)
                .WithMany()
                .HasForeignKey(e => e.ScheduledClassId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Badge)
                .WithMany(b => b.PointTransactions)
                .HasForeignKey(e => e.BadgeId)
                .OnDelete(DeleteBehavior.SetNull);

            // Index for point history by student
            entity.HasIndex(e => new { e.StudentId, e.CreatedAt });
        });

        // Ticket configuration
        builder.Entity<Ticket>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Student)
                .WithMany(u => u.Tickets)
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.UsedForClass)
                .WithMany()
                .HasForeignKey(e => e.UsedForClassId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Subscription)
                .WithMany()
                .HasForeignKey(e => e.SubscriptionId)
                .OnDelete(DeleteBehavior.SetNull);

            // Index for finding available tickets by student
            entity.HasIndex(e => new { e.StudentId, e.IsUsed, e.ExpiresAt });
            // Index for finding tickets by source
            entity.HasIndex(e => new { e.StudentId, e.Source });
        });

        // TicketRedemption configuration
        builder.Entity<TicketRedemption>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)");

            entity.HasOne(e => e.Ticket)
                .WithMany()
                .HasForeignKey(e => e.TicketId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Student)
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ScheduledClass)
                .WithMany()
                .HasForeignKey(e => e.ScheduledClassId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Payment)
                .WithMany()
                .HasForeignKey(e => e.PaymentId)
                .OnDelete(DeleteBehavior.SetNull);

            // Index for finding redemptions by student
            entity.HasIndex(e => new { e.StudentId, e.Type, e.RedeemedAt });
        });

        // CurriculumTopic configuration
        builder.Entity<CurriculumTopic>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.CefrLevel, e.Type, e.IsActive });
        });

        // Assignment configuration
        builder.Entity<Assignment>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.CurriculumTopic)
                .WithMany(t => t.Assignments)
                .HasForeignKey(e => e.CurriculumTopicId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ReviewedBy)
                .WithMany()
                .HasForeignKey(e => e.ReviewedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedById)
                .OnDelete(DeleteBehavior.NoAction);

            // Content library - self-referencing for cloned assignments
            entity.HasOne(e => e.SourceAssignment)
                .WithMany()
                .HasForeignKey(e => e.SourceAssignmentId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(e => new { e.CefrLevel, e.Status, e.Type });
            entity.HasIndex(e => e.SourceAssignmentId);
        });

        // StudentAssignment configuration
        builder.Entity<StudentAssignment>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.BestScore).HasPrecision(5, 2);

            entity.HasOne(e => e.Student)
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Assignment)
                .WithMany(a => a.StudentAssignments)
                .HasForeignKey(e => e.AssignmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.AssignedBy)
                .WithMany()
                .HasForeignKey(e => e.AssignedById)
                .OnDelete(DeleteBehavior.NoAction);

            // Index for finding student's assignments
            entity.HasIndex(e => new { e.StudentId, e.Status, e.Priority });

            // Prevent duplicate assignment to same student
            entity.HasIndex(e => new { e.StudentId, e.AssignmentId }).IsUnique();
        });

        // AssignmentSubmission configuration
        builder.Entity<AssignmentSubmission>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PercentageScore).HasColumnType("decimal(5,2)");

            entity.HasOne(e => e.Assignment)
                .WithMany(a => a.Submissions)
                .HasForeignKey(e => e.AssignmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Student)
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index for finding student's submissions
            entity.HasIndex(e => new { e.StudentId, e.SubmittedAt });
            entity.HasIndex(e => new { e.AssignmentId, e.StudentId });
        });

        // PlacementTestSession configuration
        builder.Entity<PlacementTestSession>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Student)
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index for finding active session by student
            entity.HasIndex(e => new { e.StudentId, e.Status });
        });

        // NotificationLog configuration
        builder.Entity<NotificationLog>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Recipient)
                .WithMany()
                .HasForeignKey(e => e.RecipientId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index for finding notifications by recipient and type
            entity.HasIndex(e => new { e.RecipientId, e.Type, e.SentAt });
            // Index for finding notifications by related entity (to prevent duplicates)
            entity.HasIndex(e => new { e.Type, e.RelatedEntityId, e.RecipientId });
        });

        // === Curriculum entities (Phase 3) ===

        // WisconsinStandard configuration
        builder.Entity<WisconsinStandard>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Code is the natural key from the WI document; must be unique
            entity.HasIndex(e => e.Code).IsUnique();

            // Common filters: by category, by proficiency band, by K4-2 applicability
            entity.HasIndex(e => new { e.Category, e.ProficiencyBand, e.ProficiencySubLevel });
            entity.HasIndex(e => e.ApplicableToK4_2);
        });

        // Period configuration
        builder.Entity<Period>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => new { e.StartDate, e.EndDate });
        });

        // LearningPath configuration
        builder.Entity<LearningPath>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.GradeBand, e.Audience, e.IsActive });
        });

        // Unit configuration — includes self-referencing many-to-many for prior units
        builder.Entity<Unit>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.LearningPath)
                .WithMany(p => p.Units)
                .HasForeignKey(e => e.LearningPathId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.LearningPathId, e.DisplayOrder });
            entity.HasIndex(e => e.Theme);

            // Self-referencing many-to-many: PriorUnits / DependentUnits.
            // EF Core 10 requires the join table to have an explicit primary
            // key configured for self-referencing many-to-many — the implicit
            // shorthand that worked in EF 8 (.UsingEntity("UnitDependencies"))
            // no longer auto-derives a composite key.
            entity.HasMany(u => u.PriorUnits)
                .WithMany(u => u.DependentUnits)
                .UsingEntity<Dictionary<string, object>>(
                    "UnitDependencies",
                    j => j.HasOne<Unit>().WithMany().HasForeignKey("DependentUnitId").OnDelete(DeleteBehavior.NoAction),
                    j => j.HasOne<Unit>().WithMany().HasForeignKey("PriorUnitId").OnDelete(DeleteBehavior.NoAction),
                    j => j.HasKey("PriorUnitId", "DependentUnitId"));
        });

        // Day configuration
        builder.Entity<Day>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Unit)
                .WithMany(u => u.Days)
                .HasForeignKey(e => e.UnitId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.UnitId, e.DayNumberInUnit });
            entity.HasIndex(e => new { e.GradeBand, e.Theme });
            entity.HasIndex(e => e.IsActive);

            // Many-to-many auto-join tables for tag-style relationships
            entity.HasMany(d => d.WisconsinStandards).WithMany();
            entity.HasMany(d => d.RecommendedArtifacts).WithMany(a => a.RecommendingDays);
            entity.HasMany(d => d.PracticeAssignments).WithMany();
        });

        // ArtifactLibrary configuration
        builder.Entity<ArtifactLibrary>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Slug is URL-safe identifier used for cross-references in lesson Markdown;
            // must be unique across the library.
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.HasIndex(e => new { e.Type, e.Subtype });
            entity.HasIndex(e => e.IsActive);

            // Many-to-many auto-join table for WI standard tags
            entity.HasMany(a => a.WisconsinStandards).WithMany();
        });

        // TeacherClassAssignment configuration
        builder.Entity<TeacherClassAssignment>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Teacher)
                .WithMany()
                .HasForeignKey(e => e.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Period)
                .WithMany(p => p.TeacherClassAssignments)
                .HasForeignKey(e => e.PeriodId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.LearningPath)
                .WithMany()
                .HasForeignKey(e => e.LearningPathId)
                .OnDelete(DeleteBehavior.Restrict);

            // Common filter: find a teacher's active assignments
            entity.HasIndex(e => new { e.TeacherId, e.IsActive });
            entity.HasIndex(e => new { e.PeriodId, e.IsActive });
        });

        // CurriculumVersion configuration
        builder.Entity<CurriculumVersion>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Look up versions by entity reference + chronologically
            entity.HasIndex(e => new { e.EntityType, e.EntityId, e.VersionNumber }).IsUnique();
            entity.HasIndex(e => new { e.EntityType, e.EntityId, e.EffectiveDate });
        });

        // BinderComposition configuration
        builder.Entity<BinderComposition>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.TeacherClassAssignment)
                .WithMany(t => t.BinderCompositions)
                .HasForeignKey(e => e.TeacherClassAssignmentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Many-to-many auto-join tables for selected content
            entity.HasMany(b => b.Days).WithMany();
            entity.HasMany(b => b.ExtraArtifacts).WithMany();

            entity.HasIndex(e => new { e.TeacherClassAssignmentId, e.IsTemplate });
        });

        // BinderGeneration configuration
        builder.Entity<BinderGeneration>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.TeacherClassAssignment)
                .WithMany(t => t.BinderGenerations)
                .HasForeignKey(e => e.TeacherClassAssignmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.BinderComposition)
                .WithMany()
                .HasForeignKey(e => e.BinderCompositionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.TeacherClassAssignmentId, e.GeneratedAt });
        });

        // === Media library entities (Phase 3) ===

        // MediaAsset configuration
        builder.Entity<MediaAsset>(entity =>
        {
            entity.HasKey(e => e.Id);

            // FileHash supports duplicate detection on upload/import
            entity.HasIndex(e => e.FileHash);
            entity.HasIndex(e => new { e.Source, e.Category });
            entity.HasIndex(e => e.UploadedById);
            entity.HasIndex(e => e.SourceId);
        });

        // MediaUsage configuration — polymorphic over consuming entity type
        builder.Entity<MediaUsage>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.MediaAsset)
                .WithMany(m => m.Usages)
                .HasForeignKey(e => e.MediaAssetId)
                .OnDelete(DeleteBehavior.Cascade);

            // Find all usages of a media asset, or all media used by an entity
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
            entity.HasIndex(e => e.MediaAssetId);
        });

        // === docx-upload pipeline: LessonVideo + Shortlink ===

        builder.Entity<LessonVideo>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Url).HasMaxLength(500).IsRequired();

            entity.HasOne(e => e.Day)
                .WithMany(d => d.Videos)
                .HasForeignKey(e => e.DayId)
                .OnDelete(DeleteBehavior.Cascade);

            // Common query: list videos for a Day in display order.
            entity.HasIndex(e => new { e.DayId, e.DisplayOrder });
        });

        builder.Entity<Shortlink>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Code).HasMaxLength(3).IsRequired();

            // Code is the lookup key — must be unique across all shortlinks.
            entity.HasIndex(e => e.Code).IsUnique();
            // Find all shortlinks targeting a specific entity (admin cleanup).
            entity.HasIndex(e => new { e.DestinationType, e.DestinationId });
        });

        // Day: slug must be unique across all Days (public route segment).
        builder.Entity<Day>(entity =>
        {
            entity.Property(e => e.Slug).HasMaxLength(120);
            entity.HasIndex(e => e.Slug)
                .IsUnique()
                .HasFilter("\"Slug\" IS NOT NULL AND \"Slug\" <> ''");
        });

        // === EnrollmentProgram + ProgramEnrollment ===

        builder.Entity<EnrollmentProgram>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Slug).HasMaxLength(80).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(160).IsRequired();
            entity.Property(e => e.TagLine).HasMaxLength(200);
            entity.Property(e => e.LocationName).HasMaxLength(120);
            entity.Property(e => e.LocationAddress).HasMaxLength(240);
            entity.Property(e => e.MeetingDays).HasMaxLength(80);
            entity.Property(e => e.GradeRange).HasMaxLength(20);
            entity.Property(e => e.ContactPhone).HasMaxLength(20);
            entity.Property(e => e.ContactEmail).HasMaxLength(120);

            // FullPrice controls billing; store as decimal(18,2) to match other money columns.
            entity.Property(e => e.FullPrice).HasColumnType("decimal(18,2)");

            // Slug is the public URL segment — must be unique.
            entity.HasIndex(e => e.Slug).IsUnique();

            // Common admin queries: list active programs; filter by listed/unlisted.
            entity.HasIndex(e => new { e.IsActive, e.IsListed });
        });

        builder.Entity<ProgramEnrollment>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Program)
                .WithMany(p => p.Enrollments)
                .HasForeignKey(e => e.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(e => e.ParentFirstName).HasMaxLength(80).IsRequired();
            entity.Property(e => e.ParentLastName).HasMaxLength(80).IsRequired();
            entity.Property(e => e.ParentEmail).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ParentPhone).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ParentAddressLine1).HasMaxLength(200);
            entity.Property(e => e.ParentCity).HasMaxLength(80);
            entity.Property(e => e.ParentState).HasMaxLength(20);
            entity.Property(e => e.ParentZip).HasMaxLength(20);

            entity.Property(e => e.StudentFirstName).HasMaxLength(80).IsRequired();
            entity.Property(e => e.StudentLastName).HasMaxLength(80).IsRequired();
            entity.Property(e => e.StudentGrade).HasMaxLength(20);

            entity.Property(e => e.EmergencyName).HasMaxLength(160);
            entity.Property(e => e.EmergencyPhone).HasMaxLength(20);
            entity.Property(e => e.EmergencyRelationship).HasMaxLength(60);

            entity.Property(e => e.TotalAmountPaid).HasColumnType("decimal(18,2)");

            // Admin views the roster per program, sorted by signup order.
            entity.HasIndex(e => new { e.ProgramId, e.CreatedAt });
            // Quick lookup by Stripe session id when a webhook or return handler arrives.
            entity.HasIndex(e => e.StripeCheckoutSessionId)
                .HasFilter("\"StripeCheckoutSessionId\" IS NOT NULL");
            // Same for subscription id (installment path).
            entity.HasIndex(e => e.StripeSubscriptionId)
                .HasFilter("\"StripeSubscriptionId\" IS NOT NULL");
        });

        builder.Entity<ProgramEnrollmentAuditEvent>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Enrollment)
                .WithMany(en => en.AuditEvents)
                .HasForeignKey(e => e.EnrollmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.ActorUserId).HasMaxLength(450);
            entity.Property(e => e.ActorDisplayName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Details).HasMaxLength(2000);
            entity.Property(e => e.MonetaryDelta).HasColumnType("decimal(18,2)");

            // Common read: full history for one enrollment, most recent first.
            entity.HasIndex(e => new { e.EnrollmentId, e.OccurredAt });
        });
    }
}
