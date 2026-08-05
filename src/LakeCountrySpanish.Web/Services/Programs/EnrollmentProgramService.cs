using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace LakeCountrySpanish.Web.Services.Programs;

public sealed class EnrollmentProgramService : IEnrollmentProgramService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EnrollmentProgramService> _logger;

    public EnrollmentProgramService(ApplicationDbContext context, ILogger<EnrollmentProgramService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<EnrollmentProgram>> ListAllAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        var q = _context.Programs.AsNoTracking().AsQueryable();
        if (!includeInactive) q = q.Where(p => p.IsActive);
        return await q.OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
    }

    public Task<EnrollmentProgram?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var normalized = slug?.Trim().ToLowerInvariant() ?? string.Empty;
        return _context.Programs
            .FirstOrDefaultAsync(p => p.Slug.ToLower() == normalized, ct);
    }

    public Task<EnrollmentProgram?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _context.Programs.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<EnrollmentProgram> CreateAsync(EnrollmentProgram program, CancellationToken ct = default)
    {
        // Basic invariants — the admin form should have caught these already,
        // but service-layer defence in depth is cheap.
        NormalizeSlug(program);
        if (program.FullPrice <= 0)
            throw new InvalidOperationException("FullPrice must be positive.");
        if (program.InstallmentCount < 2)
            program.InstallmentCount = 2;

        program.CreatedAt = DateTime.UtcNow;
        program.UpdatedAt = null;

        _context.Programs.Add(program);
        await _context.SaveChangesAsync(ct);

        try
        {
            await ProvisionStripeAsync(program, ct);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe provisioning failed for new Program {ProgramId} ({Slug}); rolling back the DB row.", program.Id, program.Slug);
            _context.Programs.Remove(program);
            await _context.SaveChangesAsync(ct);
            throw new InvalidOperationException("Stripe provisioning failed: " + ex.Message, ex);
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Created EnrollmentProgram {ProgramId} ({Slug}) with Stripe product {ProductId}", program.Id, program.Slug, program.StripeProductId);
        return program;
    }

    public async Task<EnrollmentProgram> UpdateAsync(EnrollmentProgram program, CancellationToken ct = default)
    {
        var existing = await _context.Programs.FirstOrDefaultAsync(p => p.Id == program.Id, ct)
            ?? throw new InvalidOperationException($"Program {program.Id} not found.");

        // Once Stripe knows about this program, price-affecting fields freeze.
        var stripeIsWired = !string.IsNullOrEmpty(existing.StripeProductId);
        if (stripeIsWired)
        {
            if (existing.FullPrice != program.FullPrice)
                throw new InvalidOperationException("FullPrice cannot change after the program is created (Stripe Prices are immutable). Duplicate this program to change pricing.");

            if (existing.InstallmentCount != program.InstallmentCount)
                throw new InvalidOperationException("InstallmentCount cannot change after the program is created. Duplicate this program to change the installment plan.");

            // Turning installments OFF is fine (parents just don't see the option).
            // Turning them back ON at a different implied installment amount is not.
            if (!existing.InstallmentsEnabled && program.InstallmentsEnabled)
                throw new InvalidOperationException("Installments cannot be re-enabled on a program that was created without them. Duplicate this program to add an installment plan.");
        }

        NormalizeSlug(program);

        // Copy the mutable metadata fields over — leave StripeProductId/PriceIds untouched.
        existing.Slug = program.Slug;
        existing.Name = program.Name;
        existing.TagLine = program.TagLine;
        existing.Description = program.Description;
        existing.HeroImagePath = program.HeroImagePath;
        existing.LocationName = program.LocationName;
        existing.LocationAddress = program.LocationAddress;
        existing.StartDate = program.StartDate;
        existing.EndDate = program.EndDate;
        existing.MeetingDays = program.MeetingDays;
        existing.StartTime = program.StartTime;
        existing.EndTime = program.EndTime;
        existing.GradeRange = program.GradeRange;
        existing.AgeMin = program.AgeMin;
        existing.AgeMax = program.AgeMax;
        existing.InstallmentsEnabled = program.InstallmentsEnabled;
        existing.FinalInstallmentDueDate = program.FinalInstallmentDueDate;
        existing.CashOptionEnabled = program.CashOptionEnabled;
        existing.WaiverText = program.WaiverText;
        existing.RefundPolicyText = program.RefundPolicyText;
        existing.ContactPhone = program.ContactPhone;
        existing.ContactEmail = program.ContactEmail;
        existing.IsActive = program.IsActive;
        existing.IsListed = program.IsListed;
        existing.UpdatedAt = DateTime.UtcNow;

        // Locked-but-passed-through so caller sees the true persisted values.
        program.FullPrice = existing.FullPrice;
        program.InstallmentCount = existing.InstallmentCount;

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Updated EnrollmentProgram {ProgramId} ({Slug})", existing.Id, existing.Slug);
        return existing;
    }

    // ---------------- Stripe provisioning ----------------

    /// <summary>
    /// Creates the Stripe Product plus a one-time Full Price and (if installments
    /// enabled) a recurring monthly Installment Price. Idempotency keys are
    /// scoped by program id so a retried Create is safe.
    /// </summary>
    private async Task ProvisionStripeAsync(EnrollmentProgram program, CancellationToken ct)
    {
        var productSvc = new ProductService();
        var priceSvc = new PriceService();

        var product = await productSvc.CreateAsync(new ProductCreateOptions
        {
            Name = program.Name,
            Description = string.IsNullOrWhiteSpace(program.TagLine) ? null : program.TagLine,
            Metadata = new Dictionary<string, string>
            {
                { "lcs.enrollmentProgramId", program.Id.ToString() },
                { "lcs.slug", program.Slug }
            }
        }, new RequestOptions { IdempotencyKey = $"lcs-program-{program.Id}-product" }, ct);

        program.StripeProductId = product.Id;

        var fullPrice = await priceSvc.CreateAsync(new PriceCreateOptions
        {
            Product = product.Id,
            Currency = "usd",
            UnitAmount = ToCents(program.FullPrice),
            Nickname = "Full one-time",
            Metadata = new Dictionary<string, string>
            {
                { "lcs.enrollmentProgramId", program.Id.ToString() },
                { "lcs.priceRole", "full" }
            }
        }, new RequestOptions { IdempotencyKey = $"lcs-program-{program.Id}-price-full" }, ct);

        program.StripeFullPriceId = fullPrice.Id;

        if (program.InstallmentsEnabled)
        {
            var installmentPrice = await priceSvc.CreateAsync(new PriceCreateOptions
            {
                Product = product.Id,
                Currency = "usd",
                UnitAmount = ToCents(program.InstallmentAmount),
                Nickname = $"Installment ({program.InstallmentCount}x monthly)",
                Recurring = new PriceRecurringOptions
                {
                    // Monthly cadence with cancel_at set on the subscription
                    // means Stripe auto-charges once at signup + once ~30 days
                    // later, then auto-cancels. No scheduled task on our side.
                    Interval = "month",
                    IntervalCount = 1
                },
                Metadata = new Dictionary<string, string>
                {
                    { "lcs.enrollmentProgramId", program.Id.ToString() },
                    { "lcs.priceRole", "installment" },
                    { "lcs.installmentCount", program.InstallmentCount.ToString() }
                }
            }, new RequestOptions { IdempotencyKey = $"lcs-program-{program.Id}-price-installment" }, ct);

            program.StripeInstallmentPriceId = installmentPrice.Id;
        }
    }

    private static long ToCents(decimal amount) => (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    private static void NormalizeSlug(EnrollmentProgram program)
    {
        program.Slug = (program.Slug ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(program.Slug))
            throw new InvalidOperationException("Slug is required.");
    }
}
