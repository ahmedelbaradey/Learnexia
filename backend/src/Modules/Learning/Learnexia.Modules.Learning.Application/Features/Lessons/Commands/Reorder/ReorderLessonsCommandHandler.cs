using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Reorder;

/// <summary>
/// Batch-updates SequenceOrder on the given Lessons.
/// All Lessons must belong to the same UnitId — cross-unit reorder is rejected.
/// Publishes <see cref="AdminActionPerformedEvent"/> post-commit, best-effort.
/// Mirrors <c>ReorderUnitsCommandHandler</c> exactly.
/// </summary>
public class ReorderLessonsCommandHandler
    : BaseResponseHandler, ICommandHandler<ReorderLessonsCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ReorderLessonsCommandHandler(
        ILearningRepositoryManager repository,
        ICurrentUserService currentUser,
        IPublisher publisher,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
        _currentUser = currentUser;
        _publisher = publisher;
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
            var lessons = await _repository.Learning
                .GetByCondition<Lesson>(l => request.LessonIds.Contains(l.Id), trackChanges: true)
                .ToListAsync(cancellationToken);

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
                await _repository.Learning.UpdateAsync(lessonsById[request.LessonIds[i]]);
            }

            // Best-effort post-commit event publish.
            try
            {
                await _publisher.Publish(new AdminActionPerformedEvent(
                    EventId: Guid.NewGuid(),
                    OccurredAtUtc: DateTime.UtcNow,
                    AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                    Action: AdminActions.LessonReordered,
                    TargetEntityType: nameof(Lesson),
                    TargetEntityId: 0,
                    Details: $"Reordered {request.LessonIds.Count} lessons in UnitId={unitIds[0]}"),
                    cancellationToken);
            }
            catch (Exception publishEx)
            {
                _logger.LogError(publishEx, "P7-02: AdminActionPerformedEvent publish failed for ReorderLessonsCommand");
            }

            return Success<string>(_localizer[SharedResourcesKey.OperationCompletedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ReorderLessonsCommand");
            return ServerError<string>();
        }
    }
}
