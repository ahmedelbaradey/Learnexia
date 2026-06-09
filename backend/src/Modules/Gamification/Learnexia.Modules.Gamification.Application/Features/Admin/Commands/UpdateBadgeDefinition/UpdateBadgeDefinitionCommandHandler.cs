using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.UpdateBadgeDefinition;

/// <summary>
/// Updates mutable fields on an existing <see cref="BadgeDefinition"/>. Code is immutable.
/// Raises <see cref="AdminActionPerformedDomainEvent"/> for post-commit audit.
/// </summary>
public sealed class UpdateBadgeDefinitionCommandHandler
    : BaseResponseHandler, ICommandHandler<UpdateBadgeDefinitionCommand, BaseResponse<bool>>
{
    private readonly IGamificationRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public UpdateBadgeDefinitionCommandHandler(
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
        UpdateBadgeDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var badge = await _repository.GetBadgeDefinitionByIdAsync(request.Id, cancellationToken);
            if (badge is null)
                return NotFound<bool>(_localizer[SharedResourcesKey.GamificationBadgeNotFound]);

            badge.AdminUpdate(
                request.Name,
                request.Description,
                request.IconKey,
                request.Rarity,
                request.SortOrder,
                request.TriggerType,
                request.Threshold,
                request.RewardXp);

            badge.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.BadgeUpdated,
                TargetEntityType: nameof(BadgeDefinition),
                TargetEntityId: badge.Id,
                Details: $"Code={badge.Code}; Name={request.Name}"));

            return Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in UpdateBadgeDefinitionCommand");
            return ServerError<bool>();
        }
    }
}
