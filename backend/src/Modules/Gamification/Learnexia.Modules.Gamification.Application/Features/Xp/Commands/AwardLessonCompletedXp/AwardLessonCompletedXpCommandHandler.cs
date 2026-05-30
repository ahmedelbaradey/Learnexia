using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Common;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Gamification.Application.Features.Xp.Commands.AwardLessonCompletedXp;

/// <summary>
/// Handles <see cref="AwardLessonCompletedXpCommand"/>.
///
/// Writes TWO <see cref="XpAward"/> rows in one transaction when accuracy ≥ threshold:
///   - One <c>LessonComplete</c> award (+50, always on completion)
///   - One <c>QuizPass</c> award (+20, only if accuracy ≥ <c>QuizPassAccuracyThreshold</c>)
///
/// Each award is idempotency-checked separately by (OriginEventId, Reason) — AC4.
/// After writing, updates <see cref="StudentXpProfile"/> via <c>ApplyAward</c> and recomputes level
/// via <see cref="LevelCurve"/>. Level-ups raise <c>StudentLeveledUpDomainEvent</c> (dispatched after
/// commit by <c>UnitOfWorkBehavior</c>).
///
/// A row-lock is acquired via <see cref="IGamificationRepository.AcquireProfileLockAsync"/> before
/// the profile read to prevent concurrent events from racing on the same student's XP total (Q7).
/// </summary>
public class AwardLessonCompletedXpCommandHandler
    : BaseResponseHandler, ICommandHandler<AwardLessonCompletedXpCommand, BaseResponse<Unit>>
{
    private readonly IGamificationRepository _repo;
    private readonly ILoggerManager _logger;

    public AwardLessonCompletedXpCommandHandler(
        IGamificationRepository repo,
        ILoggerManager logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<BaseResponse<Unit>> Handle(
        AwardLessonCompletedXpCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // ── LessonComplete award ─────────────────────────────────────────────────────────
            bool lessonAlreadyAwarded = await _repo.HasXpAwardAsync(
                request.OriginEventId, XpReason.LessonCompleted, cancellationToken);

            if (lessonAlreadyAwarded)
            {
                _logger.LogInfo(
                    $"P4-02: duplicate LessonCompleted event {request.OriginEventId} " +
                    $"for student {request.StudentId} — LessonComplete award already exists, skipping.");
                return Success(Unit.Value);
            }

            // Row-lock on profile row before read to prevent concurrent XP race (Q7).
            await _repo.AcquireProfileLockAsync(request.StudentId, cancellationToken);

            var profile = await _repo.GetProfileByStudentIdAsync(request.StudentId, cancellationToken)
                          ?? StudentXpProfile.CreateFor(request.StudentId);

            // Ensure the profile is tracked (new or existing) so EF can resolve FK at SaveChanges.
            _repo.UpsertXpProfile(profile);

            int lessonAmount = GamificationConstants.XpRewards.LessonComplete;
            var lessonAward = XpAward.Create(
                profile: profile,
                reason: XpReason.LessonCompleted,
                xpAmount: lessonAmount,
                originEventId: request.OriginEventId,
                occurredAtUtc: request.OccurredAtUtc,
                lessonId: request.LessonId,
                skillId: request.SkillId);

            await _repo.AddXpAwardAsync(lessonAward, cancellationToken);

            int newLevel = LevelCurve.LevelForXp(profile.TotalXp + lessonAmount);
            profile.ApplyAward(lessonAmount, newLevel);

            _logger.LogInfo(
                $"P4-02: LessonComplete +{lessonAmount} XP awarded to student {request.StudentId} " +
                $"(eventId={request.OriginEventId}, lessonId={request.LessonId}).");

            // ── QuizPass award (accuracy threshold) ─────────────────────────────────────────
            if (request.AccuracyPercentage >= GamificationConstants.XpRewards.QuizPassAccuracyThreshold)
            {
                bool quizAlreadyAwarded = await _repo.HasXpAwardAsync(
                    request.OriginEventId, XpReason.QuizCompleted, cancellationToken);

                if (quizAlreadyAwarded)
                {
                    _logger.LogInfo(
                        $"P4-02: duplicate LessonCompleted event {request.OriginEventId} " +
                        $"for student {request.StudentId} — QuizPass award already exists, skipping.");
                }
                else
                {
                    int quizAmount = GamificationConstants.XpRewards.QuizPass;
                    var quizAward = XpAward.Create(
                        profile: profile,
                        reason: XpReason.QuizCompleted,
                        xpAmount: quizAmount,
                        originEventId: request.OriginEventId,
                        occurredAtUtc: request.OccurredAtUtc,
                        lessonId: request.LessonId,
                        skillId: request.SkillId);

                    await _repo.AddXpAwardAsync(quizAward, cancellationToken);

                    int levelAfterQuiz = LevelCurve.LevelForXp(profile.TotalXp + quizAmount);
                    profile.ApplyAward(quizAmount, levelAfterQuiz);

                    _logger.LogInfo(
                        $"P4-02: QuizPass +{quizAmount} XP awarded to student {request.StudentId} " +
                        $"(eventId={request.OriginEventId}).");
                }
            }

            return Success(Unit.Value);
        }
        catch (DbUpdateException dbEx)
        {
            // Defense-in-depth: unique-constraint violation from a concurrent duplicate delivery.
            _logger.LogError(dbEx,
                $"P4-02: DbUpdateException awarding LessonCompleted XP for student {request.StudentId} " +
                $"(eventId={request.OriginEventId}) — likely idempotency race, treated as already-applied.");
            return Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-02: Unexpected error in AwardLessonCompletedXpCommandHandler " +
                $"(studentId={request.StudentId}, eventId={request.OriginEventId}).");
            return ServerError<Unit>();
        }
    }
}
