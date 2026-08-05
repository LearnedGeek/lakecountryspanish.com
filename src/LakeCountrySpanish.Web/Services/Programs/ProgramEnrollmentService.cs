using System.Text.Encodings.Web;
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
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProgramEnrollmentService> _logger;

    public ProgramEnrollmentService(
        ApplicationDbContext context,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<ProgramEnrollmentService> logger)
    {
        _context = context;
        _emailService = emailService;
        _configuration = configuration;
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
            await SendEnrollmentEmailsSafeAsync(enrollment, program, ct);
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

        // Send enrollment confirmation + Karen notification now that first payment
        // has landed — for TwoInstallment this happens on installment 1, and
        // HandleInvoicePaidAsync will send a separate "installment 2 complete"
        // note when the final charge lands.
        await SendEnrollmentEmailsSafeAsync(enrollment, enrollment.Program, ct);

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

    public async Task<ProgramEnrollment> MarkCashConfirmedAsync(int enrollmentId, AdminActor actor, CancellationToken ct = default)
    {
        var enrollment = await _context.ProgramEnrollments
            .Include(e => e.Program)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId, ct)
            ?? throw new InvalidOperationException($"Enrollment {enrollmentId} not found.");

        if (enrollment.PaymentType != ProgramPaymentType.CashInHand)
            throw new InvalidOperationException("Cash confirmation only applies to cash-in-hand enrollments.");

        if (enrollment.Status == ProgramEnrollmentStatus.FullyPaid)
            return enrollment;  // idempotent

        var amount = enrollment.Program.FullPrice;
        enrollment.Status = ProgramEnrollmentStatus.FullyPaid;
        enrollment.FirstPaymentAt ??= DateTime.UtcNow;
        enrollment.TotalAmountPaid = amount;
        enrollment.UpdatedAt = DateTime.UtcNow;

        _context.ProgramEnrollmentAuditEvents.Add(new ProgramEnrollmentAuditEvent
        {
            EnrollmentId = enrollment.Id,
            OccurredAt = DateTime.UtcNow,
            ActorUserId = actor.UserId,
            ActorDisplayName = actor.DisplayName,
            EventType = EnrollmentAuditEventType.CashConfirmed,
            Details = $"Marked cash payment received (${amount:N2})",
            MonetaryDelta = amount
        });

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Enrollment {EnrollmentId} cash payment confirmed by {Actor}", enrollmentId, actor.DisplayName);
        return enrollment;
    }

    public async Task<ProgramEnrollment> UndoCashConfirmationAsync(int enrollmentId, AdminActor actor, string? reason, CancellationToken ct = default)
    {
        var enrollment = await _context.ProgramEnrollments
            .Include(e => e.Program)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId, ct)
            ?? throw new InvalidOperationException($"Enrollment {enrollmentId} not found.");

        if (enrollment.PaymentType != ProgramPaymentType.CashInHand)
            throw new InvalidOperationException("Undo cash confirmation only applies to cash-in-hand enrollments.");

        if (enrollment.Status != ProgramEnrollmentStatus.FullyPaid)
            throw new InvalidOperationException("This enrollment is not marked as cash paid, so there's nothing to undo.");

        var amount = enrollment.TotalAmountPaid;
        enrollment.Status = ProgramEnrollmentStatus.CashPending;
        enrollment.TotalAmountPaid = 0m;
        // FirstPaymentAt kept as-is so we retain the original claim timestamp;
        // the audit event captures the reversal separately.
        enrollment.UpdatedAt = DateTime.UtcNow;

        var details = string.IsNullOrWhiteSpace(reason)
            ? $"Reversed cash confirmation (-${amount:N2})"
            : $"Reversed cash confirmation (-${amount:N2}) — {reason.Trim()}";

        _context.ProgramEnrollmentAuditEvents.Add(new ProgramEnrollmentAuditEvent
        {
            EnrollmentId = enrollment.Id,
            OccurredAt = DateTime.UtcNow,
            ActorUserId = actor.UserId,
            ActorDisplayName = actor.DisplayName,
            EventType = EnrollmentAuditEventType.CashConfirmationUndone,
            Details = details,
            MonetaryDelta = -amount
        });

        await _context.SaveChangesAsync(ct);
        _logger.LogWarning("Enrollment {EnrollmentId} cash confirmation reversed by {Actor}: {Reason}", enrollmentId, actor.DisplayName, reason ?? "(no reason given)");
        return enrollment;
    }

    public async Task<IReadOnlyList<ProgramEnrollmentAuditEvent>> GetAuditEventsAsync(int enrollmentId, CancellationToken ct = default)
    {
        return await _context.ProgramEnrollmentAuditEvents
            .Where(e => e.EnrollmentId == enrollmentId)
            .OrderBy(e => e.OccurredAt)
            .ToListAsync(ct);
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

    // ---------------- Email helpers ----------------

    // Brand colors match SmtpEmailService's palette so headers/gradients look
    // native to the rest of the site's transactional email.
    private const string BrandNavy = "#1E3A8A";
    private const string BrandTeal = "#1E8189";

    /// <summary>
    /// Sends the parent confirmation + teacher notification pair via the LCS
    /// branded email shell. Both wrapped in try/catch so an SMTP hiccup can't
    /// undo a successful enrollment / payment. The teacher email goes to
    /// <see cref="EnrollmentProgram.ContactEmail"/> so a per-program teacher
    /// (Cece for one, Karen for another) gets the right ping.
    /// </summary>
    private async Task SendEnrollmentEmailsSafeAsync(ProgramEnrollment enrollment, EnrollmentProgram program, CancellationToken ct)
    {
        try
        {
            var parentSubject = $"You're enrolled in {program.Name} — Lake Country Spanish";
            var parentBody = BuildParentConfirmationBody(enrollment, program);
            await _emailService.SendBrandedEmailAsync(
                enrollment.ParentEmail,
                $"{enrollment.ParentFirstName} {enrollment.ParentLastName}",
                parentSubject,
                headerTitle: $"You're enrolled — {program.Name}",
                headerColorHex: BrandNavy,
                bodyContentHtml: parentBody,
                preheader: $"Confirmation + your submitted information + the waiver you accepted. Enrollment #{enrollment.Id}.",
                emoji: "🎉");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send parent enrollment confirmation for enrollment {EnrollmentId}", enrollment.Id);
        }

        if (!string.IsNullOrWhiteSpace(program.ContactEmail))
        {
            try
            {
                var teacherSubject = $"New enrollment: {enrollment.StudentFirstName} {enrollment.StudentLastName} — {program.Name}";
                var teacherBody = BuildTeacherNotificationBody(enrollment, program);
                await _emailService.SendBrandedEmailAsync(
                    program.ContactEmail,
                    program.LocationName,
                    teacherSubject,
                    headerTitle: $"New enrollment — {program.Name}",
                    headerColorHex: BrandTeal,
                    bodyContentHtml: teacherBody,
                    preheader: $"{enrollment.StudentFirstName} {enrollment.StudentLastName} · {enrollment.PaymentType}. Full details inside.",
                    emoji: "📋");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send teacher enrollment notification for enrollment {EnrollmentId}", enrollment.Id);
            }
        }
    }

    /// <summary>
    /// Full audit-trail confirmation the parent files away and can reference
    /// if anything is later disputed: program details, everything they
    /// submitted, the exact waiver text they accepted, and a stable
    /// enrollment reference number.
    /// </summary>
    private string BuildParentConfirmationBody(ProgramEnrollment e, EnrollmentProgram p)
    {
        var enc = HtmlEncoder.Default;
        var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://lakecountryspanish.com";

        var paymentBlurb = e.PaymentType switch
        {
            ProgramPaymentType.FullOneTime => $"Payment received in full: <strong>${e.TotalAmountPaid:N2}</strong>.",
            ProgramPaymentType.TwoInstallment => $"First installment received: <strong>${e.TotalAmountPaid:N2}</strong>. Your second installment will be charged automatically in about 30 days.",
            ProgramPaymentType.CashInHand => $"We'll collect <strong>${p.FullPrice:N2}</strong> from you at the booth. Bring cash or a check made out to <strong>Lake Country Spanish, LLC</strong>.",
            _ => string.Empty
        };

        var medical = string.IsNullOrWhiteSpace(e.MedicalConcerns)
            ? "<em style=\"color:#9ca3af;\">None noted</em>"
            : enc.Encode(e.MedicalConcerns!);

        var address = string.IsNullOrWhiteSpace(e.ParentAddressLine1)
            ? "<em style=\"color:#9ca3af;\">Not provided</em>"
            : $"{enc.Encode(e.ParentAddressLine1)}<br />{enc.Encode(e.ParentCity)}, {enc.Encode(e.ParentState)} {enc.Encode(e.ParentZip)}";

        var photoRelease = e.PhotoReleaseGrantedAt.HasValue
            ? $"<strong>Granted</strong> on {e.PhotoReleaseGrantedAt.Value:MMM d, yyyy 'at' h:mm tt} UTC"
            : "<em style=\"color:#9ca3af;\">Not granted</em>";

        var waiverHtml = string.IsNullOrWhiteSpace(p.WaiverText)
            ? "<em style=\"color:#9ca3af;\">No waiver on file for this program.</em>"
            : Markdig.Markdown.ToHtml(p.WaiverText);

        return $@"
<p style=""margin: 0 0 16px 0; color: #374151; font-size: 15px; line-height: 1.6;"">Hi {enc.Encode(e.ParentFirstName)},</p>

<p style=""margin: 0 0 16px 0; color: #374151; font-size: 15px; line-height: 1.6;"">
    Thanks for enrolling <strong>{enc.Encode(e.StudentFirstName)} {enc.Encode(e.StudentLastName)}</strong> in
    <strong>{enc.Encode(p.Name)}</strong>. This email is your receipt — keep it for your records.
</p>

<h3 style=""margin: 24px 0 8px 0; color: #111827; font-size: 16px;"">Program details</h3>
{BuildKvpTable(enc, new (string, string)[] {
    ("Location", $"{enc.Encode(p.LocationName)}<br /><span style=\"color:#6b7280; font-size:12px;\">{enc.Encode(p.LocationAddress)}</span>"),
    ("Dates", $"{p.StartDate:MMM d} &ndash; {p.EndDate:MMM d, yyyy}"),
    ("Meets", $"{enc.Encode(p.MeetingDays)} &middot; {p.StartTime:h:mm tt} &ndash; {p.EndTime:h:mm tt}"),
})}

<div style=""margin: 20px 0; padding: 14px 16px; background: #f0f9ff; border-left: 4px solid {BrandNavy}; border-radius: 6px; font-size: 15px; color: #1e3a8a;"">
    {paymentBlurb}
</div>

<h3 style=""margin: 24px 0 8px 0; color: #111827; font-size: 16px;"">Information you submitted</h3>
<p style=""margin: 0 0 8px 0; color: #6b7280; font-size: 13px;"">
    If anything below is wrong, please <a href=""mailto:{enc.Encode(p.ContactEmail)}"" style=""color: {BrandNavy}; font-weight: 600;"">reply to this email</a> or call
    <a href=""tel:{enc.Encode(p.ContactPhone)}"" style=""color: {BrandNavy}; font-weight: 600;"">{enc.Encode(p.ContactPhone)}</a>.
</p>

<h4 style=""margin: 16px 0 6px 0; color: #374151; font-size: 14px;"">Student</h4>
{BuildKvpTable(enc, new (string, string)[] {
    ("Name", $"<strong>{enc.Encode(e.StudentFirstName)} {enc.Encode(e.StudentLastName)}</strong>"),
    ("Grade", string.IsNullOrEmpty(e.StudentGrade) ? "&mdash;" : enc.Encode(e.StudentGrade)),
    ("Birthdate", $"{e.StudentBirthDate:MMM d, yyyy}"),
    ("Medical concerns", medical),
    ("Notes", string.IsNullOrWhiteSpace(e.StudentNotes) ? "<em style=\"color:#9ca3af;\">None</em>" : enc.Encode(e.StudentNotes!)),
})}

<h4 style=""margin: 16px 0 6px 0; color: #374151; font-size: 14px;"">Parent / guardian</h4>
{BuildKvpTable(enc, new (string, string)[] {
    ("Name", $"{enc.Encode(e.ParentFirstName)} {enc.Encode(e.ParentLastName)}"),
    ("Email", enc.Encode(e.ParentEmail)),
    ("Phone", enc.Encode(e.ParentPhone)),
    ("Address", address),
})}

<h4 style=""margin: 16px 0 6px 0; color: #374151; font-size: 14px;"">Emergency contact</h4>
{BuildKvpTable(enc, new (string, string)[] {
    ("Name", $"{enc.Encode(e.EmergencyName)} ({enc.Encode(e.EmergencyRelationship)})"),
    ("Phone", enc.Encode(e.EmergencyPhone)),
})}

<h4 style=""margin: 16px 0 6px 0; color: #374151; font-size: 14px;"">Pickup authorization</h4>
<div style=""padding: 12px 14px; background: #f9fafb; border-radius: 6px; font-size: 14px; color: #374151; white-space: pre-wrap;"">{enc.Encode(e.PickupAuthorization)}</div>

<h4 style=""margin: 16px 0 6px 0; color: #374151; font-size: 14px;"">Photo / video release</h4>
<p style=""margin: 0 0 8px 0; color: #374151; font-size: 14px;"">{photoRelease}</p>

<h3 style=""margin: 28px 0 8px 0; color: #111827; font-size: 16px;"">Waiver you accepted</h3>
<p style=""margin: 0 0 8px 0; color: #6b7280; font-size: 13px;"">
    Accepted on <strong>{e.WaiverAcceptedAt:MMM d, yyyy 'at' h:mm tt} UTC</strong>.
</p>
<div style=""padding: 16px 18px; background: #f9fafb; border: 1px solid #e5e7eb; border-radius: 8px; font-size: 14px; color: #374151; line-height: 1.6;"">
    {waiverHtml}
</div>

<p style=""margin: 28px 0 8px 0; color: #6b7280; font-size: 12px;"">
    Enrollment reference: <strong>#{e.Id}</strong> &middot; Enrolled {e.CreatedAt:yyyy-MM-dd HH:mm} UTC
</p>
<p style=""margin: 0; color: #9ca3af; font-size: 12px;"">
    Lake Country Spanish, LLC &middot; <a href=""{baseUrl}"" style=""color: #6b7280;"">{baseUrl.Replace("https://", "").Replace("http://", "")}</a>
</p>";
    }

    /// <summary>
    /// Full-detail teacher notification with the same audit-trail content the
    /// parent sees, plus a CTA link back to the admin roster for the program
    /// so the teacher can take action (cash-confirm, contact the family, etc.)
    /// in one click.
    /// </summary>
    private string BuildTeacherNotificationBody(ProgramEnrollment e, EnrollmentProgram p)
    {
        var enc = HtmlEncoder.Default;
        var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://lakecountryspanish.com";
        var rosterUrl = $"{baseUrl}/Admin/Programs/{p.Id}/Enrollments";

        var paymentLine = e.PaymentType switch
        {
            ProgramPaymentType.FullOneTime => $"Paid in full: <strong>${e.TotalAmountPaid:N2}</strong>",
            ProgramPaymentType.TwoInstallment => $"Installment 1 of 2 received: <strong>${e.TotalAmountPaid:N2}</strong> (auto-charge for installment 2 in ~30 days)",
            ProgramPaymentType.CashInHand => $"<strong>Cash-in-hand</strong> — collect ${p.FullPrice:N2} at the booth. Mark cash confirmed in admin once received.",
            _ => string.Empty
        };

        var medical = string.IsNullOrWhiteSpace(e.MedicalConcerns)
            ? "<em style=\"color:#9ca3af;\">None noted</em>"
            : $"<span style=\"color: #b91c1c; font-weight: 600;\">{enc.Encode(e.MedicalConcerns!)}</span>";

        var photoRelease = e.PhotoReleaseGrantedAt.HasValue
            ? $"<strong>Granted</strong> on {e.PhotoReleaseGrantedAt.Value:MMM d, yyyy 'at' h:mm tt} UTC"
            : "<em style=\"color:#9ca3af;\">Not granted</em>";

        var waiverHtml = string.IsNullOrWhiteSpace(p.WaiverText)
            ? "<em style=\"color:#9ca3af;\">No waiver on file for this program.</em>"
            : Markdig.Markdown.ToHtml(p.WaiverText);

        return $@"
<p style=""margin: 0 0 16px 0; color: #374151; font-size: 15px; line-height: 1.6;"">
    A new enrollment just landed for <strong>{enc.Encode(p.Name)}</strong>.
</p>

<div style=""margin: 20px 0; padding: 14px 16px; background: #ecfdf5; border-left: 4px solid #059669; border-radius: 6px; font-size: 15px; color: #065f46;"">
    {paymentLine}
</div>

<h3 style=""margin: 24px 0 8px 0; color: #111827; font-size: 16px;"">Student</h3>
{BuildKvpTable(enc, new (string, string)[] {
    ("Name", $"<strong>{enc.Encode(e.StudentFirstName)} {enc.Encode(e.StudentLastName)}</strong>"),
    ("Grade", string.IsNullOrEmpty(e.StudentGrade) ? "&mdash;" : enc.Encode(e.StudentGrade)),
    ("Birthdate", $"{e.StudentBirthDate:MMM d, yyyy}"),
    ("Medical concerns", medical),
    ("Notes", string.IsNullOrWhiteSpace(e.StudentNotes) ? "<em style=\"color:#9ca3af;\">None</em>" : enc.Encode(e.StudentNotes!)),
})}

<h3 style=""margin: 24px 0 8px 0; color: #111827; font-size: 16px;"">Parent / guardian</h3>
{BuildKvpTable(enc, new (string, string)[] {
    ("Name", $"{enc.Encode(e.ParentFirstName)} {enc.Encode(e.ParentLastName)}"),
    ("Email", $"<a href=\"mailto:{enc.Encode(e.ParentEmail)}\" style=\"color: {BrandNavy};\">{enc.Encode(e.ParentEmail)}</a>"),
    ("Phone", $"<a href=\"tel:{enc.Encode(e.ParentPhone)}\" style=\"color: {BrandNavy};\">{enc.Encode(e.ParentPhone)}</a>"),
})}

<h3 style=""margin: 24px 0 8px 0; color: #111827; font-size: 16px;"">Emergency contact</h3>
{BuildKvpTable(enc, new (string, string)[] {
    ("Name", $"{enc.Encode(e.EmergencyName)} ({enc.Encode(e.EmergencyRelationship)})"),
    ("Phone", $"<a href=\"tel:{enc.Encode(e.EmergencyPhone)}\" style=\"color: {BrandNavy};\">{enc.Encode(e.EmergencyPhone)}</a>"),
})}

<h3 style=""margin: 24px 0 8px 0; color: #111827; font-size: 16px;"">Pickup authorization</h3>
<div style=""padding: 12px 14px; background: #f9fafb; border-radius: 6px; font-size: 14px; color: #374151; white-space: pre-wrap;"">{enc.Encode(e.PickupAuthorization)}</div>

<h3 style=""margin: 24px 0 8px 0; color: #111827; font-size: 16px;"">Photo / video release</h3>
<p style=""margin: 0 0 16px 0; color: #374151; font-size: 14px;"">{photoRelease}</p>

<h3 style=""margin: 24px 0 8px 0; color: #111827; font-size: 16px;"">Waiver they accepted</h3>
<p style=""margin: 0 0 8px 0; color: #6b7280; font-size: 13px;"">
    Accepted on <strong>{e.WaiverAcceptedAt:MMM d, yyyy 'at' h:mm tt} UTC</strong>.
</p>
<div style=""padding: 14px 16px; background: #f9fafb; border: 1px solid #e5e7eb; border-radius: 8px; font-size: 13px; color: #4b5563; line-height: 1.6; max-height: 240px; overflow: auto;"">
    {waiverHtml}
</div>

<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""margin: 24px 0;"">
    <tr>
        <td align=""center"">
            <a href=""{rosterUrl}"" style=""display: inline-block; background-color: {BrandTeal}; color: #ffffff; font-weight: 600; font-size: 15px; padding: 12px 28px; border-radius: 8px; text-decoration: none;"">View roster + take action</a>
        </td>
    </tr>
</table>

<p style=""margin: 0; color: #9ca3af; font-size: 12px;"">
    Enrollment reference: <strong>#{e.Id}</strong> &middot; Received {e.CreatedAt:yyyy-MM-dd HH:mm} UTC
</p>";
    }

    /// <summary>Renders a two-column key/value table with LCS's standard typography.</summary>
    private static string BuildKvpTable(HtmlEncoder enc, (string label, string valueHtml)[] rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<table cellpadding=\"6\" cellspacing=\"0\" style=\"border-collapse: collapse; width: 100%; font-size: 14px;\">");
        foreach (var (label, valueHtml) in rows)
        {
            sb.Append("<tr>");
            sb.Append($"<td style=\"color: #6b7280; vertical-align: top; padding: 4px 12px 4px 0; width: 130px; white-space: nowrap;\">{enc.Encode(label)}</td>");
            sb.Append($"<td style=\"color: #111827; padding: 4px 0;\">{valueHtml}</td>");
            sb.Append("</tr>");
        }
        sb.Append("</table>");
        return sb.ToString();
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
