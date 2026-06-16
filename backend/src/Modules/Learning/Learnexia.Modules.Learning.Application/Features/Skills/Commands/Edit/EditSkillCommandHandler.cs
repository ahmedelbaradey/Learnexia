using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Skills.Commands.Edit;

/// <summary>
/// P7-03: Updates an existing Skill.
/// Mass-assignment guard: IsActive and IsDeleted are ignored on the Edit map (see SkillsProfile).
///
/// P7-12: Domain event raised on the tracked Skill aggregate (post-commit via UnitOfWorkBehavior, ADR 0002).
///
/// Option-C refactor: the post-update re-fetch via _repository.Learning.GetByCondition replaced
/// by _service.SkillService.GetSkillTrackedByIdAsync, which returns the entity already in
/// EF's identity map (the UpdateAsync in LearningBaseService.UpdateAsync fetched it with trackChanges=true).
/// </summary>
public class EditSkillCommandHandler : BaseResponseHandler, ICommandHandler<EditSkillCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public EditSkillCommandHandler(
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

    public async Task<BaseResponse<string>> Handle(EditSkillCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            var result = await _service.SkillService.UpdateAsync(request);

            if (result.Successed)
            {
                // LearningBaseService.UpdateAsync fetches with trackChanges=true — the entity is
                // already in EF's identity map; GetSkillTrackedByIdAsync returns that same instance.
                var tracked = await _service.SkillService.GetSkillTrackedByIdAsync(request.Id, cancellationToken);

                // Raise domain event on the tracked aggregate — dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
                tracked?.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                    AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                    Action: AdminActions.SkillUpdated,
                    TargetEntityType: nameof(Skill),
                    TargetEntityId: request.Id,
                    Details: null));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in EditSkillCommand");
            return ServerError<string>();
        }
    }
}
