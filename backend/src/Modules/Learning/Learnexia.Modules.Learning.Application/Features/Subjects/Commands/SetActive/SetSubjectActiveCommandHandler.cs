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

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Commands.SetActive;

/// <summary>
/// Toggles <c>Subject.IsActive</c>. Inactive subjects are hidden from student-facing reads.
/// Publishes <see cref="AdminActionPerformedEvent"/> post-commit, best-effort.
/// </summary>
public class SetSubjectActiveCommandHandler : BaseResponseHandler, ICommandHandler<SetSubjectActiveCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public SetSubjectActiveCommandHandler(
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

    public async Task<BaseResponse<string>> Handle(SetSubjectActiveCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            var subject = await _repository.Learning
                .GetByCondition<Subject>(s => s.Id == request.SubjectId, trackChanges: true)
                .FirstOrDefaultAsync(cancellationToken);

            if (subject is null)
                return NotFound<string>(_localizer[SharedResourcesKey.SubjectNotFound]);

            subject.IsActive = request.IsActive;
            await _repository.Learning.UpdateAsync(subject);

            var action = request.IsActive ? AdminActions.SubjectActivated : AdminActions.SubjectDeactivated;

            // Best-effort post-commit event publish.
            try
            {
                await _publisher.Publish(new AdminActionPerformedEvent(
                    EventId: Guid.NewGuid(),
                    OccurredAtUtc: DateTime.UtcNow,
                    AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                    Action: action,
                    TargetEntityType: nameof(Subject),
                    TargetEntityId: request.SubjectId,
                    Details: $"IsActive={request.IsActive}"), cancellationToken);
            }
            catch (Exception publishEx)
            {
                _logger.LogError(publishEx, $"P7-01: AdminActionPerformedEvent publish failed for SubjectId={request.SubjectId}");
            }

            var message = request.IsActive
                ? _localizer[SharedResourcesKey.SubjectActivatedSuccessfully]
                : _localizer[SharedResourcesKey.SubjectDeactivatedSuccessfully];

            return Success<string>(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in SetSubjectActiveCommand");
            return ServerError<string>();
        }
    }
}
