using Learnexia.Modules.Moderation.Application.Abstractions;
using Learnexia.Modules.Moderation.Application.Features.ModerationQueue.Dtos;
using Learnexia.Modules.Moderation.Domain.Enums;
using Learnexia.Modules.Moderation.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Moderation.Application.Features.ModerationQueue.Commands.ReviewItem;

/// <summary>
/// Handles <see cref="ReviewModerationItemCommand"/>. Enforces valid status transitions,
/// applies the review fields, and raises <see cref="AdminActionPerformedDomainEvent"/> on
/// the aggregate for post-commit relay to the P7-12 audit log.
///
/// <para>Option C: the handler loads the entity via <see cref="IModerationItemService"/>
/// (no DbContext or EF types here); the UnitOfWorkBehavior commits the staged mutation and
/// dispatches the domain event after commit.</para>
///
/// <para>Allowed transitions (OQ-4 spec):</para>
/// <list type="bullet">
///   <item><see cref="ModerationStatus.Pending"/> → Approved | Rejected | Flagged</item>
///   <item><see cref="ModerationStatus.Flagged"/> → Approved | Rejected</item>
///   <item><see cref="ModerationStatus.Approved"/> and <see cref="ModerationStatus.Rejected"/> are terminal.</item>
/// </list>
/// </summary>
public class ReviewModerationItemCommandHandler
    : BaseResponseHandler, ICommandHandler<ReviewModerationItemCommand, BaseResponse<ModerationItemDto>>
{
    private readonly IModerationItemService _itemService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ReviewModerationItemCommandHandler(
        IModerationItemService itemService,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _itemService  = itemService;
        _currentUser  = currentUser;
        _logger       = logger;
        _localizer    = localizer;
    }

    public async Task<BaseResponse<ModerationItemDto>> Handle(
        ReviewModerationItemCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _itemService.LoadTrackedAsync(request.Id, cancellationToken);

            if (item is null)
                return NotFound<ModerationItemDto>(_localizer[SharedResourcesKey.ModerationItemNotFound]);

            // Guard: terminal items cannot be re-reviewed.
            if (item.Status == ModerationStatus.Approved || item.Status == ModerationStatus.Rejected)
                return BadRequest<ModerationItemDto>(_localizer[SharedResourcesKey.ModerationItemAlreadyTerminal]);

            // Guard: Flagged items can only transition to Approved or Rejected, not re-Flagged.
            if (item.Status == ModerationStatus.Flagged && request.Decision == ModerationStatus.Flagged)
                return BadRequest<ModerationItemDto>(_localizer[SharedResourcesKey.ModerationItemAlreadyFlagged]);

            var adminUserId   = _currentUser.UserId.GetValueOrDefault();
            var previousStatus = item.Status;

            // Apply review fields.
            item.Status               = request.Decision;
            item.ReviewedByUserId     = adminUserId;
            item.ReviewedAtUtc        = DateTime.UtcNow;
            item.ReviewDecisionReason = request.Reason;

            // Raise domain event on the aggregate — dispatched post-commit by UnitOfWorkBehavior.
            // Details are PII-safe: ids + before→after status only (no free-text reason).
            item.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId:      adminUserId,
                Action:           AdminActions.ModerationItemReviewed,
                TargetEntityType: nameof(Learnexia.Modules.Moderation.Domain.Entities.ModerationItem),
                TargetEntityId:   item.Id,
                Details:          $"Status: {previousStatus} → {request.Decision}"));

            // Return the DTO for the controller response.
            var dto = new ModerationItemDto(
                item.Id,
                item.SourceEventId,
                item.Source.ToString(),
                item.ContentReference,
                item.StudentId,
                item.TaskKind,
                item.SubjectCode,
                item.Grade,
                item.Status.ToString(),
                item.SafetyVerdict,
                item.DetectedAt,
                item.CreatedAt);

            var response = Success(dto);
            response.Message = _localizer[SharedResourcesKey.ModerationItemReviewedSuccessfully];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error: in ReviewModerationItemCommand (Id={request.Id})");
            return ServerError<ModerationItemDto>();
        }
    }
}
