namespace Learnexia.Modules.Curriculum.Domain.Enums;

/// <summary>
/// Human-review status of an <see cref="Entities.IngestionReviewItem"/>.
/// Stored as int in the database via HasConversion&lt;int&gt;().
///
/// This is SEPARATE from <see cref="KGSuggestionStatus"/> (which applies to BL-03 KG graph edges).
/// IngestionReviewItem holds low-confidence ingestion outputs (nodes/classification proposals)
/// that are withheld from the published curriculum tree pending admin review.
/// Default threshold: 0.7 (config-driven — never hardcoded).
///
/// Values: Pending=0, Approved=1, Rejected=2.
/// (BL-05 BE-1, Q5 decision)
/// </summary>
public enum ReviewStatus
{
    Pending  = 0,
    Approved = 1,
    Rejected = 2,
}
