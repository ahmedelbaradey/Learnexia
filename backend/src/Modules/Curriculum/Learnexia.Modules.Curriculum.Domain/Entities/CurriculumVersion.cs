using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Curriculum.Domain.Entities;

/// <summary>
/// Represents a versioned snapshot of curriculum content for a given subject and language.
///
/// Rules (curriculum-system-of-record.md §3):
/// - Exactly one <c>Active</c> version per (SubjectId, Language) at any point — enforced by
///   a filtered unique index in <c>CurriculumVersionConfig</c>.
/// - Published (Active) content is immutable. Corrections are a new Draft → Active atomic switch.
/// - Visibility governs retrieval: <c>chunk_embeddings_bge_m3</c> only returns chunks whose version
///   has <c>Status = Active</c> (P3-07 retrieval filter).
///
/// BL-04 will extend this entity with additional lifecycle fields; P3-07 provides the minimal slice.
/// </summary>
public class CurriculumVersion : AggregateRoot
{
    /// <summary>
    /// Loose reference to the subject (no cross-module FK). E.g. the SubjectId from the Learning module.
    /// </summary>
    public int SubjectId { get; set; }

    /// <summary>
    /// Language of the curriculum content covered by this version.
    /// Stored as int.
    /// </summary>
    public ContentLanguage Language { get; set; }

    /// <summary>
    /// Lifecycle status. Filtered unique index ensures at most one Active per (SubjectId, Language).
    /// Stored as int.
    /// </summary>
    public CurriculumVersionStatus Status { get; set; }

    /// <summary>
    /// Human-readable version label (e.g. "MVP-G3-Math-v1"). The seeder assigns this when creating
    /// the seeded MVP corpus version (FINAL LOCKED AI ARCHITECTURE §4); the P7-05 publish surface
    /// displays it. Stable once assigned.
    /// </summary>
    public string Name { get; set; } = null!;

    // Navigation
    public ICollection<CurriculumChunk> Chunks { get; set; } = new List<CurriculumChunk>();
}
