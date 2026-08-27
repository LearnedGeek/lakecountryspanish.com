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
    /// Persists a new Program AND provisions Stripe Product + Prices for it.
    /// After this call the program has non-null <c>StripeProductId</c>,
    /// <c>StripeFullPriceId</c>, and (if installments enabled) <c>StripeInstallmentPriceId</c>.
    /// </summary>
    Task<EnrollmentProgram> CreateAsync(EnrollmentProgram program, CancellationToken ct = default);

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
}
