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

    public async Task<EnrollmentProgram> CreateAsync(EnrollmentProgram program, CancellationToken ct = default, bool provisionStripe = true)
    {
        NormalizeSlug(program);

        // FullPrice must be sane whenever we're provisioning Stripe (Stripe
        // Prices need a positive unit amount). For a pure draft save, allow 0
        // as "not yet decided" — publish path will validate before flipping
        // active.
        if (provisionStripe && program.FullPrice <= 0)
            throw new InvalidOperationException("FullPrice must be positive.");
        if (program.InstallmentCount < 2)
            program.InstallmentCount = 2;

        program.CreatedAt = DateTime.UtcNow;
        program.UpdatedAt = null;

        _context.Programs.Add(program);
        await _context.SaveChangesAsync(ct);

        if (!provisionStripe)
        {
            _logger.LogInformation("Created draft EnrollmentProgram {ProgramId} ({Slug}) — no Stripe provisioning yet.", program.Id, program.Slug);
            return program;
        }

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

        // Preserve the system + Stripe fields the caller shouldn't touch —
        // SetValues would otherwise wipe them because the form-bound program
        // object has null / default values for these. Everything else on the
        // entity flows through automatically, so adding a new mutable field
        // needs no change here.
        var stripeProductId = existing.StripeProductId;
        var stripeFullPriceId = existing.StripeFullPriceId;
        var stripeInstallmentPriceId = existing.StripeInstallmentPriceId;
        var createdAt = existing.CreatedAt;

        _context.Entry(existing).CurrentValues.SetValues(program);

        existing.StripeProductId = stripeProductId;
        existing.StripeFullPriceId = stripeFullPriceId;
        existing.StripeInstallmentPriceId = stripeInstallmentPriceId;
        existing.CreatedAt = createdAt;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Updated EnrollmentProgram {ProgramId} ({Slug})", existing.Id, existing.Slug);
        return existing;
    }

    public async Task DeleteAsync(int programId, CancellationToken ct = default)
    {
        var program = await _context.Programs.FirstOrDefaultAsync(p => p.Id == programId, ct)
            ?? throw new InvalidOperationException($"Program {programId} not found.");

        // Enforce the "no delete once anyone signed up" rule at the service layer
        // so both admin form clicks and any future API path get the same guard.
        var enrollmentCount = await _context.ProgramEnrollments.CountAsync(e => e.ProgramId == programId, ct);
        if (enrollmentCount > 0)
        {
            throw new InvalidOperationException(
                $"Cannot delete \"{program.Name}\" — it has {enrollmentCount} enrollment(s). Archive it instead by editing the program and unchecking Active + Listed.");
        }

        // Archive (not delete) the Stripe Product. Stripe products with a
        // pricing history can only be archived, and even fresh ones we prefer
        // to archive so ids stay resolvable in reporting. Best-effort — a
        // Stripe outage shouldn't block Karen from cleaning up her admin list.
        if (!string.IsNullOrEmpty(program.StripeProductId))
        {
            try
            {
                var productSvc = new ProductService();
                await productSvc.UpdateAsync(
                    program.StripeProductId,
                    new ProductUpdateOptions { Active = false },
                    cancellationToken: ct);
                _logger.LogInformation("Archived Stripe product {ProductId} for deleted program {ProgramId} ({Slug})", program.StripeProductId, program.Id, program.Slug);
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(ex, "Could not archive Stripe product {ProductId} for deleted program {ProgramId} — continuing with DB delete anyway.", program.StripeProductId, program.Id);
            }
        }

        _context.Programs.Remove(program);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Deleted EnrollmentProgram {ProgramId} ({Slug})", program.Id, program.Slug);
    }

    public async Task<EnrollmentProgram> PublishAsync(int programId, CancellationToken ct = default)
    {
        var program = await _context.Programs.FirstOrDefaultAsync(p => p.Id == programId, ct)
            ?? throw new InvalidOperationException($"Program {programId} not found.");

        // Defence in depth — the ViewModel's Validate() runs first at the
        // controller layer, but re-check the pricing invariant here so any
        // future callers of this method can't accidentally publish a $0 program.
        if (program.FullPrice <= 0)
            throw new InvalidOperationException("Full price must be positive before publishing.");
        if (program.InstallmentCount < 2)
            program.InstallmentCount = 2;

        // Idempotent: if Stripe is already wired (a previously-published
        // program being re-activated after IsActive was toggled off), skip
        // provisioning and just flip active.
        if (string.IsNullOrEmpty(program.StripeProductId))
        {
            try
            {
                await ProvisionStripeAsync(program, ct);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe provisioning failed while publishing Program {ProgramId} ({Slug}); leaving as draft.", program.Id, program.Slug);
                throw new InvalidOperationException("Stripe provisioning failed: " + ex.Message, ex);
            }
        }

        program.IsActive = true;
        program.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Published EnrollmentProgram {ProgramId} ({Slug}) — Stripe product {ProductId}", program.Id, program.Slug, program.StripeProductId);
        return program;
    }

    public async Task<EnrollmentProgram> DuplicateAsync(int sourceProgramId, CancellationToken ct = default)
    {
        var source = await _context.Programs.AsNoTracking().FirstOrDefaultAsync(p => p.Id == sourceProgramId, ct)
            ?? throw new InvalidOperationException($"Program {sourceProgramId} not found.");

        var newSlug = await FindAvailableSlugAsync($"{source.Slug}-copy", ct);

        var copy = new EnrollmentProgram
        {
            // Everything content-level copies over
            Slug = newSlug,
            Name = source.Name,
            TagLine = source.TagLine,
            Description = source.Description,
            HeroImagePath = source.HeroImagePath,
            EventImagePath = source.EventImagePath,
            LocationName = source.LocationName,
            LocationAddress = source.LocationAddress,
            StartDate = source.StartDate,
            EndDate = source.EndDate,
            EnrollmentStartsAt = source.EnrollmentStartsAt,
            EnrollmentDeadline = source.EnrollmentDeadline,
            MeetingDays = source.MeetingDays,
            StartTime = source.StartTime,
            EndTime = source.EndTime,
            GradeRange = source.GradeRange,
            AgeMin = source.AgeMin,
            AgeMax = source.AgeMax,
            FullPrice = source.FullPrice,
            InstallmentsEnabled = source.InstallmentsEnabled,
            InstallmentCount = source.InstallmentCount,
            FinalInstallmentDueDate = source.FinalInstallmentDueDate,
            CashOptionEnabled = source.CashOptionEnabled,
            WaiverText = source.WaiverText,
            RefundPolicyText = source.RefundPolicyText,
            ContactPhone = source.ContactPhone,
            ContactEmail = source.ContactEmail,

            // Draft posture — Karen tweaks then publishes
            IsActive = false,
            IsListed = false,

            // Stripe IDs deliberately null — publish path provisions fresh ones
            // so the copy owns its own Stripe Product and can't accidentally
            // credit enrollments to the source or archive the source on delete.
            StripeProductId = null,
            StripeFullPriceId = null,
            StripeInstallmentPriceId = null,
        };

        // Create as draft (no Stripe provisioning yet).
        return await CreateAsync(copy, ct, provisionStripe: false);
    }

    private async Task<string> FindAvailableSlugAsync(string baseSlug, CancellationToken ct)
    {
        // Try the base first (`bailamos-copy`), then `-copy-2`, `-copy-3`, ...
        // Bounded to avoid an infinite loop on pathological data.
        var candidate = baseSlug;
        for (var i = 2; i < 100; i++)
        {
            var exists = await _context.Programs.AnyAsync(p => p.Slug == candidate, ct);
            if (!exists) return candidate;
            candidate = $"{baseSlug}-{i}";
        }
        throw new InvalidOperationException($"Could not find an unused slug based on '{baseSlug}' after 100 attempts.");
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
