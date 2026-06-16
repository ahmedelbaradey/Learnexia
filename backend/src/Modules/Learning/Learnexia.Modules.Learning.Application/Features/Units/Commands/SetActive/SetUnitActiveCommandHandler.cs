using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;
using LearningUnit = Learnexia.Modules.Learning.Domain.Entities.Unit;

namespace Learnexia.Modules.Learning.Application.Features.Units.Commands.SetActive;

/// <summary>
/// Toggles <c>Unit.IsActive</c>. Inactive units are hidden from student-facing reads.
/// Publishes <see cref="AdminActionPerformedDomainEvent"/> post-commit, best-effort.
///
/// Option C: all EF queries delegated to IUnitService. Handler injects only ILearningServiceManager.
/// </summary>
public class SetUnitActiveCommandHandler : BaseResponseHandler, ICommandHandler<SetUnitActiveCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public SetUnitActiveCommandHandler(
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

    public async Task<BaseResponse<string>> Handle(SetUnitActiveCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            var unit = await _service.UnitService.GetUnitTrackedAsync(request.UnitId, cancellationToken);
            if (unit is null)
                return NotFound<string>(_localizer[SharedResourcesKey.UnitNotFound]);

            unit.IsActive = request.IsActive;
            await _service.UnitService.StageUnitUpdateAsync(unit, cancellationToken);

            var action = request.IsActive ? AdminActions.UnitActivated : AdminActions.UnitDeactivated;

            // Raise domain event on the tracked aggregate — dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
            unit.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: action,
                TargetEntityType: nameof(LearningUnit),
                TargetEntityId: request.UnitId,
                Details: $"IsActive={request.IsActive}"));

            var message = request.IsActive
                ? _localizer[SharedResourcesKey.UnitActivatedSuccessfully]
                : _localizer[SharedResourcesKey.UnitDeactivatedSuccessfully];

            return Success<string>(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in SetUnitActiveCommand");
            return ServerError<string>();
        }
    }
}
