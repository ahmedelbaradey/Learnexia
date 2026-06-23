using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Curriculum.Domain.Entities;

/// <summary>
/// An auto-inferred candidate edge in the skill prerequisite graph.
/// (curriculum-system-of-record.md §5, BL-04 BE-10)
///
/// KGSuggestion rows are auto-inferred proposals — NEVER auto-published to KnowledgeEdge.
/// Admin approval required. See curriculum-system-of-record.md §5.
///
/// Design rules:
/// - <see cref="SourceNodeId"/> and <see cref="TargetNodeId"/> are plain ints (no cross-module FK/nav).
///   Module isolation: they reference learning.KnowledgeNodes.Id.
/// - <see cref="RelationshipType"/> uses a LOCAL curriculum enum (<see cref="CurriculumRelationshipType"/>)
///   that mirrors learning's EdgeRelationshipType (BL-04 Q-B decision — no cross-module enum reference).
/// - <see cref="SourceCurriculumDocumentId"/> is a nullable plain int, NO FK.
///   BL-01 CurriculumDocument does not exist yet (Q-C decision — forward reference avoidance).
/// </summary>
public class KGSuggestion : AggregateRoot
{
    /// <summary>
    /// Source/prerequisite knowledge node. Plain int — cross-module isolation (no FK/nav to learning.KnowledgeNodes).
    /// </summary>
    public int SourceNodeId { get; set; }

    /// <summary>
    /// Target knowledge node (the one that requires the source as a prerequisite).
    /// Plain int — cross-module isolation.
    /// </summary>
    public int TargetNodeId { get; set; }

    /// <summary>
    /// Relationship type. Local curriculum enum mirroring learning's EdgeRelationshipType.
    /// Stored as int.
    /// </summary>
    public CurriculumRelationshipType RelationshipType { get; set; }

    /// <summary>
    /// Model-assigned strength of this suggested edge. Decimal in [0, 1].
    /// Stored as decimal(5, 4).
    /// </summary>
    public decimal Strength { get; set; }

    /// <summary>
    /// Optional provenance: the curriculum document from which this suggestion was inferred.
    /// Nullable plain int — NO FK (BL-01 CurriculumDocument does not exist yet; Q-C decision).
    /// BL-01 will add the FK constraint when CurriculumDocument is implemented.
    /// </summary>
    public int? SourceCurriculumDocumentId { get; set; }

    /// <summary>
    /// Identifier of the AI model that inferred this suggestion (e.g. "lightrag-v1").
    /// </summary>
    public string InferenceModel { get; set; } = null!;

    /// <summary>
    /// Current approval status of this suggestion. Starts as Pending.
    /// Stored as int.
    /// </summary>
    public KGSuggestionStatus Status { get; set; }

    /// <summary>
    /// When this suggestion was reviewed (approved or rejected). Null while Pending.
    /// </summary>
    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>
    /// The user who reviewed this suggestion (plain int — no cross-module FK). Null while Pending.
    /// </summary>
    public int? ReviewedByUserId { get; set; }
}
