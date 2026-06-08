using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Learning.Domain.Entities;

/// <summary>
/// Immutable publish-time snapshot of a single curriculum entity (Subject, Unit, Lesson, or QuizQuestion).
///
/// P7-05: Records every publish event for a versioned entity. Each publish produces one new row
/// with a monotonically increasing <see cref="VersionNumber"/> within the
/// <c>(EntityType, EntityId)</c> partition. Rollback = re-applying a prior row's
/// <see cref="Snapshot"/> to the live entity in an explicit transaction.
///
/// Design decisions:
/// - <see cref="EntityType"/> + <see cref="EntityId"/> form a polymorphic loose reference
///   (no cross-entity FK — module isolation rule). The application layer resolves the pair.
/// - <see cref="Snapshot"/> is a jsonb column containing the full serialized state of the entity
///   at the moment of publish. Per-entity snapshot (not sub-tree) — tree publish = one transaction,
///   N per-entity version rows (one per entity in the tree).
/// - <see cref="Language"/> records the <see cref="ContentLanguage"/> of the owning Subject tree
///   at publish time. Publishing an Ar tree does not touch the En sibling; this field provides
///   traceability and feeds the publication-coverage query.
/// - <see cref="PublishedByUserId"/> is stamped from the JWT admin claim, NOT a client-supplied value.
/// - <see cref="PublishedAtUtc"/>: timestamptz. Per HANDOFF P4-11, callers must call
///   <c>.ToUniversalTime()</c> at the mapping boundary when reading back under
///   <c>Npgsql.EnableLegacyTimestampBehavior=true</c>.
///
/// Derives from <see cref="FullAuditedEntity"/> (not <see cref="AggregateRoot"/>) — version rows
/// are append-only records; they carry no independent domain-event lifecycle.
/// Soft-delete (<c>IsDeleted</c>) is inherited and a global query filter is registered in
/// <see cref="LearningDbContext"/> so soft-deleted version rows are invisible by default.
/// </summary>
public class ContentVersion : FullAuditedEntity
{
    /// <summary>
    /// Which curriculum entity type this snapshot belongs to (Subject/Unit/Lesson/QuizQuestion).
    /// Stored as int via HasConversion&lt;int&gt;() in ContentVersionConfig.
    /// Together with <see cref="EntityId"/> forms the loose polymorphic reference.
    /// </summary>
    public VersionedEntityType EntityType { get; set; }

    /// <summary>
    /// The primary key of the versioned entity. Plain int — no FK constraint (module isolation).
    /// Interpreted in context of <see cref="EntityType"/> by the application layer.
    /// </summary>
    public int EntityId { get; set; }

    /// <summary>
    /// Monotonically increasing version counter within the <c>(EntityType, EntityId)</c> partition.
    /// Version 1 = first publish; each subsequent publish increments by 1.
    /// Indexed as part of the unique composite index <c>(EntityType, EntityId, VersionNumber)</c>.
    /// </summary>
    public int VersionNumber { get; set; }

    /// <summary>
    /// Full serialized state of the entity at the moment of publish. Stored as jsonb.
    /// Snapshot scope: single-entity payload (not the full sub-tree). Tree publish = N rows,
    /// one per entity in the tree, all within a single explicit transaction.
    /// Used by rollback to restore prior entity state.
    /// </summary>
    public string Snapshot { get; set; } = null!;

    /// <summary>
    /// Admin user id of the person who triggered the publish. Read from JWT claim at the
    /// application layer (never trust a client-supplied value).
    /// Plain int — no FK constraint (cross-module isolation: admin identity lives in Identity module).
    /// </summary>
    public int PublishedByUserId { get; set; }

    /// <summary>
    /// UTC timestamp of when the publish occurred.
    /// timestamptz in PostgreSQL. Callers must call <c>.ToUniversalTime()</c> at the mapping boundary
    /// when reading back under <c>Npgsql.EnableLegacyTimestampBehavior=true</c> (HANDOFF P4-11).
    /// </summary>
    public DateTime PublishedAtUtc { get; set; }

    /// <summary>
    /// Language of the owning Subject tree at publish time (Ar / En).
    /// Stored as int via HasConversion&lt;int&gt;(). Enables the publication-coverage query
    /// (<c>GetPublicationCoverageQuery</c>) to distinguish which language trees have been published
    /// and surfaces one-sided coverage gaps (e.g. Ar-Math published, En-Math still draft).
    /// </summary>
    public ContentLanguage Language { get; set; }
}
