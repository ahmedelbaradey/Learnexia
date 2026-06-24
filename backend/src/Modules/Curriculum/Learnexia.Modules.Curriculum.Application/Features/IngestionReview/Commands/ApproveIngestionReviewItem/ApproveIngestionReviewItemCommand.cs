using Learnexia.Modules.Curriculum.Application.Features.IngestionReview.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Curriculum.Application.Features.IngestionReview.Commands.ApproveIngestionReviewItem;

/// <summary>
/// Admin command to approve a pending <c>IngestionReviewItem</c> (BL-05-BE-8).
///
/// <para>Prerequisites:
/// <list type="bullet">
///   <item>Item must exist (404 otherwise).</item>
///   <item>Item must be in <see cref="Learnexia.Modules.Curriculum.Domain.Enums.ReviewStatus.Pending"/>
///         (409 if already resolved).</item>
/// </list>
/// </para>
///
/// <para>On success: item transitions to Approved, ReviewedByUserId + ReviewedAt are stamped.
/// The approved payload is committed to the pedagogical tree via
/// <see cref="Learnexia.Shared.Contracts.Learning.IPedagogicalTreeWriter"/> (cross-module seam).
/// </para>
/// </summary>
public record ApproveIngestionReviewItemCommand(int ReviewItemId, string? ReviewNotes)
    : ICommand<BaseResponse<IngestionReviewItemDto>>;
