namespace LakeCountrySpanish.Web;

public static class AppRoles
{
    /// <summary>
    /// Full-power role. Everything Author + Teacher can do, plus
    /// business-critical + destructive surfaces: Stripe config, student
    /// / scheduling management, user + role administration. Karen and
    /// Cece deliberately do NOT hold this — the standing rule is that
    /// the Stripe + student/scheduling surface is dormant and untested
    /// for LCS's usage pattern, so it stays walled off. Currently the
    /// only Admin is Mark's admin@lakecountryspanish.com seed account.
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>
    /// Content author. Grants write access to the surfaces Karen and
    /// Cece actually run: curriculum authoring (create/edit lessons,
    /// blocks, publish), binder PDF upload/replace/delete, and the
    /// Program CRUD flow. Deliberately does NOT grant access to user
    /// / role management, Stripe config, or student scheduling —
    /// those stay Admin-only per the standing role-scope split.
    /// Karen + Cece hold Teacher + Author. Sub teachers hold Teacher
    /// only.
    /// </summary>
    public const string Author = "Author";

    /// <summary>
    /// Customer / parent role — created via the enrollment path, not
    /// via the admin surfaces.
    /// </summary>
    public const string Student = "Student";

    /// <summary>
    /// Read-only staff role. Grants access to view + print curriculum,
    /// download binders, and the teacher dashboard. No write anywhere.
    /// Every staff account needs at least Teacher; Author is layered
    /// on top for the co-founders.
    /// </summary>
    public const string Teacher = "Teacher";
}
