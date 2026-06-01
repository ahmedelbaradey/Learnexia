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
/// parameter (IDOR-proof by construction). Mirrors <c>GetMyProfileQueryHandler</c>.
///
/// Algorithm:
///   1. Load all BadgeDefinition catalog rows (10 rows — cheap, AsNoTracking).
///   2. Load all StudentBadge rows for this student (via StudentXpProfile join — AsNoTracking).
///   3. Build an earned-by-BadgeDefinitionId lookup for O(1) join in-memory.
///   4. Project each definition into <see cref="BadgeStateDto"/> with IsEarned + AwardedAtUtc.
///   5. Sort by Rarity ASC, SortOrder ASC — stable order for FE rendering.
///
/// This is a READ-ONLY query (no SaveChangesAsync, ValidationBehavior does NOT apply).
/// </summary>
public sealed class GetMyBadgesQueryHandler
    : BaseResponseHandler, IQueryHandler<GetMyBadgesQuery, BaseResponse<MyBadgesResponse>>
{
    private readonly IGamificationRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetMyBadgesQueryHandler(
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

    public async Task<BaseResponse<MyBadgesResponse>> Handle(
        GetMyBadgesQuery request, CancellationToken cancellationToken)
    {
        // Belt-and-suspenders auth guard — [Authorize] on the controller already blocks anonymous;
        // this null-check is defense-in-depth (mirrors GetMyProfileQueryHandler).
        if (_currentUser.UserId is not { } studentId)
            return Unauthorized<MyBadgesResponse>(_localizer[SharedResourcesKey.Unauthorized]);

        try
        {
            // ── 1. Load the full badge catalog (all 10 rows) ─────────────────────────────────
            var definitions = await _repo.GetAllBadgeDefinitionsAsync(cancellationToken);

            // ── 2. Load all earned badges for this student ────────────────────────────────────
            var studentBadges = await _repo.GetStudentBadgesAsync(studentId, cancellationToken);

            // ── 3. Build earned-by-BadgeDefinitionId lookup for O(1) join ────────────────────
            var earnedById = studentBadges.ToDictionary(sb => sb.BadgeDefinitionId);

            // ── 4 + 5. Project + sort ─────────────────────────────────────────────────────────
            var badges = definitions
                .OrderBy(d => d.Rarity)
                .ThenBy(d => d.SortOrder)
                .Select(d => new BadgeStateDto(
                    Code: d.Code,
                    IconKey: d.IconKey,
                    Rarity: d.Rarity,
                    TriggerType: d.TriggerType,
                    Threshold: d.Threshold,
                    RewardXp: d.RewardXp,
                    IsEarned: earnedById.ContainsKey(d.Id),
                    AwardedAtUtc: earnedById.TryGetValue(d.Id, out var earned)
                        ? earned.AwardedAtUtc
                        : null))
                .ToList();

            return Success(new MyBadgesResponse(badges));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in GetMyBadgesQuery for student {studentId}");
            return ServerError<MyBadgesResponse>();   // do NOT echo ex.Message to the client
        }
    }
}
