using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.UpdateMissionDefinition;

/// <summary>
/// Updates mutable fields on an existing <see cref="MissionDefinition"/>. Code is immutable.
/// Raises <see cref="AdminActionPerformedDomainEvent"/> for post-commit audit.
/// </summary>
public sealed class UpdateMissionDefinitionCommandHandler
    : BaseResponseHandler, ICommandHandler<UpdateMissionDefinitionCommand, BaseResponse<bool>>
{
    private readonly IGamificationRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public UpdateMissionDefinitionCommandHandler(
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
        UpdateMissionDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var mission = await _repository.GetMissionDefinitionByIdAsync(request.Id, cancellationToken);
            if (mission is null)
                return NotFound<bool>(_localizer[SharedResourcesKey.GamificationMissionNotFound]);

            mission.AdminUpdate(
                request.IconKey,
                request.TitleKey,
                request.Cadence,
                request.TargetType,
                request.Target,
                request.RewardXp,
                request.SortOrder);

            mission.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.MissionUpdated,
                TargetEntityType: nameof(MissionDefinition),
                TargetEntityId: mission.Id,
                Details: $"Code={mission.Code}; Cadence={request.Cadence}"));

            return Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in UpdateMissionDefinitionCommand");
            return ServerError<bool>();
        }
    }
}
