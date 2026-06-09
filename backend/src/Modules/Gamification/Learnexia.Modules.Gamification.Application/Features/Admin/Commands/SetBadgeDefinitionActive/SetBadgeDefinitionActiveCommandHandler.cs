using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.SetBadgeDefinitionActive;

/// <summary>
/// Activates or deactivates a <see cref="BadgeDefinition"/>. Deactivation does NOT strip
/// already-earned <c>StudentBadge</c> rows (AC2). Raises <see cref="AdminActionPerformedDomainEvent"/>.
/// </summary>
public sealed class SetBadgeDefinitionActiveCommandHandler
    : BaseResponseHandler, ICommandHandler<SetBadgeDefinitionActiveCommand, BaseResponse<bool>>
{
    private readonly IGamificationRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public SetBadgeDefinitionActiveCommandHandler(
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
        SetBadgeDefinitionActiveCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var badge = await _repository.GetBadgeDefinitionByIdAsync(request.Id, cancellationToken);
            if (badge is null)
                return NotFound<bool>(_localizer[SharedResourcesKey.GamificationBadgeNotFound]);

            // F6: no-op guard — if already in the requested state, reject without mutating or
            // raising a domain event (suppresses spurious audit rows).
            if (badge.IsActive == request.IsActive)
                return BadRequest<bool>(_localizer[SharedResourcesKey.GamificationBadgeAlreadyInRequestedState]);

            badge.SetActive(request.IsActive);

            var action = request.IsActive ? AdminActions.BadgeActivated : AdminActions.BadgeDeactivated;

            badge.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: action,
                TargetEntityType: nameof(BadgeDefinition),
                TargetEntityId: badge.Id,
                Details: $"Code={badge.Code}; IsActive={request.IsActive}"));

            return Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in SetBadgeDefinitionActiveCommand");
            return ServerError<bool>();
        }
    }
}
