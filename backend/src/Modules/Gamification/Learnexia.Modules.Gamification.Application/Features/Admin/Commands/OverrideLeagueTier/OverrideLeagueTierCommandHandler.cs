using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.OverrideLeagueTier;

/// <summary>
/// Sets <see cref="StudentXpProfile.CurrentTier"/> to <see cref="OverrideLeagueTierCommand.NewTier"/>.
/// The override takes effect at the next league rollover; live standings are intentionally not forced
/// to avoid corrupting the cohort's in-progress rollover data (F5 security fix).
///
/// Raises <see cref="AdminActionPerformedDomainEvent"/> on the <see cref="StudentXpProfile"/>
/// aggregate for post-commit audit relay.
/// </summary>
public sealed class OverrideLeagueTierCommandHandler
    : BaseResponseHandler, ICommandHandler<OverrideLeagueTierCommand, BaseResponse<bool>>
{
    private readonly IGamificationRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public OverrideLeagueTierCommandHandler(
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
        OverrideLeagueTierCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!request.Confirm)
                return BadRequest<bool>(_localizer[SharedResourcesKey.GamificationConfirmRequired]);

            var profile = await _repository.GetProfileByStudentIdTrackedAsync(
                request.ChildId, cancellationToken);

            if (profile is null)
                return NotFound<bool>(_localizer[SharedResourcesKey.GamificationProfileNotFound]);

            if (profile.CurrentTier == request.NewTier)
                return BadRequest<bool>(_localizer[SharedResourcesKey.GamificationLeagueTierNoOp]);

            var oldTier = profile.CurrentTier;
            profile.UpdateTier(request.NewTier);

            // F5: FinalizePromotion belongs to LeagueRolloverJob only — not called here to avoid
            // corrupting in-progress rollover state. The tier change takes effect at next rollover.

            var adminUserId = _currentUser.UserId.GetValueOrDefault();

            profile.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: adminUserId,
                Action: AdminActions.GamificationLeagueTierOverridden,
                TargetEntityType: nameof(StudentXpProfile),
                TargetEntityId: profile.Id,
                Details: $"ChildId={request.ChildId}; Tier: {oldTier}→{request.NewTier}"));

            return Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in OverrideLeagueTierCommand");
            return ServerError<bool>();
        }
    }
}
