using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Reorder;

/// <summary>
/// Batch-updates SequenceOrder on the given Lessons.
/// All Lessons must belong to the same UnitId — cross-unit reorder is rejected.
/// Publishes <see cref="AdminActionPerformedDomainEvent"/> post-commit, best-effort.
/// Mirrors <c>ReorderUnitsCommandHandler</c> exactly.
///
/// Option-C: all EF calls moved into ILessonService.GetLessonsTrackedByIdsAsync (Infrastructure).
/// Handler is now thin.
/// </summary>
public class ReorderLessonsCommandHandler
    : BaseResponseHandler, ICommandHandler<ReorderLessonsCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ReorderLessonsCommandHandler(
        ILearningServiceManager service,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _service = service;
        _currentUser = currentUser;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<string>> Handle(
        ReorderLessonsCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request is null || request.LessonIds.Count == 0)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            // Load all specified lessons with tracking.
            var lessons = await _service.LessonService.GetLessonsTrackedByIdsAsync(request.LessonIds, cancellationToken);

            if (lessons.Count != request.LessonIds.Count)
                return NotFound<string>(_localizer[SharedResourcesKey.LessonNotFound]);

            // Validate all belong to the same UnitId — cross-unit reorder is forbidden.
            var unitIds = lessons.Select(l => l.UnitId).Distinct().ToList();
            if (unitIds.Count > 1)
                return BadRequest<string>(_localizer[SharedResourcesKey.ReorderCrossTreeForbidden]);

            // Apply new SequenceOrder based on position in the requested list.
            var lessonsById = lessons.ToDictionary(l => l.Id);
            for (var i = 0; i < request.LessonIds.Count; i++)
            {
                lessonsById[request.LessonIds[i]].SequenceOrder = i;
                await _service.LessonService.StageLessonUpdateAsync(lessonsById[request.LessonIds[i]], cancellationToken);
            }

            // Raise domain event on the first tracked lesson (representative for the batch).
            // Dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
            lessonsById[request.LessonIds[0]].RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.LessonReordered,
                TargetEntityType: nameof(Lesson),
                TargetEntityId: 0,
                Details: $"Reordered {request.LessonIds.Count} lessons in UnitId={unitIds[0]}"));

            return Success<string>(_localizer[SharedResourcesKey.OperationCompletedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ReorderLessonsCommand");
            return ServerError<string>();
        }
    }
}
