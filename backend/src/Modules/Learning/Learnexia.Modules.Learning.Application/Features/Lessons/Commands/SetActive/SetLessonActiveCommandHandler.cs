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

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Commands.SetActive;

/// <summary>
/// Toggles <c>Lesson.IsActive</c>. Inactive lessons are hidden from student-facing reads.
/// Publishes <see cref="AdminActionPerformedEvent"/> post-commit, best-effort.
/// Mirrors <c>SetUnitActiveCommandHandler</c> exactly.
/// </summary>
public class SetLessonActiveCommandHandler
    : BaseResponseHandler, ICommandHandler<SetLessonActiveCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public SetLessonActiveCommandHandler(
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
        SetLessonActiveCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            var lesson = await _repository.Learning
                .GetByCondition<Lesson>(l => l.Id == request.LessonId, trackChanges: true)
                .FirstOrDefaultAsync(cancellationToken);

            if (lesson is null)
                return NotFound<string>(_localizer[SharedResourcesKey.LessonNotFound]);

            lesson.IsActive = request.IsActive;
            await _repository.Learning.UpdateAsync(lesson);

            var action = request.IsActive ? AdminActions.LessonActivated : AdminActions.LessonDeactivated;

            // Best-effort post-commit event publish.
            try
            {
                await _publisher.Publish(new AdminActionPerformedEvent(
                    EventId: Guid.NewGuid(),
                    OccurredAtUtc: DateTime.UtcNow,
                    AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                    Action: action,
                    TargetEntityType: nameof(Lesson),
                    TargetEntityId: request.LessonId,
                    Details: $"IsActive={request.IsActive}"),
                    cancellationToken);
            }
            catch (Exception publishEx)
            {
                _logger.LogError(publishEx, $"P7-02: AdminActionPerformedEvent publish failed for LessonId={request.LessonId}");
            }

            var message = request.IsActive
                ? _localizer[SharedResourcesKey.LessonActivatedSuccessfully]
                : _localizer[SharedResourcesKey.LessonDeactivatedSuccessfully];

            return Success<string>(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in SetLessonActiveCommand");
            return ServerError<string>();
        }
    }
}
