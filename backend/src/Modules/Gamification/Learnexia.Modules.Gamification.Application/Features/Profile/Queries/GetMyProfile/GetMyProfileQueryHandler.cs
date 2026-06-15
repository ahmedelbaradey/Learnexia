using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Features.Profile.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Gamification.Application.Features.Profile.Queries.GetMyProfile;

/// <summary>
/// Handles <see cref="GetMyProfileQuery"/>.
///
/// Resolves <c>StudentId</c> from <c>ICurrentUserService.UserId</c> — NEVER from a client-supplied
/// parameter (IDOR-proof by construction). Delegates read logic to <see cref="IGamificationQueryService"/>.
/// Handler is EF-free and repository-free per §7 CONVENTIONS.
/// </summary>
public class GetMyProfileQueryHandler
    : BaseResponseHandler, IQueryHandler<GetMyProfileQuery, BaseResponse<StudentProfileDto>>
{
    private readonly IGamificationQueryService _queryService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetMyProfileQueryHandler(
        IGamificationQueryService queryService,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _queryService = queryService;
        _currentUser  = currentUser;
        _logger       = logger;
        _localizer    = localizer;
    }

    public async Task<BaseResponse<StudentProfileDto>> Handle(
        GetMyProfileQuery request,
        CancellationToken cancellationToken)
    {
        // Belt-and-suspenders auth guard — [Authorize] on the controller already blocks anonymous;
        // this null-check is defense-in-depth.
        if (_currentUser.UserId is not { } studentId)
            return Unauthorized<StudentProfileDto>(_localizer[SharedResourcesKey.Unauthorized]);

        return await _queryService.GetMyProfileAsync(studentId, cancellationToken);
    }
}
