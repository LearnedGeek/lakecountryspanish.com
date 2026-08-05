using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace LakeCountrySpanish.Web.Services.Programs;

public sealed class ProgramEnrollmentService : IProgramEnrollmentService
{
    /// <summary>
    /// Metadata key used on Stripe Checkout Sessions + Subscriptions to link
    /// them back to the ProgramEnrollment row on webhook receipt.
    /// </summary>
    private const string EnrollmentIdMetadataKey = "lcs.enrollmentId";

    /// <summary>
    /// Metadata sentinel that tells the payment webhook this session belongs
    /// to the program-enrollment flow, so it can route to us and skip the
    /// normal Payment-record lookup.
    /// </summary>
    private const string ProgramEnrollmentType = "program-enrollment";

    /// <summary>
    /// Days between subscription creation and auto-cancel for the 2-installment
    /// plan. Stripe charges cycle 1 immediately, cycle 2 at day 30, then cancels
    /// before cycle 3 would occur at day 60. 35 days gives cycle 2 a safe buffer.
    /// </summary>
    private const int InstallmentCancelBufferDays = 35;

    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProgramEnrollmentService> _logger;

    public ProgramEnrollmentService(ApplicationDbContext context, ILogger<ProgramEnrollmentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<EnrollmentSubmissionResult> SubmitAsync(EnrollmentSubmission input, CancellationToken ct = default)
    {
        if (!input.WaiverAccepted)
            return EnrollmentSubmissionResult.Failed("The waiver must be accepted before enrolling.");

        var program = await _context.Programs.FirstOrDefaultAsync(p => p.Id == input.ProgramId, ct);
        if (program is null)
            return EnrollmentSubmissionResult.Failed("Program not found.");
        if (!program.IsActive)
            return EnrollmentSubmissionResult.Failed("This program is no longer accepting enrollments.");

        // Guard: payment type must be one the program actually offers.
        switch (input.PaymentType)
        {
            case ProgramPaymentType.TwoInstallment when !program.InstallmentsEnabled:
                return EnrollmentSubmissionResult.Failed("This program doesn't offer an installment plan.");
            case ProgramPaymentType.CashInHand when !program.CashOptionEnabled:
                return EnrollmentSubmissionResult.Failed("This program requires online payment.");
        }

        var enrollment = new ProgramEnrollment
        {
            ProgramId = program.Id,
            CreatedAt = DateTime.UtcNow,

            ParentFirstName = input.ParentFirstName.Trim(),
            ParentLastName = input.ParentLastName.Trim(),
            ParentEmail = input.ParentEmail.Trim(),
            ParentPhone = input.ParentPhone.Trim(),
            ParentAddressLine1 = input.ParentAddressLine1.Trim(),
            ParentCity = input.ParentCity.Trim(),
            ParentState = input.ParentState.Trim(),
            ParentZip = input.ParentZip.Trim(),

            StudentFirstName = input.StudentFirstName.Trim(),
            StudentLastName = input.StudentLastName.Trim(),
            StudentGrade = input.StudentGrade.Trim(),
            StudentBirthDate = input.StudentBirthDate,
            MedicalConcerns = string.IsNullOrWhiteSpace(input.MedicalConcerns) ? null : input.MedicalConcerns.Trim(),
            StudentNotes = string.IsNullOrWhiteSpace(input.StudentNotes) ? null : input.StudentNotes.Trim(),

            EmergencyName = input.EmergencyName.Trim(),
            EmergencyPhone = input.EmergencyPhone.Trim(),
            EmergencyRelationship = input.EmergencyRelationship.Trim(),

            PickupAuthorization = input.PickupAuthorization.Trim(),

            WaiverAcceptedAt = DateTime.UtcNow,
            PhotoReleaseGrantedAt = input.PhotoReleaseGranted ? DateTime.UtcNow : null,

            PaymentType = input.PaymentType,
            Status = input.PaymentType == ProgramPaymentType.CashInHand
                ? ProgramEnrollmentStatus.CashPending
                : ProgramEnrollmentStatus.PendingPayment
        };

        _context.ProgramEnrollments.Add(enrollment);
        await _context.SaveChangesAsync(ct);

        if (input.PaymentType == ProgramPaymentType.CashInHand)
        {
            _logger.LogInformation("Program enrollment {EnrollmentId} recorded as cash-in-hand for program {ProgramId}", enrollment.Id, program.Id);
            return EnrollmentSubmissionResult.ForCash(enrollment.Id);
        }

        // ---- Stripe Checkout ----
        try
        {
            var sessionUrl = await CreateStripeCheckoutSessionAsync(program, enrollment, input, ct);
            return EnrollmentSubmissionResult.ForStripe(enrollment.Id, sessionUrl);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe checkout session creation failed for enrollment {EnrollmentId}", enrollment.Id);
            enrollment.Status = ProgramEnrollmentStatus.Cancelled;
            enrollment.AdminNotes = $"Stripe checkout creation failed at {DateTime.UtcNow:u}: {ex.Message}";
            await _context.SaveChangesAsync(ct);
            return EnrollmentSubmissionResult.Failed("Payment setup failed. Please try again in a moment.");
        }
    }

    public Task<ProgramEnrollment?> GetAsync(int id, CancellationToken ct = default) =>
        _context.ProgramEnrollments
            .Include(e => e.Program)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<ProgramEnrollment?> GetByCheckoutSessionAsync(string sessionId, CancellationToken ct = default) =>
        _context.ProgramEnrollments
            .Include(e => e.Program)
            .FirstOrDefaultAsync(e => e.StripeCheckoutSessionId == sessionId, ct);

    public async Task<bool> HandleCheckoutSessionCompletedAsync(Session session, CancellationToken ct = default)
    {
        if (!TryReadEnrollmentId(session.Metadata, out var enrollmentId))
            return false;

        var enrollment = await _context.ProgramEnrollments
            .Include(e => e.Program)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId, ct);

        if (enrollment is null)
        {
            _logger.LogWarning("Checkout session {SessionId} referenced enrollment {EnrollmentId} that doesn't exist", session.Id, enrollmentId);
            return true;  // handled — nothing to do
        }

        // Idempotency: don't reprocess a session we've already booked.
        if (enrollment.Status is ProgramEnrollmentStatus.FirstPaymentComplete
            or ProgramEnrollmentStatus.FullyPaid)
        {
            _logger.LogInformation("Enrollment {EnrollmentId} already marked paid; ignoring duplicate checkout.session.completed", enrollmentId);
            return true;
        }

        enrollment.StripeCheckoutSessionId = session.Id;
        enrollment.StripeCustomerId = session.CustomerId;
        enrollment.FirstPaymentAt = DateTime.UtcNow;

        if (enrollment.PaymentType == ProgramPaymentType.FullOneTime)
        {
            enrollment.Status = ProgramEnrollmentStatus.FullyPaid;
            enrollment.TotalAmountPaid = enrollment.Program.FullPrice;
            _logger.LogInformation("Enrollment {EnrollmentId} fully paid via one-time checkout", enrollmentId);
        }
        else  // TwoInstallment — subscription mode
        {
            enrollment.StripeSubscriptionId = session.SubscriptionId;
            enrollment.Status = ProgramEnrollmentStatus.FirstPaymentComplete;
            enrollment.TotalAmountPaid = enrollment.Program.InstallmentAmount;

            // Now that the subscription exists, tell Stripe to auto-cancel it after
            // the second monthly charge lands (buffer beyond cycle 2 at day 30).
            await SetSubscriptionAutoCancelAsync(session.SubscriptionId, ct);

            _logger.LogInformation("Enrollment {EnrollmentId} first installment received; subscription {SubId} scheduled to auto-cancel after installment 2",
                enrollmentId, session.SubscriptionId);
        }

        enrollment.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> HandleInvoicePaidAsync(Invoice invoice, CancellationToken ct = default)
    {
        // Only interested in subscription invoices (installments). One-time invoices
        // are for the FullOneTime path and are already handled via checkout.session.completed.
        var subscriptionId = ReadInvoiceSubscriptionId(invoice);
        if (string.IsNullOrEmpty(subscriptionId)) return false;

        var enrollment = await _context.ProgramEnrollments
            .Include(e => e.Program)
            .FirstOrDefaultAsync(e => e.StripeSubscriptionId == subscriptionId, ct);

        if (enrollment is null) return false;

        // Cycle 1 was already recorded via checkout.session.completed. Only bump
        // to FullyPaid on cycle 2 (or later — safe idempotency).
        if (enrollment.Status == ProgramEnrollmentStatus.FullyPaid)
        {
            _logger.LogInformation("Enrollment {EnrollmentId} already fully paid; ignoring extra invoice.paid", enrollment.Id);
            return true;
        }

        if (enrollment.Status != ProgramEnrollmentStatus.FirstPaymentComplete)
        {
            // First installment hasn't been recorded yet — probably invoice.paid arriving
            // out-of-order with checkout.session.completed. Record what we can and move on.
            enrollment.StripeCustomerId ??= invoice.CustomerId;
            enrollment.FirstPaymentAt ??= DateTime.UtcNow;
            enrollment.Status = ProgramEnrollmentStatus.FirstPaymentComplete;
            enrollment.TotalAmountPaid = enrollment.Program.InstallmentAmount;
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Enrollment {EnrollmentId}: recorded first payment from invoice.paid (out-of-order path)", enrollment.Id);
            return true;
        }

        // Normal case: cycle 2 arrived — mark fully paid.
        enrollment.SecondPaymentAt = DateTime.UtcNow;
        enrollment.Status = ProgramEnrollmentStatus.FullyPaid;
        enrollment.TotalAmountPaid = enrollment.Program.FullPrice;
        enrollment.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Enrollment {EnrollmentId} second installment received; fully paid", enrollment.Id);
        return true;
    }

    public async Task<ProgramEnrollment> MarkCashConfirmedAsync(int enrollmentId, string adminUserId, CancellationToken ct = default)
    {
        var enrollment = await _context.ProgramEnrollments
            .Include(e => e.Program)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId, ct)
            ?? throw new InvalidOperationException($"Enrollment {enrollmentId} not found.");

        if (enrollment.PaymentType != ProgramPaymentType.CashInHand)
            throw new InvalidOperationException("Cash confirmation only applies to cash-in-hand enrollments.");

        if (enrollment.Status == ProgramEnrollmentStatus.FullyPaid)
            return enrollment;  // idempotent

        enrollment.Status = ProgramEnrollmentStatus.FullyPaid;
        enrollment.FirstPaymentAt ??= DateTime.UtcNow;
        enrollment.TotalAmountPaid = enrollment.Program.FullPrice;
        enrollment.UpdatedAt = DateTime.UtcNow;
        enrollment.AdminNotes = $"{enrollment.AdminNotes}\n[{DateTime.UtcNow:u}] Cash confirmed by admin {adminUserId}".Trim();

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Enrollment {EnrollmentId} cash payment confirmed by {AdminUserId}", enrollmentId, adminUserId);
        return enrollment;
    }

    // ---------------- Stripe helpers ----------------

    private async Task<string> CreateStripeCheckoutSessionAsync(
        EnrollmentProgram program,
        ProgramEnrollment enrollment,
        EnrollmentSubmission input,
        CancellationToken ct)
    {
        var successUrl = $"{input.BaseUrl}/join/{program.Slug}/thank-you?session={{CHECKOUT_SESSION_ID}}";
        var cancelUrl = $"{input.BaseUrl}/join/{program.Slug}?cancelled=1";

        var metadata = new Dictionary<string, string>
        {
            { EnrollmentIdMetadataKey, enrollment.Id.ToString() },
            { "lcs.type", ProgramEnrollmentType },
            { "lcs.programSlug", program.Slug }
        };

        var options = new SessionCreateOptions
        {
            CustomerEmail = enrollment.ParentEmail,
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            Metadata = metadata,
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    Price = input.PaymentType == ProgramPaymentType.FullOneTime
                        ? program.StripeFullPriceId
                        : program.StripeInstallmentPriceId,
                    Quantity = 1
                }
            }
        };

        if (input.PaymentType == ProgramPaymentType.FullOneTime)
        {
            options.Mode = "payment";
        }
        else  // TwoInstallment
        {
            options.Mode = "subscription";
            // Stripe.net's SessionSubscriptionDataOptions doesn't expose cancel_at
            // at Checkout Session creation time. We set it in the webhook handler
            // once checkout.session.completed lands with the created subscription id
            // — see SetSubscriptionAutoCancelAsync().
            options.SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = metadata
            };
        }

        var svc = new SessionService();
        var session = await svc.CreateAsync(options, cancellationToken: ct);

        // Store the session id up front so the return handler can look up the
        // enrollment even before the webhook lands.
        enrollment.StripeCheckoutSessionId = session.Id;
        await _context.SaveChangesAsync(ct);

        return session.Url!;
    }

    /// <summary>
    /// Sets the Stripe subscription's <c>cancel_at</c> to now + buffer days so
    /// Stripe charges cycle 2 (~day 30) and then auto-cancels before cycle 3
    /// would occur (~day 60). Called from the checkout.session.completed handler
    /// once we have the subscription id — Stripe.net doesn't expose cancel_at
    /// at Checkout Session creation time.
    /// </summary>
    private async Task SetSubscriptionAutoCancelAsync(string subscriptionId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(subscriptionId)) return;
        try
        {
            var svc = new SubscriptionService();
            await svc.UpdateAsync(subscriptionId, new SubscriptionUpdateOptions
            {
                CancelAt = DateTime.UtcNow.AddDays(InstallmentCancelBufferDays)
            }, cancellationToken: ct);
        }
        catch (StripeException ex)
        {
            // Non-fatal: enrollment is already recorded; a cancel_at that failed
            // just means we might see an extra charge attempt at day 60. Log
            // loudly so we notice on the first occurrence.
            _logger.LogError(ex, "Failed to set cancel_at on installment subscription {SubscriptionId}. Manual cancel needed after installment 2.", subscriptionId);
        }
    }

    private static bool TryReadEnrollmentId(IDictionary<string, string>? metadata, out int enrollmentId)
    {
        enrollmentId = 0;
        if (metadata is null) return false;
        return metadata.TryGetValue(EnrollmentIdMetadataKey, out var raw)
               && int.TryParse(raw, out enrollmentId);
    }

    /// <summary>
    /// Best-effort read of the subscription id off an Invoice. Different Stripe.net
    /// versions expose this slightly differently — check Parent/SubscriptionDetails
    /// first (modern API) then fall back to the older top-level SubscriptionId.
    /// </summary>
    private static string? ReadInvoiceSubscriptionId(Invoice invoice)
    {
        // The modern API path (parent.subscription_details.subscription).
        var parentSub = invoice.Parent?.SubscriptionDetails?.SubscriptionId;
        if (!string.IsNullOrEmpty(parentSub)) return parentSub;

        // Older/legacy path — some SDK versions still expose Subscription directly.
        var legacy = invoice.GetType().GetProperty("SubscriptionId")?.GetValue(invoice) as string;
        return legacy;
    }
}
