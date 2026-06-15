using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Features.Badges.Commands.AwardBadge;
using Learnexia.Modules.Gamification.Application.Features.Badges.Queries.GetMyBadges;
using Learnexia.Modules.Gamification.Application.Features.Events.Boost;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Exceptions;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;
using MediatR;

namespace Learnexia.Modules.Gamification.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IBadgeService"/>.
/// Owns badge catalog load, profile load/upsert, idempotency, XP boost, domain mutation,
/// StudentBadge and XpAward row staging via <see cref="IGamificationRepository"/>.
/// Transactions are owned by UnitOfWorkBehavior (commands); reads are non-transactional.
/// </summary>
internal sealed class BadgeService : BaseResponseHandler, IBadgeService
{
    private readonly IGamificationRepository _repo;
    private readonly ILoggerManager _logger;
    private readonly IXpBoostCalculator _boostCalc;

    public BadgeService(
        IGamificationRepository repo,
        ILoggerManager logger,
        IXpBoostCalculator boostCalc)
    {
        _repo = repo;
        _logger = logger;
        _boostCalc = boostCalc;
    }

    /// <inheritdoc />
    public async Task<BaseResponse<Unit>> AwardBadgeAsync(
        AwardBadgeCommand request,
        CancellationToken ct = default)
    {
        try
        {
            // ── 1. Load the BadgeDefinition catalog row (read-only) ──────────────────────────
            var definitions = await _repo.GetAllBadgeDefinitionsAsync(ct);
            var definition = definitions.FirstOrDefault(d => d.Id == request.BadgeDefinitionId);
            if (definition is null)
            {
                _logger.LogWarn(
                    $"P4-05: AwardBadgeCommand for unknown definitionId={request.BadgeDefinitionId}, " +
                    $"studentId={request.StudentId} — skipping.");
                return Success(Unit.Value);
            }

            // ── 2. Row-lock on profile to serialize concurrent badge awards ───────────────────
            await _repo.AcquireProfileLockAsync(request.StudentId, ct);

            // ── 3. Load or create profile ─────────────────────────────────────────────────────
            var profile = await _repo.GetProfileByStudentIdAsync(request.StudentId, ct)
                          ?? StudentXpProfile.CreateFor(request.StudentId);
            _repo.UpsertXpProfile(profile);

            // ── 4. Idempotency pre-check ──────────────────────────────────────────────────────
            if (profile.Id > 0)
            {
                bool already = await _repo.HasBadgeAsync(profile.Id, request.BadgeDefinitionId, ct);
                if (already)
                {
                    _logger.LogInfo(
                        $"P4-05: duplicate badge award {definition.Code} for student {request.StudentId} — skipping.");
                    return Success(Unit.Value);
                }
            }

            // ── 5. Apply timed-event XP boost to the badge reward, then recompute level ──────
            int effectiveRewardXp = await _boostCalc.GetEffectiveAmountAsync(
                definition.RewardXp,
                XpReason.BadgeEarned,
                request.AwardedAtUtc,
                ct);

            int newLevel = LevelCurve.LevelForXp(profile.TotalXp + effectiveRewardXp);

            // ── 6. Domain mutation ────────────────────────────────────────────────────────────
            profile.RecordBadgeEarned(
                badgeDefinitionId: definition.Id,
                code: definition.Code,
                rarity: definition.Rarity,
                rewardXp: effectiveRewardXp,
                newLevel: newLevel,
                awardedAtUtc: request.AwardedAtUtc);

            // ── 7. Persist the StudentBadge ledger row ────────────────────────────────────────
            _repo.AttachBadgeDefinition(definition);
            var ledger = StudentBadge.Create(
                profile: profile,
                definition: definition,
                originEventId: request.OriginEventId,
                originEventType: request.OriginEventType,
                awardedAtUtc: request.AwardedAtUtc);
            await _repo.AddStudentBadgeAsync(ledger, ct);

            // ── 8. Persist the XpAward row ────────────────────────────────────────────────────
            var xpAward = XpAward.Create(
                profile: profile,
                reason: XpReason.BadgeEarned,
                xpAmount: effectiveRewardXp,
                originEventId: Guid.NewGuid(),
                occurredAtUtc: request.AwardedAtUtc,
                lessonId: null,
                skillId: null);
            await _repo.AddXpAwardAsync(xpAward, ct);

            _logger.LogInfo(
                $"P4-05: badge awarded {definition.Code} (rarity={definition.Rarity}, +{effectiveRewardXp} XP) " +
                $"to studentId={request.StudentId}, eventId={request.OriginEventId}.");

            return Success(Unit.Value);
        }
        catch (GamificationUniqueConstraintException ucEx)
            when (ucEx.ConstraintHint.Contains("UX_StudentBadges_StudentXpProfileId_BadgeDefinitionId")
                  || ucEx.ConstraintHint.Contains("UX_XpAwards_OriginEventId_Reason")
                  || ucEx.ConstraintHint.Contains("23505"))
        {
            _logger.LogInfo(
                $"P4-05: unique-constraint violation on badge award for studentId={request.StudentId} " +
                $"(eventId={request.OriginEventId}, definitionId={request.BadgeDefinitionId}) — already applied.");
            return Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-05: Unexpected error in BadgeService.AwardBadgeAsync " +
                $"(studentId={request.StudentId}, definitionId={request.BadgeDefinitionId}, " +
                $"eventId={request.OriginEventId}).");
            return ServerError<Unit>();
        }
    }

    /// <inheritdoc />
    public async Task<BaseResponse<MyBadgesResponse>> GetMyBadgesAsync(
        int studentId,
        CancellationToken ct = default)
    {
        try
        {
            // ── 1. Load the full badge catalog (all active rows) ─────────────────────────────
            var definitions = await _repo.GetAllBadgeDefinitionsAsync(ct);

            // ── 2. Load all earned badges for this student ────────────────────────────────────
            var studentBadges = await _repo.GetStudentBadgesAsync(studentId, ct);

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
            _logger.LogError(ex, $"Error in BadgeService.GetMyBadgesAsync for student {studentId}");
            return ServerError<MyBadgesResponse>();
        }
    }

    /// <inheritdoc />
    public async Task EvaluateAndDispatchBadgesAsync(
        BadgeTriggerType triggerType,
        int value,
        int studentId,
        string originEventType,
        Guid originEventId,
        DateTime awardedAtUtc,
        IMediator mediator,
        CancellationToken ct = default)
    {
        try
        {
            // ── 1. Load active definitions for this trigger ──────────────────────────────────
            var definitions = await _repo.GetBadgeDefinitionsByTriggerAsync(triggerType, ct);
            if (definitions.Count == 0) return;

            // ── 2. Load earned badge IDs for idempotency pre-filter ──────────────────────────
            var earned = await _repo.GetEarnedBadgeIdsAsync(studentId, ct);

            // ── 3. Evaluate predicate — which definitions match? ──────────────────────────────
            var matches = BadgePredicateEvaluator
                .Match(triggerType, value, definitions, earned)
                .ToList();

            // ── 4. Dispatch one AwardBadgeCommand per match ───────────────────────────────────
            foreach (var def in matches)
            {
                try
                {
                    await mediator.Send(new AwardBadgeCommand(
                        StudentId: studentId,
                        BadgeDefinitionId: def.Id,
                        OriginEventId: Guid.NewGuid(),
                        OriginEventType: originEventType,
                        AwardedAtUtc: awardedAtUtc), ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        $"P4-05: Error awarding badge {def.Code} for {originEventType} " +
                        $"(studentId={studentId}, triggerType={triggerType}, value={value}, " +
                        $"originEventId={originEventId}).");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-05: Error in BadgeService.EvaluateAndDispatchBadgesAsync " +
                $"(studentId={studentId}, triggerType={triggerType}, value={value}, " +
                $"originEventType={originEventType}, originEventId={originEventId}).");
        }
    }
}
