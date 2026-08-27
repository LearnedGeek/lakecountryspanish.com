using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Web.Services.Programs;

/// <summary>
/// CRUD for <see cref="EnrollmentProgram"/> that also provisions the Stripe
/// side of the world (Product + one-time Full Price + recurring Installment
/// Price) on first save. Enforces the "pricing is locked once Stripe knows
/// about it" rule so Prices can never drift from Program state — if Karen
/// wants different pricing she duplicates the Program instead.
/// </summary>
public interface IEnrollmentProgramService
{
    Task<IReadOnlyList<EnrollmentProgram>> ListAllAsync(bool includeInactive = false, CancellationToken ct = default);

    /// <summary>Case-insensitive slug lookup for the public /join/{slug} landing page.</summary>
    Task<EnrollmentProgram?> GetBySlugAsync(string slug, CancellationToken ct = default);

    Task<EnrollmentProgram?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Persists a new Program. When <paramref name="provisionStripe"/> is true
    /// (default), also provisions Stripe Product + Prices — the program has
    /// non-null <c>StripeProductId</c>, <c>StripeFullPriceId</c>, and (if
    /// installments enabled) <c>StripeInstallmentPriceId</c> after the call.
    /// When false, saves the row as a draft with no Stripe wiring; call
    /// <see cref="PublishAsync"/> later to provision Stripe and activate.
    /// </summary>
    Task<EnrollmentProgram> CreateAsync(EnrollmentProgram program, CancellationToken ct = default, bool provisionStripe = true);

    /// <summary>
    /// Updates metadata on an existing Program. Rejects (via
    /// <see cref="InvalidOperationException"/>) any change to price-affecting
    /// fields once Stripe wiring exists: <c>FullPrice</c>,
    /// <c>InstallmentCount</c>, or turning <c>InstallmentsEnabled</c> back on
    /// after it was disabled at a different price. Karen can safely edit
    /// name, description, dates, waiver, contact info, active/listed flags.
    /// </summary>
    Task<EnrollmentProgram> UpdateAsync(EnrollmentProgram program, CancellationToken ct = default);

    /// <summary>
    /// Hard-deletes a program if it has zero enrollments. Best-effort archives
    /// the Stripe Product on the way out. Throws
    /// <see cref="InvalidOperationException"/> if any enrollments exist — the
    /// caller should surface the message to the user and suggest archiving
    /// (unchecking IsActive + IsListed) instead.
    /// </summary>
    Task DeleteAsync(int programId, CancellationToken ct = default);

    /// <summary>
    /// Activates a draft: provisions Stripe (if not already wired) and sets
    /// <c>IsActive = true</c>. Idempotent for a program that's already
    /// Stripe-wired — just flips <c>IsActive</c>. Callers should have already
    /// validated publish-required fields (via the ViewModel's
    /// <c>Validate</c>) before calling this; this method assumes valid input.
    /// </summary>
    Task<EnrollmentProgram> PublishAsync(int programId, CancellationToken ct = default);

    /// <summary>
    /// Creates a draft copy of an existing program with an auto-suffixed slug
    /// (<c>{source}-copy</c>, or <c>-copy-2</c>, <c>-copy-3</c>, ... on collision),
    /// no Stripe wiring, and <c>IsActive = false</c> + <c>IsListed = false</c>.
    /// Returns the new draft so the caller can redirect to its edit page.
    /// </summary>
    Task<EnrollmentProgram> DuplicateAsync(int sourceProgramId, CancellationToken ct = default);
}
