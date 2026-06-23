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
/// Note (BL-04 Q4 decision): this entity is the curriculum-tree publish unit, distinct from
/// <c>learning.ContentVersions</c> (P7-05's per-content-entity snapshot log). They are complementary
/// layers — do NOT unify or rename. See curriculum-system-of-record.md §3.
///
/// Note (BL-04 Q-A decision): SubjectId (int) is the canonical keying dimension. SubjectCode is
/// documentation-only and is not stored as a separate column here.
/// </summary>
public class CurriculumVersion : AggregateRoot
{
    /// <summary>
    /// Loose reference to the subject (no cross-module FK). E.g. the SubjectId from the Learning module.
    /// Canonical keying dimension — do NOT add a SubjectCode column (Q-A decision).
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

    // ── BL-04 lifecycle fields (BE-9) ─────────────────────────────────────────────────────────────

    /// <summary>
    /// When this version was published (Status transitioned to Active). Null while in Draft.
    /// The P7-05 publish action sets this when switching Active versions.
    /// </summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>
    /// The user who published this version (plain int — no cross-module FK). Null while in Draft.
    /// </summary>
    public int? PublishedByUserId { get; set; }

    /// <summary>
    /// When this version was archived (Status transitioned to Archived). Null while Active or Draft.
    /// Set atomically during the Draft → Active version switch (former Active becomes Archived).
    /// </summary>
    public DateTimeOffset? ArchivedAt { get; set; }

    /// <summary>
    /// Optional human-readable notes about this version (e.g. what changed from the prior Active).
    /// </summary>
    public string? Notes { get; set; }

    // Navigation
    public ICollection<CurriculumChunk> Chunks { get; set; } = new List<CurriculumChunk>();
}
