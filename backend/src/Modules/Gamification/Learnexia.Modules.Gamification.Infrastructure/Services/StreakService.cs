using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Common;
using Learnexia.Modules.Gamification.Application.Configuration;
using Learnexia.Modules.Gamification.Application.Features.Events.Boost;
using Learnexia.Modules.Gamification.Application.Features.Streaks.Commands.AdvanceStreak;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Exceptions;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IStreakService"/>.
/// Owns idempotency, practice-mode gate, day classification, freeze milestone, StreakBonus XP staging
/// via <see cref="IGamificationRepository"/>. Transactions are owned by UnitOfWorkBehavior.
/// </summary>
internal sealed class StreakService : BaseResponseHandler, IStreakService
{
    private readonly IGamificationRepository _repo;
    private readonly ISystemClock _clock;
    private readonly IOptions<StreakOptions> _streakOptions;
    private readonly IOptions<HeartsOptions> _heartsOptions;
    private readonly FreezeOptions _freezeOptions;
    private readonly ILoggerManager _logger;
    private readonly IXpBoostCalculator _boostCalc;

    public StreakService(
        IGamificationRepository repo,
        ISystemClock clock,
        IOptions<StreakOptions> streakOptions,
        IOptions<HeartsOptions> heartsOptions,
        IOptions<FreezeOptions> freezeOptions,
        ILoggerManager logger,
        IXpBoostCalculator boostCalc)
    {
        _repo = repo;
        _clock = clock;
        _streakOptions = streakOptions;
        _heartsOptions = heartsOptions;
        _freezeOptions = freezeOptions.Value;
        _logger = logger;
        _boostCalc = boostCalc;
    }

    /// <inheritdoc />
    public async Task<BaseResponse<Unit>> AdvanceStreakAsync(
        AdvanceStreakCommand request,
        CancellationToken ct = default)
    {
        try
        {
            // ── 1. Idempotency pre-check ──────────────────────────────────────────────────────
            bool alreadyAwarded = await _repo.HasXpAwardAsync(
                request.OriginEventId, XpReason.StreakBonus, ct);

            if (alreadyAwarded)
            {
                _logger.LogInfo(
                    $"P4-03: duplicate StreakBonus event {request.OriginEventId} " +
                    $"for student {request.StudentId} — award already exists, skipping.");
                return Success(Unit.Value);
            }

            // ── 2. Row-lock ───────────────────────────────────────────────────────────────────
            await _repo.AcquireProfileLockAsync(request.StudentId, ct);

            // ── 3. Compute day-bucket ─────────────────────────────────────────────────────────
            var activityDate = StreakDayCalculator.DayOf(
                request.OccurredAtUtc, _streakOptions.Value.TimeZoneId);

            // ── 4. Load or create profile ─────────────────────────────────────────────────────
            var profile = await _repo.GetProfileByStudentIdAsync(request.StudentId, ct)
                          ?? StudentXpProfile.CreateFor(request.StudentId);

            _repo.UpsertXpProfile(profile);

            // ── 4b. P4-04 Practice-Mode gate ─────────────────────────────────────────────────
            // Lazy-refill first so a student whose hearts have ticked back up is not falsely gated.
            var heartsOpts = _heartsOptions.Value;
            profile.RefreshHeartsAgainst(_clock.UtcNow, heartsOpts.Cap, heartsOpts.RefillIntervalMinutes);

            if (profile.InPracticeMode)
            {
                _logger.LogInfo(
                    $"P4-04: Practice Mode — AdvanceStreak suspended (studentId={request.StudentId}).");
                return Success(Unit.Value);
            }

            // ── 5. Classify and branch ────────────────────────────────────────────────────────
            var transition = StreakDayCalculator.Classify(profile.LastActivityDateUtc, activityDate);

            switch (transition)
            {
                case StreakDayCalculator.Transition.NoOp:
                    // Same calendar day — only first lesson of the day pays the bonus.
                    _logger.LogDebug(
                        $"P4-03: same-day activity — streak unchanged " +
                        $"(studentId={request.StudentId}, eventId={request.OriginEventId}).");
                    return Success(Unit.Value);

                case StreakDayCalculator.Transition.OutOfOrder:
                    // Stale / out-of-order event (e.g. delayed re-delivery from days ago).
                    _logger.LogWarn(
                        $"P4-03: out-of-order activity event ignored " +
                        $"(studentId={request.StudentId}, eventId={request.OriginEventId}).");
                    return Success(Unit.Value);

                case StreakDayCalculator.Transition.FirstActivity:
                case StreakDayCalculator.Transition.Advance:
                    profile.AdvanceStreak(activityDate);
                    break;

                case StreakDayCalculator.Transition.Reset:
                    profile.ResetStreakAndStart(activityDate);
                    break;
            }

            // ── 5b. P4-11: Grant a freeze on every N-day streak milestone ──────────────────
            if (profile.CurrentStreak > 0 && profile.CurrentStreak % _freezeOptions.EarnEveryNStreakDays == 0)
            {
                profile.GrantFreeze(request.OccurredAtUtc);
            }

            // ── 6. Award StreakBonus XP ───────────────────────────────────────────────────────
            int bonusAmount = await _boostCalc.GetEffectiveAmountAsync(
                GamificationConstants.XpRewards.StreakBonus,
                XpReason.StreakBonus,
                request.OccurredAtUtc,
                ct);

            var award = XpAward.Create(
                profile: profile,
                reason: XpReason.StreakBonus,
                xpAmount: bonusAmount,
                originEventId: request.OriginEventId,
                occurredAtUtc: request.OccurredAtUtc,
                lessonId: request.LessonId,
                skillId: request.SkillId);

            await _repo.AddXpAwardAsync(award, ct);

            // ── 7. Recompute level ────────────────────────────────────────────────────────────
            int newLevel = LevelCurve.LevelForXp(profile.TotalXp + bonusAmount);
            profile.ApplyAward(bonusAmount, newLevel, XpReason.StreakBonus, request.OccurredAtUtc);

            _logger.LogInfo(
                $"P4-03: streak advanced " +
                $"(studentId={request.StudentId}, currentStreak={profile.CurrentStreak}, " +
                $"longestStreak={profile.LongestStreak}) — +{bonusAmount} StreakBonus XP awarded.");

            return Success(Unit.Value);
        }
        catch (GamificationUniqueConstraintException ucEx)
            when (ucEx.ConstraintHint.Contains("UX_XpAwards_OriginEventId_Reason")
                  || ucEx.ConstraintHint.Contains("23505"))
        {
            _logger.LogInfo(
                $"P4-03: unique-constraint violation on XpAwards — concurrent duplicate delivery " +
                $"for student {request.StudentId} (eventId={request.OriginEventId}), treated as already-applied.");
            return Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-03: Unexpected error in StreakService.AdvanceStreakAsync " +
                $"(studentId={request.StudentId}, eventId={request.OriginEventId}).");
            return ServerError<Unit>();
        }
    }
}
