using Learnexia.Modules.Gamification.Application.Features.Profile.Dtos;
using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Resources;
using Microsoft.Extensions.Localization;

namespace Learnexia.Modules.Gamification.Application.Features.Profile.Queries.GetMyProfile;

/// <summary>
/// Handles <see cref="GetMyProfileQuery"/>.
///
/// Resolves <c>StudentId</c> from <c>ICurrentUserService.UserId</c> — NEVER from a client-supplied
/// parameter (IDOR-proof by construction). If no profile exists yet (brand-new student), returns a
/// zero-state <c>{ TotalXp=0, CurrentLevel=1, XpIntoCurrentLevel=0, XpToNextLevel=100 }</c> with
/// 200 OK. The <c>LevelCurve</c> is used to compute level progress fields.
///
/// This is a READ-ONLY query (no SaveChangesAsync, no ICommand, ValidationBehavior does not apply).
/// </summary>
public class GetMyProfileQueryHandler
    : BaseResponseHandler, IQueryHandler<GetMyProfileQuery, BaseResponse<StudentProfileDto>>
{
    private readonly IGamificationRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetMyProfileQueryHandler(
        IGamificationRepository repo,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repo = repo;
        _currentUser = currentUser;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<StudentProfileDto>> Handle(
        GetMyProfileQuery request,
        CancellationToken cancellationToken)
    {
        // Belt-and-suspenders auth guard — [Authorize] on the controller already blocks anonymous;
        // this null-check is defense-in-depth (mirrors GetDashboardQueryHandler).
        if (_currentUser.UserId is not { } studentId)
            return Unauthorized<StudentProfileDto>(_localizer[SharedResourcesKey.Unauthorized]);

        try
        {
            var profile = await _repo.GetProfileByStudentIdAsync(studentId, cancellationToken);

            int totalXp = profile?.TotalXp ?? 0;
            var (level, xpIntoLevel, xpToNext) = LevelCurve.Compute(totalXp);

            var dto = new StudentProfileDto(
                TotalXp: totalXp,
                CurrentLevel: level,
                XpIntoCurrentLevel: xpIntoLevel,
                XpToNextLevel: xpToNext);

            return Success(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in GetMyProfileQuery for student {studentId}");
            return ServerError<StudentProfileDto>();   // do NOT echo ex.Message to the client
        }
    }
}
