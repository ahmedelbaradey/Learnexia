using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Gamification.Application.Features.Leagues.Queries.GetMyLeague;

/// <summary>
/// Handles <see cref="GetMyLeagueQuery"/> — returns the full cohort standings for the
/// JWT-resolved student (P4-07, AC3).
///
/// Resolves <c>StudentId</c> from <c>ICurrentUserService.UserId</c> — NEVER from a client-supplied
/// parameter (IDOR-proof by construction). Delegates read logic to <see cref="ILeagueService"/>.
/// Handler is EF-free and repository-free per §7 CONVENTIONS.
/// </summary>
public sealed class GetMyLeagueQueryHandler
    : BaseResponseHandler, IQueryHandler<GetMyLeagueQuery, BaseResponse<MyLeagueResponse>>
{
    private readonly ILeagueService _leagueService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetMyLeagueQueryHandler(
        ILeagueService leagueService,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _leagueService = leagueService;
        _currentUser   = currentUser;
        _logger        = logger;
        _localizer     = localizer;
    }

    public async Task<BaseResponse<MyLeagueResponse>> Handle(
        GetMyLeagueQuery request, CancellationToken ct)
    {
        // Belt-and-suspenders auth guard — [Authorize] on the controller already blocks anonymous.
        if (_currentUser.UserId is not { } studentId)
            return Unauthorized<MyLeagueResponse>(_localizer[SharedResourcesKey.Unauthorized]);

        return await _leagueService.GetMyLeagueAsync(studentId, ct);
    }
}
