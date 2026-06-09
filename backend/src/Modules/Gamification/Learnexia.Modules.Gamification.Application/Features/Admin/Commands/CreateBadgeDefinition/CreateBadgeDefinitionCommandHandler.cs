using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.CreateBadgeDefinition;

/// <summary>
/// Creates a new <see cref="BadgeDefinition"/> catalog row. Validates Code uniqueness.
/// Raises <see cref="AdminActionPerformedDomainEvent"/> on the new aggregate for post-commit audit.
/// </summary>
public sealed class CreateBadgeDefinitionCommandHandler
    : BaseResponseHandler, ICommandHandler<CreateBadgeDefinitionCommand, BaseResponse<int>>
{
    private readonly IGamificationRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public CreateBadgeDefinitionCommandHandler(
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
        CreateBadgeDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (await _repository.BadgeCodeExistsAsync(request.Code, cancellationToken))
                return BusinessValidation<int>(_localizer[SharedResourcesKey.GamificationBadgeCodeAlreadyExists]);

            var badge = BadgeDefinition.Create(
                request.Code,
                request.Name,
                request.Description,
                request.IconKey,
                request.Rarity,
                request.SortOrder,
                request.TriggerType,
                request.Threshold,
                request.RewardXp);

            await _repository.AddBadgeDefinitionAsync(badge, cancellationToken);

            var adminUserId = _currentUser.UserId.GetValueOrDefault();

            badge.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: adminUserId,
                Action: AdminActions.BadgeCreated,
                TargetEntityType: nameof(BadgeDefinition),
                TargetEntityId: badge.Id,
                Details: $"Code={request.Code}; Name={request.Name}"));

            return Created(badge.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in CreateBadgeDefinitionCommand");
            return ServerError<int>();
        }
    }
}
