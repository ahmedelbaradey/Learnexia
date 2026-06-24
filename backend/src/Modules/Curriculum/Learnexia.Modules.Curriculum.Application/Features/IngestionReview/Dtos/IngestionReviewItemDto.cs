using Learnexia.Modules.Curriculum.Domain.Enums;

namespace Learnexia.Modules.Curriculum.Application.Features.IngestionReview.Dtos;

/// <summary>
/// DTO for a single <c>IngestionReviewItem</c> returned by the admin review queue
/// (BL-05-BE-7 GetIngestionReviewItems, BE-8 ApproveIngestionReviewItem).
///
/// <para><strong>Security:</strong> <c>PayloadJson</c> is included — admin eyes only.
/// The controller is gated by <c>[Authorize(Policy = "Curriculum.ReviewIngest")]</c>.</para>
/// </summary>
public class IngestionReviewItemDto
{
    public int Id { get; set; }
    public int CurriculumDocumentId { get; set; }
    public int? CurriculumVersionId { get; set; }
    public string SourceReference { get; set; } = null!;
    public string SuggestedClassification { get; set; } = null!;
    public string PayloadJson { get; set; } = null!;
    public decimal Confidence { get; set; }
    public ReviewStatus Status { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTime CreatedAt { get; set; }
}
