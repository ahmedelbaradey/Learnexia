using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Features.Profile.Dtos;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IGamificationQueryService"/>.
/// READ-ONLY — no staging or commit. Resolves the XP profile for a student and
/// projects it into the <see cref="StudentProfileDto"/> DTO via <see cref="LevelCurve"/>.
/// </summary>
internal sealed class GamificationQueryService : BaseResponseHandler, IGamificationQueryService
{
    private readonly IGamificationRepository _repo;
    private readonly ILoggerManager _logger;

    public GamificationQueryService(
        IGamificationRepository repo,
        ILoggerManager logger)
    {
        _repo   = repo;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BaseResponse<StudentProfileDto>> GetMyProfileAsync(
        int studentId,
        CancellationToken ct = default)
    {
        try
        {
            var profile = await _repo.GetProfileByStudentIdAsync(studentId, ct);

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
            _logger.LogError(ex, $"Error in GamificationQueryService.GetMyProfileAsync for student {studentId}");
            return ServerError<StudentProfileDto>();
        }
    }

    /// <inheritdoc />
    public async Task<StudentXpProfile?> GetProfileEntityForCacheAsync(
        int studentId,
        CancellationToken ct = default)
    {
        try
        {
            return await _repo.GetProfileByStudentIdAsync(studentId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error in GamificationQueryService.GetProfileEntityForCacheAsync for student {studentId}");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<LeagueMembership?> GetMembershipForCacheAsync(
        int profileId,
        string weeklyKey,
        CancellationToken ct = default)
    {
        try
        {
            return await _repo.GetMembershipForStudentAsync(profileId, weeklyKey, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error in GamificationQueryService.GetMembershipForCacheAsync " +
                $"(profileId={profileId}, weeklyKey={weeklyKey})");
            return null;
        }
    }
}
