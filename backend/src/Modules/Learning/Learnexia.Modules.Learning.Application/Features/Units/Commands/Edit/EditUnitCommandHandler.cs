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
using LearningUnit = Learnexia.Modules.Learning.Domain.Entities.Unit;

namespace Learnexia.Modules.Learning.Application.Features.Units.Commands.Edit;

public class EditUnitCommandHandler : BaseResponseHandler, ICommandHandler<EditUnitCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public EditUnitCommandHandler(
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

    public async Task<BaseResponse<string>> Handle(EditUnitCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            var result = await _service.UnitService.UpdateAsync(request);

            if (result.Successed)
            {
                // The service fetched the Unit with trackChanges=true — it is in the EF ChangeTracker.
                // Query with trackChanges=true — EF's identity map returns the same tracked instance.
                var tracked = await _repository.Learning
                    .GetByCondition<LearningUnit>(u => u.Id == request.Id, trackChanges: true)
                    .FirstOrDefaultAsync(cancellationToken);

                // Raise domain event on the tracked aggregate — dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
                tracked?.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                    AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                    Action: AdminActions.UnitUpdated,
                    TargetEntityType: nameof(LearningUnit),
                    TargetEntityId: request.Id,
                    Details: null));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in EditUnitCommand");
            return ServerError<string>();
        }
    }
}
