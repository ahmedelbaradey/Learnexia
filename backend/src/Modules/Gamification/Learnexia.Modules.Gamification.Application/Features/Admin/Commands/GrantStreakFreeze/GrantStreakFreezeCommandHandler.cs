using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.GrantStreakFreeze;

/// <summary>
/// Grants up to <see cref="GrantStreakFreezeCommand.Count"/> streak freezes, clamped at
/// <see cref="StudentXpProfile.MaxFreezes"/>. Uses the bounded <c>GrantFreezes</c> overload
/// added in P7-13.
///
/// Raises <see cref="AdminActionPerformedDomainEvent"/> on <see cref="StudentXpProfile"/> for
/// post-commit audit relay.
/// </summary>
public sealed class GrantStreakFreezeCommandHandler
    : BaseResponseHandler, ICommandHandler<GrantStreakFreezeCommand, BaseResponse<bool>>
{
    private readonly IGamificationRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GrantStreakFreezeCommandHandler(
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
        GrantStreakFreezeCommand request,
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

            int balanceBefore = profile.FreezeBalance;
            int granted = profile.GrantFreezes(request.Count, DateTime.UtcNow);

            if (granted == 0)
                return BadRequest<bool>(_localizer[SharedResourcesKey.GamificationFreezeBalanceAtMax]);

            var adminUserId = _currentUser.UserId.GetValueOrDefault();

            profile.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: adminUserId,
                Action: AdminActions.GamificationStreakFreezeGranted,
                TargetEntityType: nameof(StudentXpProfile),
                TargetEntityId: profile.Id,
                Details: $"ChildId={request.ChildId}; FreezeBalance: {balanceBefore}→{profile.FreezeBalance}; count={granted}"));

            return Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GrantStreakFreezeCommand");
            return ServerError<bool>();
        }
    }
}
