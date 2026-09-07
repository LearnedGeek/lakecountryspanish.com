using System.Text;
using System.Text.Encodings.Web;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace LakeCountrySpanish.Web.Services.Programs;

/// <summary>
/// Batches per-enrollment admin notifications into a periodic digest so
/// abuse bursts (a scripted attack against POST /join/{slug}) can't flood
/// the admin inbox one message per enrollment. Real signup flow is
/// unaffected: parents still get their confirmation email inline; only
/// the admin's ping is deferred by up to ~<see cref="TickInterval"/>.
///
/// Ticks every 90 seconds. Each tick:
///   1. Loads all <see cref="ProgramEnrollment"/> rows where
///      <see cref="ProgramEnrollment.AdminNotifiedAt"/> is null AND the
///      row is at least <see cref="StabilityWindow"/> old (to avoid grabbing
///      an enrollment mid-save with partial associated data).
///   2. Groups the batch by the program's <see cref="EnrollmentProgram.ContactEmail"/>
///      so per-program admin routing (Karen for one program, Cece for another)
///      keeps working.
///   3. Sends one compact digest email per group with the enrollments listed.
///   4. Stamps <see cref="ProgramEnrollment.AdminNotifiedAt"/> = UtcNow on the
///      batched rows so they aren't re-sent next tick.
///
/// Failure modes handled:
///   • Email send throws → skip the mark-notified for that group; the same
///     rows come back on the next tick and retry. No data loss, no dupe
///     unless the SMTP handshake succeeds AND the DB update fails (small window).
///   • DB save throws → log + move on; next tick catches up.
///
/// Added 2026-09-06 after a scripted-enrollment attack demonstrated the
/// per-enrollment inline email is a flood vector.
/// </summary>
public class AdminEnrollmentDigestBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AdminEnrollmentDigestBackgroundService> _logger;

    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(90);

    // Small quiet window before we grab a row — protects against a race
    // where the enrollment row is committed but the associated Program
    // navigation isn't populated in a way we'd want to read.
    private static readonly TimeSpan StabilityWindow = TimeSpan.FromSeconds(30);

    // Brand palette matches ProgramEnrollmentService's transactional emails.
    private const string BrandNavy = "#1E3A8A";
    private const string BrandTeal = "#1E8189";

    public AdminEnrollmentDigestBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<AdminEnrollmentDigestBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Admin Enrollment Digest Background Service started (tick every {Seconds}s)", TickInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDigestAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing admin enrollment digest");
            }

            try
            {
                await Task.Delay(TickInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Admin Enrollment Digest Background Service stopped");
    }

    private async Task ProcessDigestAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var cutoff = DateTime.UtcNow - StabilityWindow;

        var pending = await db.ProgramEnrollments
            .AsNoTracking()
            .Where(e => e.AdminNotifiedAt == null && e.CreatedAt <= cutoff)
            .Include(e => e.Program)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(ct);

        if (pending.Count == 0) return;

        // Only send to programs that have a ContactEmail set. Programs
        // without one are silently skipped (matches pre-digest behaviour
        // where the teacher-email block was conditional). Their rows still
        // get marked notified so they don't accumulate indefinitely.
        var groupsWithEmail = pending
            .Where(e => !string.IsNullOrWhiteSpace(e.Program.ContactEmail))
            .GroupBy(e => e.Program.ContactEmail!, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sentEnrollmentIds = new List<int>();

        foreach (var group in groupsWithEmail)
        {
            var recipientEmail = group.Key;
            var enrollments = group.OrderBy(e => e.CreatedAt).ToList();

            try
            {
                var subject = enrollments.Count == 1
                    ? $"New enrollment: {enrollments[0].StudentFirstName} {enrollments[0].StudentLastName} — {enrollments[0].Program.Name}"
                    : $"{enrollments.Count} new enrollments — Lake Country Spanish";

                var body = BuildDigestBody(enrollments);

                await emailService.SendBrandedEmailAsync(
                    recipientEmail,
                    toName: enrollments[0].Program.LocationName,
                    subject,
                    headerTitle: enrollments.Count == 1
                        ? $"New enrollment — {enrollments[0].Program.Name}"
                        : $"{enrollments.Count} new enrollments",
                    headerColorHex: BrandTeal,
                    bodyContentHtml: body,
                    preheader: enrollments.Count == 1
                        ? $"{enrollments[0].StudentFirstName} {enrollments[0].StudentLastName} · {enrollments[0].PaymentType}. Details inside."
                        : $"{enrollments.Count} enrollments in the last {TickInterval.TotalSeconds:0}s.",
                    emoji: "📋");

                sentEnrollmentIds.AddRange(enrollments.Select(e => e.Id));
                _logger.LogInformation("Digest sent to {Email} with {Count} enrollment(s)", recipientEmail, enrollments.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send admin enrollment digest to {Email} for {Count} enrollment(s). Will retry next tick.", recipientEmail, enrollments.Count);
                // Deliberately don't add to sentEnrollmentIds — these rows
                // stay AdminNotifiedAt=null and are picked up next tick.
            }
        }

        // Also mark rows we skipped (no ContactEmail) as notified so they
        // don't sit forever. This preserves the pre-digest behaviour where
        // the teacher email path was conditional on ContactEmail non-empty.
        var skippedNoEmailIds = pending
            .Where(e => string.IsNullOrWhiteSpace(e.Program.ContactEmail))
            .Select(e => e.Id)
            .ToList();

        var toMarkIds = sentEnrollmentIds.Concat(skippedNoEmailIds).ToList();
        if (toMarkIds.Count > 0)
        {
            var now = DateTime.UtcNow;
            await db.ProgramEnrollments
                .Where(e => toMarkIds.Contains(e.Id))
                .ExecuteUpdateAsync(setters => setters.SetProperty(e => e.AdminNotifiedAt, now), ct);
        }
    }

    /// <summary>
    /// Compact one-row-per-enrollment digest. Uniform format for both
    /// single-item and multi-item cases (per design decision on
    /// 2026-09-06 — one code path is simpler than branching by count).
    /// Detail-heavy content (waiver, medical, etc.) is one click away in
    /// the admin roster rather than repeated in the email.
    /// </summary>
    private string BuildDigestBody(IReadOnlyList<ProgramEnrollment> enrollments)
    {
        var enc = HtmlEncoder.Default;
        var sb = new StringBuilder();

        var count = enrollments.Count;
        var intro = count == 1
            ? "A new enrollment just landed:"
            : $"{count} new enrollments landed in the last batch:";

        sb.Append($@"<p style=""margin: 0 0 16px 0; color: #374151; font-size: 15px; line-height: 1.6;"">
    {intro}
</p>");

        foreach (var e in enrollments)
        {
            var p = e.Program;
            var rosterUrl = $"/Admin/Programs/{p.Id}/Enrollments";

            var paymentLine = e.PaymentType switch
            {
                ProgramPaymentType.FullOneTime => $"Paid in full: <strong>${e.TotalAmountPaid:N2}</strong>",
                ProgramPaymentType.TwoInstallment => $"Installment 1 of 2 received: <strong>${e.TotalAmountPaid:N2}</strong> (auto-charge in ~30 days)",
                ProgramPaymentType.CashInHand => $"<strong>Cash-in-hand</strong> — collect ${p.FullPrice:N2} at the booth",
                _ => string.Empty
            };

            sb.Append($@"
<div style=""margin: 12px 0; padding: 14px 16px; background: #f9fafb; border-left: 4px solid {BrandTeal}; border-radius: 6px;"">
    <div style=""font-size: 15px; font-weight: 600; color: #111827; margin-bottom: 4px;"">
        {enc.Encode(e.StudentFirstName)} {enc.Encode(e.StudentLastName)}
        <span style=""color: #9ca3af; font-weight: normal; font-size: 13px;"">· enrollment #{e.Id}</span>
    </div>
    <div style=""font-size: 14px; color: #4b5563; margin-bottom: 6px;"">
        {enc.Encode(p.Name)} · {enc.Encode(p.LocationName)}
    </div>
    <div style=""font-size: 13px; color: #065f46; margin-bottom: 6px;"">
        {paymentLine}
    </div>
    <div style=""font-size: 13px; color: #6b7280;"">
        Parent: {enc.Encode(e.ParentFirstName)} {enc.Encode(e.ParentLastName)}
        · <a href=""mailto:{enc.Encode(e.ParentEmail)}"" style=""color: {BrandNavy};"">{enc.Encode(e.ParentEmail)}</a>
        · <a href=""tel:{enc.Encode(e.ParentPhone)}"" style=""color: {BrandNavy};"">{enc.Encode(e.ParentPhone)}</a>
    </div>
    <div style=""margin-top: 8px;"">
        <a href=""{rosterUrl}"" style=""color: {BrandTeal}; font-size: 13px; font-weight: 600; text-decoration: none;"">View roster →</a>
    </div>
</div>");
        }

        sb.Append($@"
<p style=""margin: 20px 0 0 0; color: #9ca3af; font-size: 12px;"">
    Digest window: {TickInterval.TotalSeconds:0} seconds. Full waiver, medical notes, emergency contact, and pickup authorization for each enrollment are available in the admin roster.
</p>");

        return sb.ToString();
    }
}
