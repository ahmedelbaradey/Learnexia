using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Contracts.Gamification;
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
/// parameter (IDOR-proof by construction). Mirrors <c>GetMyBadgesQueryHandler</c>.
///
/// Algorithm:
///   1. Call <see cref="IStudentMissionsQuery.GetByStudentIdAsync"/> — triggers lazy instantiation
///      of the current period's missions if no rows exist yet (D2 / AC4).
///   2. Load the current-period missions with full metadata via
///      <see cref="IGamificationRepository.GetActiveMissionsForStudentAsync"/> (includes MissionDefinition).
///   3. Split into Daily / Weekly, project to <see cref="MissionStateDto"/>, return.
///
/// Two-step rationale: <c>IStudentMissionsQuery</c> handles instantiation but only returns
/// a lightweight <see cref="MissionSummary"/>. This endpoint needs period boundaries and RewardXp
/// (full DTO) which are available on the <see cref="Domain.Entities.StudentMission"/> entity
/// returned by the repo query.
///
/// This is a READ-ONLY query (no SaveChangesAsync, ValidationBehavior does NOT apply).
/// </summary>
public sealed class GetMyMissionsQueryHandler
    : BaseResponseHandler, IQueryHandler<GetMyMissionsQuery, BaseResponse<MyMissionsResponse>>
{
    private readonly IGamificationRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IStudentMissionsQuery _missionsQuery;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetMyMissionsQueryHandler(
        IGamificationRepository repo,
        ICurrentUserService currentUser,
        IStudentMissionsQuery missionsQuery,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repo          = repo;
        _currentUser   = currentUser;
        _missionsQuery = missionsQuery;
        _logger        = logger;
        _localizer     = localizer;
    }

    public async Task<BaseResponse<MyMissionsResponse>> Handle(
        GetMyMissionsQuery request, CancellationToken ct)
    {
        // Belt-and-suspenders auth guard — [Authorize] on the controller already blocks anonymous;
        // this null-check is defense-in-depth (mirrors GetMyBadgesQueryHandler).
        if (_currentUser.UserId is not { } studentId)
            return Unauthorized<MyMissionsResponse>(_localizer[SharedResourcesKey.Unauthorized]);

        try
        {
            // ── 1. Trigger lazy instantiation via the cross-module seam ──────────────────────
            // This call ensures the current period's missions exist in the DB before we query them.
            // The snapshot itself is not used here — we load richer DTOs from the repo in step 2.
            _ = await _missionsQuery.GetByStudentIdAsync(studentId, ct);

            // ── 2. Load current-period missions with full metadata ────────────────────────────
            var activeMissions = await _repo.GetActiveMissionsForStudentAsync(studentId, ct);

            // ── 3. Split + project ────────────────────────────────────────────────────────────
            var daily = activeMissions
                .Where(m => m.MissionDefinition.Cadence == MissionType.Daily)
                .OrderBy(m => m.MissionDefinition.SortOrder)
                .ThenBy(m => m.MissionDefinition.Id)
                .Select(ToDto)
                .ToList();

            var weekly = activeMissions
                .Where(m => m.MissionDefinition.Cadence == MissionType.Weekly)
                .OrderBy(m => m.MissionDefinition.SortOrder)
                .ThenBy(m => m.MissionDefinition.Id)
                .Select(ToDto)
                .ToList();

            return Success(new MyMissionsResponse(daily, weekly));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"P4-06: Error in GetMyMissionsQuery for studentId={studentId}");
            return ServerError<MyMissionsResponse>();   // do NOT echo ex.Message to the client
        }
    }

    // Cast Domain enums to mirrored DTO enums so Domain types are not exposed on the API surface (F3).
    // Integer values are identical — JSON serialization is unchanged.
    // Mirrors how StudentMissionsQuery.ToSummary handles MissionTargetTypeDto + MissionStatusDto casts.
    private static MissionStateDto ToDto(Domain.Entities.StudentMission m) => new(
        Code:           m.MissionDefinition.Code,
        IconKey:        m.MissionDefinition.IconKey,
        TitleKey:       m.MissionDefinition.TitleKey,
        Cadence:        (MissionTypeDto)(int)m.MissionDefinition.Cadence,
        TargetType:     (MissionTargetTypeDto)(int)m.MissionDefinition.TargetType,
        Target:         m.Target,
        RewardXp:       m.MissionDefinition.RewardXp,
        Progress:       m.Progress,
        Status:         (MissionStatusDto)(int)m.Status,
        PeriodStartUtc: m.PeriodStartUtc,
        PeriodEndUtc:   m.PeriodEndUtc,
        CompletedAtUtc: m.CompletedAtUtc);
}
