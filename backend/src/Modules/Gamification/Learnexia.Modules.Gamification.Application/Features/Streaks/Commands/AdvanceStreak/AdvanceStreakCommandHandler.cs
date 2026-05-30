using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Common;
using Learnexia.Modules.Gamification.Application.Configuration;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Application.Features.Streaks.Commands.AdvanceStreak;

/// <summary>
/// Handles <see cref="AdvanceStreakCommand"/>.
///
/// Steps (inside UnitOfWorkBehavior transaction):
///   1. Idempotency pre-check: skip if StreakBonus XpAward already exists for this OriginEventId.
///   2. Row-lock on the profile row (prevent concurrent advance race for the same student).
///   3. Compute day-bucket via <see cref="StreakDayCalculator.DayOf"/>.
///   4. Load or create profile.
///   5. Classify via <see cref="StreakDayCalculator.Classify"/> and switch on <see cref="StreakDayCalculator.Transition"/>:
///      - NoOp        → return Success (same calendar day; only first lesson of the day pays the bonus).
///      - OutOfOrder  → log warning, return Success without write (stale / out-of-order event).
///      - FirstActivity / Advance → <c>profile.AdvanceStreak(activityDate)</c>.
///      - Reset       → <c>profile.ResetStreakAndStart(activityDate)</c>.
///   6. Award StreakBonus XP via XpAward.Create + AddXpAwardAsync.
///   7. Recompute level via LevelCurve + profile.ApplyAward (may raise StudentLeveledUpDomainEvent).
///   8. Return Success(Unit.Value). UoW commits streak + XpAward + level atomically.
///
/// DbUpdateException catch (idempotency race) mirrors AwardLessonCompletedXpCommandHandler.
/// Narrowed to unique-constraint violations only (SqlState 23505 / constraint name match via Message).
/// </summary>
public class AdvanceStreakCommandHandler
    : BaseResponseHandler, ICommandHandler<AdvanceStreakCommand, BaseResponse<Unit>>
{
    private readonly IGamificationRepository _repo;
    private readonly ISystemClock _clock;
    private readonly IOptions<StreakOptions> _streakOptions;
    private readonly ILoggerManager _logger;

    public AdvanceStreakCommandHandler(
        IGamificationRepository repo,
        ISystemClock clock,
        IOptions<StreakOptions> streakOptions,
        ILoggerManager logger)
    {
        _repo = repo;
        _clock = clock;
        _streakOptions = streakOptions;
        _logger = logger;
    }

    public async Task<BaseResponse<Unit>> Handle(
        AdvanceStreakCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // ── 1. Idempotency pre-check ──────────────────────────────────────────────────────
            bool alreadyAwarded = await _repo.HasXpAwardAsync(
                request.OriginEventId, XpReason.StreakBonus, cancellationToken);

            if (alreadyAwarded)
            {
                _logger.LogInfo(
                    $"P4-03: duplicate StreakBonus event {request.OriginEventId} " +
                    $"for student {request.StudentId} — award already exists, skipping.");
                return Success(Unit.Value);
            }

            // ── 2. Row-lock ───────────────────────────────────────────────────────────────────
            await _repo.AcquireProfileLockAsync(request.StudentId, cancellationToken);

            // ── 3. Compute day-bucket ─────────────────────────────────────────────────────────
            var activityDate = StreakDayCalculator.DayOf(
                request.OccurredAtUtc, _streakOptions.Value.TimeZoneId);

            // ── 4. Load or create profile ─────────────────────────────────────────────────────
            var profile = await _repo.GetProfileByStudentIdAsync(request.StudentId, cancellationToken)
                          ?? StudentXpProfile.CreateFor(request.StudentId);

            _repo.UpsertXpProfile(profile);

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
                    // Do not clobber a newer streak state.
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

            // ── 6. Award StreakBonus XP ───────────────────────────────────────────────────────
            int bonusAmount = GamificationConstants.XpRewards.StreakBonus;
            var award = XpAward.Create(
                profile: profile,
                reason: XpReason.StreakBonus,
                xpAmount: bonusAmount,
                originEventId: request.OriginEventId,
                occurredAtUtc: request.OccurredAtUtc,
                lessonId: request.LessonId,
                skillId: request.SkillId);

            await _repo.AddXpAwardAsync(award, cancellationToken);

            // ── 7. Recompute level ────────────────────────────────────────────────────────────
            int newLevel = LevelCurve.LevelForXp(profile.TotalXp + bonusAmount);
            profile.ApplyAward(bonusAmount, newLevel);

            _logger.LogInfo(
                $"P4-03: streak advanced " +
                $"(studentId={request.StudentId}, currentStreak={profile.CurrentStreak}, " +
                $"longestStreak={profile.LongestStreak}) — +{bonusAmount} StreakBonus XP awarded.");

            return Success(Unit.Value);
        }
        catch (DbUpdateException dbEx)
            when (dbEx.InnerException?.Message?.Contains("UX_XpAwards_OriginEventId_Reason") == true
                  || dbEx.InnerException?.Message?.Contains("23505") == true)
        {
            // Narrowed: only the unique-constraint violation on UX_XpAwards_OriginEventId_Reason is treated
            // as an idempotency race (concurrent duplicate delivery). Any other DbUpdateException falls
            // through to the outer catch and returns ServerError.
            // Using Message.Contains to avoid pulling Npgsql into the Application layer; Infrastructure owns Npgsql.
            _logger.LogInfo(
                $"P4-03: unique-constraint violation on XpAwards — concurrent duplicate delivery " +
                $"for student {request.StudentId} (eventId={request.OriginEventId}), treated as already-applied.");
            return Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-03: Unexpected error in AdvanceStreakCommandHandler " +
                $"(studentId={request.StudentId}, eventId={request.OriginEventId}).");
            return ServerError<Unit>();
        }
    }
}
