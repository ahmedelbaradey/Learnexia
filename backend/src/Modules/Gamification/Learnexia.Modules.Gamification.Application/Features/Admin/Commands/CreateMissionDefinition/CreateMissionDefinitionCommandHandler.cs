using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.CreateMissionDefinition;

/// <summary>
/// Creates a new <see cref="MissionDefinition"/> catalog row. Validates Code uniqueness.
/// Raises <see cref="AdminActionPerformedDomainEvent"/> on the new aggregate for post-commit audit.
/// </summary>
public sealed class CreateMissionDefinitionCommandHandler
    : BaseResponseHandler, ICommandHandler<CreateMissionDefinitionCommand, BaseResponse<int>>
{
    private readonly IGamificationRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public CreateMissionDefinitionCommandHandler(
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

    public async Task<BaseResponse<int>> Handle(
        CreateMissionDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (await _repository.MissionCodeExistsAsync(request.Code, cancellationToken))
                return BusinessValidation<int>(_localizer[SharedResourcesKey.GamificationMissionCodeAlreadyExists]);

            var mission = MissionDefinition.Create(
                request.Code,
                request.IconKey,
                request.TitleKey,
                request.Cadence,
                request.TargetType,
                request.Target,
                request.RewardXp,
                request.SortOrder);

            await _repository.AddMissionDefinitionAsync(mission, cancellationToken);

            mission.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.MissionCreated,
                TargetEntityType: nameof(MissionDefinition),
                TargetEntityId: mission.Id,
                Details: $"Code={request.Code}; Cadence={request.Cadence}"));

            return Created(mission.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in CreateMissionDefinitionCommand");
            return ServerError<int>();
        }
    }
}
