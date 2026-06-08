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

namespace Learnexia.Modules.Learning.Application.Features.Skills.Commands.SetActive;

/// <summary>
/// Toggles <c>Skill.IsActive</c>. Inactive skills are hidden from student-facing reads.
/// Publishes <see cref="AdminActionPerformedEvent"/> post-commit, best-effort.
/// </summary>
public class SetSkillActiveCommandHandler : BaseResponseHandler, ICommandHandler<SetSkillActiveCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public SetSkillActiveCommandHandler(
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

    public async Task<BaseResponse<string>> Handle(SetSkillActiveCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            var skill = await _repository.Learning
                .GetByCondition<Skill>(s => s.Id == request.SkillId, trackChanges: true)
                .FirstOrDefaultAsync(cancellationToken);

            if (skill is null)
                return NotFound<string>(_localizer[SharedResourcesKey.SkillNotFound]);

            skill.IsActive = request.IsActive;
            await _repository.Learning.UpdateAsync(skill);

            var action = request.IsActive ? AdminActions.SkillActivated : AdminActions.SkillDeactivated;

            // Best-effort post-commit event publish.
            try
            {
                await _publisher.Publish(new AdminActionPerformedEvent(
                    EventId: Guid.NewGuid(),
                    OccurredAtUtc: DateTime.UtcNow,
                    AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                    Action: action,
                    TargetEntityType: nameof(Skill),
                    TargetEntityId: request.SkillId,
                    Details: $"IsActive={request.IsActive}"), cancellationToken);
            }
            catch (Exception publishEx)
            {
                _logger.LogError(publishEx, $"P7-03: AdminActionPerformedEvent publish failed for SetSkillActiveCommand, SkillId={request.SkillId}");
            }

            var message = request.IsActive
                ? _localizer[SharedResourcesKey.SkillActivatedSuccessfully]
                : _localizer[SharedResourcesKey.SkillDeactivatedSuccessfully];

            return Success<string>(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in SetSkillActiveCommand");
            return ServerError<string>();
        }
    }
}
