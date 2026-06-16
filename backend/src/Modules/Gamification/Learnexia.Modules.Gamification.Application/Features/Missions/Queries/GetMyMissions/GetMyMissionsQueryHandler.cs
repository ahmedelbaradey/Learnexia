using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Gamification.Application.Features.Missions.Queries.GetMyMissions;

/// <summary>
/// Handles <see cref="GetMyMissionsQuery"/> — returns the full daily + weekly missions list
/// with period metadata for the JWT-resolved student (P4-06, AC2 + AC3).
///
/// Resolves <c>StudentId</c> from <c>ICurrentUserService.UserId</c> — NEVER from a client-supplied
/// parameter (IDOR-proof by construction). Delegates read logic to <see cref="IMissionService"/>.
/// Handler is EF-free and repository-free per §7 CONVENTIONS.
/// </summary>
public sealed class GetMyMissionsQueryHandler
    : BaseResponseHandler, IQueryHandler<GetMyMissionsQuery, BaseResponse<MyMissionsResponse>>
{
    private readonly IMissionService _missionService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetMyMissionsQueryHandler(
        IMissionService missionService,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _missionService = missionService;
        _currentUser    = currentUser;
        _logger         = logger;
        _localizer      = localizer;
    }

    public async Task<BaseResponse<MyMissionsResponse>> Handle(
        GetMyMissionsQuery request, CancellationToken ct)
    {
        // Belt-and-suspenders auth guard — [Authorize] on the controller already blocks anonymous.
        if (_currentUser.UserId is not { } studentId)
            return Unauthorized<MyMissionsResponse>(_localizer[SharedResourcesKey.Unauthorized]);

        return await _missionService.GetMyMissionsAsync(studentId, ct);
    }
}
