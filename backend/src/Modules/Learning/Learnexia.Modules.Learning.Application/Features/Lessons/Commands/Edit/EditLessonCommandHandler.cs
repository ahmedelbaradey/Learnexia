using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Edit;

/// <summary>
/// P7-02: Updates an existing Lesson and emits <see cref="AdminActionPerformedDomainEvent"/>
/// via post-commit domain-event relay (ADR 0002 / P7-12 fix).
///
/// Option-C: all EF calls moved into ILessonService.StageEditLessonAsync (Infrastructure).
/// Handler is now thin — validate/authorize → service → event → return.
/// </summary>
public class EditLessonCommandHandler : BaseResponseHandler, ICommandHandler<EditLessonCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public EditLessonCommandHandler(
        ILearningServiceManager service,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _logger = logger;
        _service = service;
        _currentUser = currentUser;
        _localizer = localizer;
    }

    public async Task<BaseResponse<string>> Handle(EditLessonCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            // StageEditLessonAsync: loads tracked lesson, maps fields, stages UpdateAsync, returns instance.
            var lesson = await _service.LessonService.StageEditLessonAsync(request, cancellationToken);

            if (lesson is null)
                return NotFound<string>(_localizer[SharedResourcesKey.LessonNotFound]);

            // Raise domain event on the tracked aggregate — dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
            lesson.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.LessonUpdated,
                TargetEntityType: nameof(Lesson),
                TargetEntityId: request.Id,
                Details: null));

            return Success<string>(_localizer[SharedResourcesKey.OperationCompletedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in EditLessonCommand");
            return ServerError<string>();
        }
    }
}
