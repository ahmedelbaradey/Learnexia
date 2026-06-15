using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Common;
using Learnexia.Modules.Gamification.Application.Configuration;
using Learnexia.Modules.Gamification.Application.Features.Events.Boost;
using Learnexia.Modules.Gamification.Application.Features.Xp.Commands.AwardAnswerSubmittedXp;
using Learnexia.Modules.Gamification.Application.Features.Xp.Commands.AwardLessonCompletedXp;
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
/// Infrastructure implementation of <see cref="IXpService"/>.
/// Owns idempotency, practice-mode gate, profile load/upsert, XP math, and row staging
/// via <see cref="IGamificationRepository"/>. Transactions are owned by UnitOfWorkBehavior.
/// </summary>
internal sealed class XpService : BaseResponseHandler, IXpService
{
    private readonly IGamificationRepository _repo;
    private readonly IOptions<HeartsOptions> _heartsOptions;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;
    private readonly IXpBoostCalculator _boostCalc;

    public XpService(
        IGamificationRepository repo,
        IOptions<HeartsOptions> heartsOptions,
        ISystemClock clock,
        ILoggerManager logger,
        IXpBoostCalculator boostCalc)
    {
        _repo = repo;
        _heartsOptions = heartsOptions;
        _clock = clock;
        _logger = logger;
        _boostCalc = boostCalc;
    }

    /// <inheritdoc />
    public async Task<BaseResponse<Unit>> AwardAnswerSubmittedXpAsync(
        AwardAnswerSubmittedXpCommand request,
        CancellationToken ct = default)
    {
        try
        {
            // Belt-and-suspenders: skip if this was a wrong answer.
            if (request.CorrectAnswerCount == 0)
                return Success(Unit.Value);

            // AC4 idempotency pre-check.
            bool alreadyAwarded = await _repo.HasXpAwardAsync(
                request.OriginEventId, XpReason.CorrectAnswer, ct);

            if (alreadyAwarded)
            {
                _logger.LogInfo(
                    $"P4-02: duplicate AnswerSubmitted event {request.OriginEventId} " +
                    $"for student {request.StudentId} — CorrectAnswer award already exists, skipping.");
                return Success(Unit.Value);
            }

            // Row-lock on profile row before read to prevent concurrent XP race (Q7).
            await _repo.AcquireProfileLockAsync(request.StudentId, ct);

            var profile = await _repo.GetProfileByStudentIdAsync(request.StudentId, ct)
                          ?? StudentXpProfile.CreateFor(request.StudentId);

            // Ensure the profile is tracked (new or existing) so EF can resolve FK at SaveChanges.
            _repo.UpsertXpProfile(profile);

            // ── P4-04 Practice-Mode gate ──────────────────────────────────────────────────────
            // Lazy-refill first so a student whose hearts have ticked back up is not falsely gated.
            var opts = _heartsOptions.Value;
            profile.RefreshHeartsAgainst(_clock.UtcNow, opts.Cap, opts.RefillIntervalMinutes);

            if (profile.InPracticeMode)
            {
                _logger.LogInfo(
                    $"P4-04: Practice Mode — AnswerSubmittedXP suspended (studentId={request.StudentId}).");
                return Success(Unit.Value);
            }

            int amount = await _boostCalc.GetEffectiveAmountAsync(
                GamificationConstants.XpRewards.CorrectAnswer,
                XpReason.CorrectAnswer,
                request.OccurredAtUtc,
                ct);

            var award = XpAward.Create(
                profile: profile,
                reason: XpReason.CorrectAnswer,
                xpAmount: amount,
                originEventId: request.OriginEventId,
                occurredAtUtc: request.OccurredAtUtc,
                lessonId: request.LessonId,
                skillId: request.SkillId);

            await _repo.AddXpAwardAsync(award, ct);

            int newLevel = LevelCurve.LevelForXp(profile.TotalXp + amount);
            profile.ApplyAward(amount, newLevel, XpReason.CorrectAnswer, request.OccurredAtUtc);

            _logger.LogInfo(
                $"P4-02: CorrectAnswer +{amount} XP awarded to student {request.StudentId} " +
                $"(eventId={request.OriginEventId}, lessonId={request.LessonId}).");

            return Success(Unit.Value);
        }
        catch (GamificationUniqueConstraintException ucEx)
        {
            // Defense-in-depth: unique-constraint violation from a concurrent duplicate delivery.
            _logger.LogError(ucEx,
                $"P4-02: unique-constraint violation awarding CorrectAnswer XP for student {request.StudentId} " +
                $"(eventId={request.OriginEventId}) — likely idempotency race, treated as already-applied.");
            return Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-02: Unexpected error in XpService.AwardAnswerSubmittedXpAsync " +
                $"(studentId={request.StudentId}, eventId={request.OriginEventId}).");
            return ServerError<Unit>();
        }
    }

    /// <inheritdoc />
    public async Task<BaseResponse<Unit>> AwardLessonCompletedXpAsync(
        AwardLessonCompletedXpCommand request,
        CancellationToken ct = default)
    {
        try
        {
            // ── LessonComplete award ─────────────────────────────────────────────────────────
            bool lessonAlreadyAwarded = await _repo.HasXpAwardAsync(
                request.OriginEventId, XpReason.LessonCompleted, ct);

            if (lessonAlreadyAwarded)
            {
                _logger.LogInfo(
                    $"P4-02: duplicate LessonCompleted event {request.OriginEventId} " +
                    $"for student {request.StudentId} — LessonComplete award already exists, skipping.");
                return Success(Unit.Value);
            }

            // Row-lock on profile row before read to prevent concurrent XP race (Q7).
            await _repo.AcquireProfileLockAsync(request.StudentId, ct);

            var profile = await _repo.GetProfileByStudentIdAsync(request.StudentId, ct)
                          ?? StudentXpProfile.CreateFor(request.StudentId);

            // Ensure the profile is tracked (new or existing) so EF can resolve FK at SaveChanges.
            _repo.UpsertXpProfile(profile);

            // ── P4-04 Practice-Mode gate ──────────────────────────────────────────────────────
            // Lazy-refill first so a student whose hearts have ticked back up is not falsely gated.
            var opts = _heartsOptions.Value;
            profile.RefreshHeartsAgainst(_clock.UtcNow, opts.Cap, opts.RefillIntervalMinutes);

            if (profile.InPracticeMode)
            {
                _logger.LogInfo(
                    $"P4-04: Practice Mode — LessonCompletedXP suspended (studentId={request.StudentId}).");
                return Success(Unit.Value);
            }

            int lessonAmount = await _boostCalc.GetEffectiveAmountAsync(
                GamificationConstants.XpRewards.LessonComplete,
                XpReason.LessonCompleted,
                request.OccurredAtUtc,
                ct);

            var lessonAward = XpAward.Create(
                profile: profile,
                reason: XpReason.LessonCompleted,
                xpAmount: lessonAmount,
                originEventId: request.OriginEventId,
                occurredAtUtc: request.OccurredAtUtc,
                lessonId: request.LessonId,
                skillId: request.SkillId);

            await _repo.AddXpAwardAsync(lessonAward, ct);

            int newLevel = LevelCurve.LevelForXp(profile.TotalXp + lessonAmount);
            profile.ApplyAward(lessonAmount, newLevel, XpReason.LessonCompleted, request.OccurredAtUtc);

            _logger.LogInfo(
                $"P4-02: LessonComplete +{lessonAmount} XP awarded to student {request.StudentId} " +
                $"(eventId={request.OriginEventId}, lessonId={request.LessonId}).");

            // ── QuizPass award (accuracy threshold) ─────────────────────────────────────────
            if (request.AccuracyPercentage >= GamificationConstants.XpRewards.QuizPassAccuracyThreshold)
            {
                bool quizAlreadyAwarded = await _repo.HasXpAwardAsync(
                    request.OriginEventId, XpReason.QuizCompleted, ct);

                if (quizAlreadyAwarded)
                {
                    _logger.LogInfo(
                        $"P4-02: duplicate LessonCompleted event {request.OriginEventId} " +
                        $"for student {request.StudentId} — QuizPass award already exists, skipping.");
                }
                else
                {
                    int quizAmount = await _boostCalc.GetEffectiveAmountAsync(
                        GamificationConstants.XpRewards.QuizPass,
                        XpReason.QuizCompleted,
                        request.OccurredAtUtc,
                        ct);

                    var quizAward = XpAward.Create(
                        profile: profile,
                        reason: XpReason.QuizCompleted,
                        xpAmount: quizAmount,
                        originEventId: request.OriginEventId,
                        occurredAtUtc: request.OccurredAtUtc,
                        lessonId: request.LessonId,
                        skillId: request.SkillId);

                    await _repo.AddXpAwardAsync(quizAward, ct);

                    int levelAfterQuiz = LevelCurve.LevelForXp(profile.TotalXp + quizAmount);
                    profile.ApplyAward(quizAmount, levelAfterQuiz, XpReason.QuizCompleted, request.OccurredAtUtc);

                    _logger.LogInfo(
                        $"P4-02: QuizPass +{quizAmount} XP awarded to student {request.StudentId} " +
                        $"(eventId={request.OriginEventId}).");
                }
            }

            return Success(Unit.Value);
        }
        catch (GamificationUniqueConstraintException ucEx)
        {
            // Defense-in-depth: unique-constraint violation from a concurrent duplicate delivery.
            _logger.LogError(ucEx,
                $"P4-02: unique-constraint violation awarding LessonCompleted XP for student {request.StudentId} " +
                $"(eventId={request.OriginEventId}) — likely idempotency race, treated as already-applied.");
            return Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-02: Unexpected error in XpService.AwardLessonCompletedXpAsync " +
                $"(studentId={request.StudentId}, eventId={request.OriginEventId}).");
            return ServerError<Unit>();
        }
    }
}
