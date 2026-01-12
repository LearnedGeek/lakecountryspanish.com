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
                .WithMany()
                .HasForeignKey(e => e.ScheduledClassId)
                .OnDelete(DeleteBehavior.SetNull);
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
    }
}
