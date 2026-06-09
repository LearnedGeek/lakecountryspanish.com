using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace LakeCountrySpanish.Web.Services.Curriculum;

public sealed class CurriculumDayService : ICurriculumDayService
{
    private const string EntityType = "Day";

    private readonly ApplicationDbContext _context;
    private readonly ILogger<CurriculumDayService> _logger;

    public CurriculumDayService(ApplicationDbContext context, ILogger<CurriculumDayService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Day>> ListAsync(int? unitId = null, GradeBand? gradeBand = null, bool includeInactive = false, CancellationToken ct = default)
    {
        var query = _context.Days
            .AsNoTracking()
            .Include(d => d.Unit)
            .AsQueryable();

        if (unitId.HasValue)   query = query.Where(d => d.UnitId == unitId.Value);
        if (gradeBand.HasValue) query = query.Where(d => d.GradeBand == gradeBand.Value);
        if (!includeInactive)  query = query.Where(d => d.IsActive);

        return await query
            .OrderBy(d => d.Unit.UnitNumber).ThenBy(d => d.DayNumberInUnit)
            .ToListAsync(ct);
    }

    public Task<Day?> GetAsync(int id, CancellationToken ct = default) =>
        _context.Days
            .Include(d => d.Unit)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<Day> CreateAsync(Day day, string authorUserId, string? changeNotes = null, CancellationToken ct = default)
    {
        day.CreatedById = authorUserId;
        day.CreatedAt = DateTime.UtcNow;
        day.LastModifiedAt = null;
        day.IsActive = true;

        _context.Days.Add(day);
        await _context.SaveChangesAsync(ct);

        _context.CurriculumVersions.Add(new CurriculumVersion
        {
            EntityType = EntityType,
            EntityId = day.Id,
            VersionNumber = 1,
            BodySnapshotMarkdown = day.TeacherPlanMarkdown,
            EffectiveDate = DateTime.UtcNow,
            ChangedById = authorUserId,
            ChangeNotes = changeNotes,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Created Day {DayId} ({Title}) by user {UserId}", day.Id, day.Title, authorUserId);
        return day;
    }

    public async Task<Day> UpdateAsync(Day day, string editorUserId, string? changeNotes = null, CancellationToken ct = default)
    {
        var existing = await _context.Days.FirstOrDefaultAsync(d => d.Id == day.Id, ct)
                       ?? throw new InvalidOperationException($"Day {day.Id} not found.");

        // Callers (CurriculumController) typically pass a tracked, already-
        // mutated entity, so `existing` and `day` are the same reference and
        // a direct `existing.Foo != day.Foo` comparison always returns false.
        // Read the pre-update snapshot from EF's change tracker instead.
        var entry = _context.Entry(existing);
        var originalBody = entry.OriginalValues.GetValue<string>(nameof(Day.TeacherPlanMarkdown)) ?? string.Empty;
        var originalActive = entry.OriginalValues.GetValue<bool>(nameof(Day.IsActive));

        var bodyChanged = !string.Equals(originalBody, day.TeacherPlanMarkdown, StringComparison.Ordinal);
        var activationChanged = originalActive != day.IsActive;
        var activatedNow = activationChanged && day.IsActive;

        existing.Title = day.Title;
        existing.Description = day.Description;
        existing.UnitId = day.UnitId;
        existing.DayNumberInUnit = day.DayNumberInUnit;
        existing.GradeBand = day.GradeBand;
        existing.Theme = day.Theme;
        existing.TeacherPlanMarkdown = day.TeacherPlanMarkdown;
        existing.EstimatedDurationMinutes = day.EstimatedDurationMinutes;
        existing.SkillFocus = day.SkillFocus;
        existing.IsActive = day.IsActive;
        existing.LastModifiedAt = DateTime.UtcNow;
        existing.LastModifiedById = editorUserId;

        // Activation is treated as an approval/review event — re-stamp the
        // reviewer fields each time someone re-activates a lesson. Deactivation
        // leaves the historical "last approved on X by Y" record intact.
        if (activatedNow)
        {
            existing.ReviewedById = editorUserId;
            existing.ReviewedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(changeNotes))
            {
                existing.ReviewNotes = changeNotes;
            }
        }

        // Write a CurriculumVersion row for every update, not just body
        // changes — that's how we get a persistent who-did-what audit trail
        // for metadata-only edits and IsActive toggles. The snapshot just
        // re-stores the current body when nothing changed; row count is
        // trivial at LCS scale.
        var nextVersion = await _context.CurriculumVersions
            .Where(v => v.EntityType == EntityType && v.EntityId == day.Id)
            .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0;

        _context.CurriculumVersions.Add(new CurriculumVersion
        {
            EntityType = EntityType,
            EntityId = day.Id,
            VersionNumber = nextVersion + 1,
            BodySnapshotMarkdown = day.TeacherPlanMarkdown,
            EffectiveDate = DateTime.UtcNow,
            ChangedById = editorUserId,
            ChangeNotes = changeNotes,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Updated Day {DayId} (body changed: {BodyChanged}, activation changed: {ActivationChanged}) by user {UserId}",
            day.Id, bodyChanged, activationChanged, editorUserId);
        return existing;
    }
}
