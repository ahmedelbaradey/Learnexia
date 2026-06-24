using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Curriculum.Domain.Entities;

/// <summary>
/// A low-confidence ingestion output that is withheld from the published curriculum tree
/// pending human (admin) review and approval.
///
/// Context (BL-05 BE-1, Q5 decision):
/// When the Python ingestion worker assigns a confidence score below the configured threshold
/// (default 0.7, config-driven — never hardcoded) to an extracted hierarchy node or
/// classification, the .NET ingest-advance service creates an IngestionReviewItem instead of
/// committing the proposed node/chunk to the live curriculum tree. The item stays Pending until
/// an admin approves (triggering the actual hierarchy write via the IPedagogicalTreeWriter seam)
/// or rejects (discarding the proposal).
///
/// This is SEPARATE from <see cref="KGSuggestion"/>, which holds auto-inferred KG edge proposals
/// for BL-03. KGSuggestion concerns the prerequisite graph (edges between KnowledgeNodes);
/// IngestionReviewItem concerns ingestion content classification (hierarchy node proposals).
///
/// Design rules:
/// - <see cref="CurriculumDocumentId"/> is an intra-module FK to <see cref="CurriculumDocument"/>
///   (same curriculum schema — FK is permitted).
/// - <see cref="CurriculumVersionId"/> is an intra-module FK to <see cref="CurriculumVersion"/>
///   (nullable — not yet resolved when a review item is created before version resolution).
/// - <see cref="ReviewedByUserId"/> is a plain int (no cross-module FK/nav — module isolation).
/// - <see cref="Status"/> defaults to <see cref="ReviewStatus.Pending"/> on creation.
/// - <see cref="Confidence"/> uses decimal(5,4) to match KGSuggestion.Strength precision.
/// </summary>
public class IngestionReviewItem : AggregateRoot
{
    /// <summary>
    /// Intra-module FK to the <see cref="CurriculumDocument"/> that produced this review item.
    /// Cascade delete: removing the document removes all its review items.
    /// </summary>
    public int CurriculumDocumentId { get; set; }

    /// <summary>
    /// Navigation property for the owning CurriculumDocument (intra-module).
    /// </summary>
    public CurriculumDocument CurriculumDocument { get; set; } = null!;

    /// <summary>
    /// Intra-module FK to the Draft <see cref="CurriculumVersion"/> under which this item was ingested.
    /// Nullable: populated after CurriculumVersionResolver resolves (or creates) the Draft version
    /// for this ingestion run.
    /// </summary>
    public int? CurriculumVersionId { get; set; }

    /// <summary>
    /// Source reference from the parsed artifact (e.g., page number, block ID, section reference).
    /// Used by the reviewer to locate the source material in the original document.
    /// </summary>
    public string SourceReference { get; set; } = null!;

    /// <summary>
    /// The hierarchy classification proposed by the LLM extractor (e.g., "Grade 5 > Math > Fractions > Unit 3").
    /// Stored as a string; the structured representation lives in <see cref="PayloadJson"/>.
    /// </summary>
    public string SuggestedClassification { get; set; } = null!;

    /// <summary>
    /// Full structured payload (proposed node/chunk content) as JSON.
    /// Carries the raw LLM-extracted data so the admin reviewer has full context.
    /// Written with column type "jsonb" in Postgres.
    /// </summary>
    public string PayloadJson { get; set; } = null!;

    /// <summary>
    /// Model-assigned confidence score in [0, 1]. Items with Confidence below the configured
    /// threshold (default 0.7) are routed here instead of being committed to the curriculum tree.
    /// Stored as decimal(5,4) — matches KGSuggestion.Strength precision.
    /// </summary>
    public decimal Confidence { get; set; }

    /// <summary>
    /// Current review status. Defaults to <see cref="ReviewStatus.Pending"/> on creation.
    /// Stored as int.
    /// </summary>
    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;

    /// <summary>
    /// The admin who reviewed this item (approved or rejected). Null while Pending.
    /// Plain int — cross-module isolation (no FK/nav to identity.Users).
    /// </summary>
    public int? ReviewedByUserId { get; set; }

    /// <summary>
    /// When this item was reviewed (approved or rejected). Null while Pending.
    /// </summary>
    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>
    /// Optional reviewer notes or reason for rejection. Null while Pending.
    /// </summary>
    public string? ReviewNotes { get; set; }
}
