using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SpanishScheduler.Web.Models.Entities;

namespace SpanishScheduler.Web.Data;

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
    }
}
