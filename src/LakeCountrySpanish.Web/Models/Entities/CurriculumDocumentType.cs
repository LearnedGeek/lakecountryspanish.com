namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// Categorization of a <see cref="CurriculumDocument"/> — Karen uses
/// "binder" as the umbrella term for anything teachers need to run a
/// class, but the underlying artifacts can vary (a full teacher guide
/// vs. weekly lesson plans vs. song sheets). Starting with one value;
/// the enum exists so we can add new types later without a schema
/// change (see issue #20).
/// </summary>
public enum CurriculumDocumentType
{
    /// <summary>
    /// The teacher binder — Karen's colloquial name for the full
    /// teacher-guide-plus-materials PDF that Cece maintains.
    /// </summary>
    TeacherBinder = 0
}
