using Learnexia.Modules.Curriculum.Domain.Enums;

namespace Learnexia.Modules.Curriculum.Application.Features.KGSuggestions.Dtos;

/// <summary>
/// DTO for a single <c>KGSuggestion</c> row returned by the admin review queue
/// (BL-03-BE-7 ListKGSuggestions, BE-8 ApproveKGSuggestion, BE-9 RejectKGSuggestion).
///
/// <para><strong>Admin-only:</strong> the controller is gated by <c>[Authorize(Policy = AdminOnly)]</c>.</para>
/// </summary>
public class KGSuggestionDto
{
    public int Id { get; set; }

    /// <summary>Source (prerequisite) KnowledgeNode.Id (plain int, no cross-module FK).</summary>
    public int SourceNodeId { get; set; }

    /// <summary>Target KnowledgeNode.Id (plain int, no cross-module FK).</summary>
    public int TargetNodeId { get; set; }

    /// <summary>Relationship type (Prerequisite or Related).</summary>
    public CurriculumRelationshipType RelationshipType { get; set; }

    /// <summary>Model-assigned edge strength in [0, 1].</summary>
    public decimal Strength { get; set; }

    /// <summary>Identifier of the AI model that inferred this suggestion (e.g. "lightrag-v1-mock").</summary>
    public string InferenceModel { get; set; } = null!;

    /// <summary>Optional provenance: curriculum document that triggered inference. Nullable.</summary>
    public int? SourceCurriculumDocumentId { get; set; }

    /// <summary>Current approval status.</summary>
    public KGSuggestionStatus Status { get; set; }

    /// <summary>When this suggestion was reviewed (null while Pending).</summary>
    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>User id of the reviewer (null while Pending).</summary>
    public int? ReviewedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }
}
