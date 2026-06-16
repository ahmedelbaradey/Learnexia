using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Skills.Commands.SetActive;

/// <summary>
/// Toggles <c>Skill.IsActive</c>. Inactive skills are hidden from student-facing reads.
/// Publishes <see cref="AdminActionPerformedEvent"/> post-commit, best-effort.
///
/// Option-C refactor: GetByCondition + FirstOrDefaultAsync + UpdateAsync moved into
/// ISkillService (GetSkillTrackedByIdAsync + StageSkillUpdateAsync).
/// </summary>
public class SetSkillActiveCommandHandler : BaseResponseHandler, ICommandHandler<SetSkillActiveCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public SetSkillActiveCommandHandler(
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

    public async Task<BaseResponse<string>> Handle(SetSkillActiveCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            var skill = await _service.SkillService.GetSkillTrackedByIdAsync(request.SkillId, cancellationToken);

            if (skill is null)
                return NotFound<string>(_localizer[SharedResourcesKey.SkillNotFound]);

            skill.IsActive = request.IsActive;
            await _service.SkillService.StageSkillUpdateAsync(skill, cancellationToken);

            var action = request.IsActive ? AdminActions.SkillActivated : AdminActions.SkillDeactivated;

            // Raise domain event on the tracked aggregate — dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
            skill.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: action,
                TargetEntityType: nameof(Skill),
                TargetEntityId: request.SkillId,
                Details: $"IsActive={request.IsActive}"));

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
