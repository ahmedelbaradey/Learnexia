using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.SetMissionDefinitionActive;

/// <summary>
/// Activates or deactivates a <see cref="MissionDefinition"/>.
/// Raises <see cref="AdminActionPerformedDomainEvent"/> for post-commit audit.
/// </summary>
public sealed class SetMissionDefinitionActiveCommandHandler
    : BaseResponseHandler, ICommandHandler<SetMissionDefinitionActiveCommand, BaseResponse<bool>>
{
    private readonly IGamificationRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public SetMissionDefinitionActiveCommandHandler(
        IGamificationRepository repository,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
        _currentUser = currentUser;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<bool>> Handle(
        SetMissionDefinitionActiveCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var mission = await _repository.GetMissionDefinitionByIdAsync(request.Id, cancellationToken);
            if (mission is null)
                return NotFound<bool>(_localizer[SharedResourcesKey.GamificationMissionNotFound]);

            // F6: no-op guard — if already in the requested state, reject without mutating or
            // raising a domain event (suppresses spurious audit rows).
            if (mission.IsActive == request.IsActive)
                return BadRequest<bool>(_localizer[SharedResourcesKey.GamificationMissionAlreadyInRequestedState]);

            mission.SetActive(request.IsActive);

            var action = request.IsActive ? AdminActions.MissionActivated : AdminActions.MissionDeactivated;

            mission.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: action,
                TargetEntityType: nameof(MissionDefinition),
                TargetEntityId: mission.Id,
                Details: $"Code={mission.Code}; IsActive={request.IsActive}"));

            return Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in SetMissionDefinitionActiveCommand");
            return ServerError<bool>();
        }
    }
}
