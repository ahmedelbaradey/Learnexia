using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Skills.Commands.Edit;

/// <summary>
/// P7-03: Updates an existing Skill.
/// Mass-assignment guard: IsActive and IsDeleted are ignored on the Edit map (see SkillsProfile).
///
/// P7-12: Domain event raised on the tracked Skill aggregate (post-commit via UnitOfWorkBehavior, ADR 0002).
/// </summary>
public class EditSkillCommandHandler : BaseResponseHandler, ICommandHandler<EditSkillCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public EditSkillCommandHandler(
        ILearningServiceManager service,
        ILearningRepositoryManager repository,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _service = service;
        _repository = repository;
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
                // The service fetched the Skill with trackChanges=true — it is in the EF ChangeTracker.
                var tracked = await _repository.Learning
                    .GetByCondition<Skill>(s => s.Id == request.Id, trackChanges: true)
                    .FirstOrDefaultAsync(cancellationToken);

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
