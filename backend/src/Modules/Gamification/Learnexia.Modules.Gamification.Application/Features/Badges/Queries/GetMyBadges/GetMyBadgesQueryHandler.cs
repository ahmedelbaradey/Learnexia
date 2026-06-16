using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Gamification.Application.Features.Badges.Queries.GetMyBadges;

/// <summary>
/// Handles <see cref="GetMyBadgesQuery"/> — returns the full badge catalog with earned/locked state
/// for the JWT-resolved student (P4-05, AC4 / FR-GM-4).
///
/// Resolves <c>StudentId</c> from <c>ICurrentUserService.UserId</c> — NEVER from a client-supplied
/// parameter (IDOR-proof by construction). Delegates read logic to <see cref="IBadgeService"/>.
/// Handler is EF-free and repository-free per §7 CONVENTIONS.
/// </summary>
public sealed class GetMyBadgesQueryHandler
    : BaseResponseHandler, IQueryHandler<GetMyBadgesQuery, BaseResponse<MyBadgesResponse>>
{
    private readonly IBadgeService _badgeService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetMyBadgesQueryHandler(
        IBadgeService badgeService,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _badgeService = badgeService;
        _currentUser  = currentUser;
        _logger       = logger;
        _localizer    = localizer;
    }

    public async Task<BaseResponse<MyBadgesResponse>> Handle(
        GetMyBadgesQuery request, CancellationToken cancellationToken)
    {
        // Belt-and-suspenders auth guard — [Authorize] on the controller already blocks anonymous.
        if (_currentUser.UserId is not { } studentId)
            return Unauthorized<MyBadgesResponse>(_localizer[SharedResourcesKey.Unauthorized]);

        return await _badgeService.GetMyBadgesAsync(studentId, cancellationToken);
    }
}
