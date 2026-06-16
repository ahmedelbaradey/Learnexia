using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Questions.Commands.SetActive;

/// <summary>
/// Toggles the <c>IsActive</c> flag on a <see cref="QuizQuestion"/>.
/// Inactive questions are hidden from student-facing reads but remain in the DB.
///
/// P7-12: Domain event raised on the QuizQuestion aggregate — dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12 fix).
///
/// Option-C refactor: FirstOrDefaultAsync moved into IQuizQuestionService.GetQuestionTrackedAsync.
/// </summary>
public class SetQuestionActiveCommandHandler
    : BaseResponseHandler, ICommandHandler<SetQuestionActiveCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public SetQuestionActiveCommandHandler(
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
        SetQuestionActiveCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            var question = await _service.QuizQuestionService.GetQuestionTrackedAsync(request.Id, cancellationToken);

            if (question is null)
                return NotFound<string>(_localizer[SharedResourcesKey.QuizQuestionNotFound]);

            question.IsActive = request.IsActive;
            await _service.QuizQuestionService.StageQuestionUpdateAsync(question, cancellationToken);

            var action = request.IsActive
                ? AdminActions.QuizQuestionActivated
                : AdminActions.QuizQuestionDeactivated;

            // Raise domain event on the tracked aggregate — dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
            question.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: action,
                TargetEntityType: nameof(QuizQuestion),
                TargetEntityId: request.Id,
                Details: $"IsActive={request.IsActive}"));

            var message = request.IsActive
                ? _localizer[SharedResourcesKey.QuizQuestionActivatedSuccessfully]
                : _localizer[SharedResourcesKey.QuizQuestionDeactivatedSuccessfully];

            return Success<string>(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in SetQuestionActiveCommand");
            return ServerError<string>();
        }
    }
}
