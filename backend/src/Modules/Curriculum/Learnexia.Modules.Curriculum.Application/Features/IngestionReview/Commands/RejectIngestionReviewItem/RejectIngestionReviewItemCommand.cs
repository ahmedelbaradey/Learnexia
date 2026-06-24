using Learnexia.Modules.Curriculum.Application.Features.IngestionReview.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Curriculum.Application.Features.IngestionReview.Commands.RejectIngestionReviewItem;

/// <summary>
/// Admin command to reject a pending <c>IngestionReviewItem</c> (BL-05-BE-8).
///
/// <para>Prerequisites:
/// <list type="bullet">
///   <item>Item must exist (404 otherwise).</item>
///   <item>Item must be in <see cref="Learnexia.Modules.Curriculum.Domain.Enums.ReviewStatus.Pending"/>
///         (409 if already resolved).</item>
/// </list>
/// </para>
///
/// <para>On success: item transitions to Rejected, ReviewedByUserId + ReviewedAt are stamped.
/// Rejected items are withheld permanently (admins may re-ingest the document to regenerate).</para>
/// </summary>
public record RejectIngestionReviewItemCommand(int ReviewItemId, string? ReviewNotes)
    : ICommand<BaseResponse<IngestionReviewItemDto>>;
