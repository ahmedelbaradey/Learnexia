using Learnexia.Modules.Gamification.Application.Features.Profile.Dtos;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Abstractions;

/// <summary>
/// Read-only query service for the Gamification module profile endpoint.
/// Returns the XP/level summary for the JWT-resolved student.
/// No staging or commit — READ-ONLY.
/// </summary>
public interface IGamificationQueryService
{
    /// <summary>
    /// Returns the XP profile summary (TotalXp, CurrentLevel, XpIntoCurrentLevel, XpToNextLevel)
    /// for the given student. Returns a zero-state DTO for brand-new students with no profile row.
    /// </summary>
    Task<BaseResponse<StudentProfileDto>> GetMyProfileAsync(
        int studentId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the raw <see cref="StudentXpProfile"/> entity for the given student, or null if
    /// no profile row exists yet. Used by post-commit notification handlers (cache invalidators)
    /// that must remain repository-free per §7 CONVENTIONS.
    /// READ-ONLY — no staging or commit.
    /// </summary>
    Task<StudentXpProfile?> GetProfileEntityForCacheAsync(
        int studentId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the <see cref="LeagueMembership"/> for the given profile + period key, or null
    /// if no membership exists. Used by post-commit cache invalidators that must remain
    /// repository-free per §7 CONVENTIONS.
    /// READ-ONLY — no staging or commit.
    /// </summary>
    Task<LeagueMembership?> GetMembershipForCacheAsync(
        int profileId,
        string weeklyKey,
        CancellationToken ct = default);
}
