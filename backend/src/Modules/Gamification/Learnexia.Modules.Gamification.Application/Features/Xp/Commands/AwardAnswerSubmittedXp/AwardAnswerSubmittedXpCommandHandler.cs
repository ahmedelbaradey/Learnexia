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

namespace Learnexia.Modules.Gamification.Application.Features.Xp.Commands.AwardAnswerSubmittedXp;

/// <summary>
/// Handles <see cref="AwardAnswerSubmittedXpCommand"/>.
///
/// Awards CorrectAnswer XP (+10) when the student submitted a correct answer
/// (<c>CorrectAnswerCount &gt; 0</c>). Belt-and-suspenders guard ensures no award on wrong answers
/// even if the command is sent directly in tests.
///
/// Idempotency: pre-checks (OriginEventId, CorrectAnswer) before inserting the <see cref="XpAward"/>
/// row. DB unique constraint is the backstop for concurrent duplicates (AC4).
///
/// A row-lock is acquired via <see cref="IGamificationRepository.AcquireProfileLockAsync"/> before
/// the profile read to prevent concurrent events from racing on the same student's XP total (Q7).
/// </summary>
public class AwardAnswerSubmittedXpCommandHandler
    : BaseResponseHandler, ICommandHandler<AwardAnswerSubmittedXpCommand, BaseResponse<Unit>>
{
    private readonly IGamificationRepository _repo;
    private readonly ILoggerManager _logger;

    public AwardAnswerSubmittedXpCommandHandler(
        IGamificationRepository repo,
        ILoggerManager logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<BaseResponse<Unit>> Handle(
        AwardAnswerSubmittedXpCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Belt-and-suspenders: skip if this was a wrong answer.
            if (request.CorrectAnswerCount == 0)
                return Success(Unit.Value);

            // AC4 idempotency pre-check.
            bool alreadyAwarded = await _repo.HasXpAwardAsync(
                request.OriginEventId, XpReason.CorrectAnswer, cancellationToken);

            if (alreadyAwarded)
            {
                _logger.LogInfo(
                    $"P4-02: duplicate AnswerSubmitted event {request.OriginEventId} " +
                    $"for student {request.StudentId} — CorrectAnswer award already exists, skipping.");
                return Success(Unit.Value);
            }

            // Row-lock on profile row before read to prevent concurrent XP race (Q7).
            await _repo.AcquireProfileLockAsync(request.StudentId, cancellationToken);

            var profile = await _repo.GetProfileByStudentIdAsync(request.StudentId, cancellationToken)
                          ?? StudentXpProfile.CreateFor(request.StudentId);

            // Ensure the profile is tracked (new or existing) so EF can resolve FK at SaveChanges.
            _repo.UpsertXpProfile(profile);

            int amount = GamificationConstants.XpRewards.CorrectAnswer;

            var award = XpAward.Create(
                profile: profile,
                reason: XpReason.CorrectAnswer,
                xpAmount: amount,
                originEventId: request.OriginEventId,
                occurredAtUtc: request.OccurredAtUtc,
                lessonId: request.LessonId,
                skillId: request.SkillId);

            await _repo.AddXpAwardAsync(award, cancellationToken);

            int newLevel = LevelCurve.LevelForXp(profile.TotalXp + amount);
            profile.ApplyAward(amount, newLevel);

            _logger.LogInfo(
                $"P4-02: CorrectAnswer +{amount} XP awarded to student {request.StudentId} " +
                $"(eventId={request.OriginEventId}, lessonId={request.LessonId}).");

            return Success(Unit.Value);
        }
        catch (DbUpdateException dbEx)
        {
            // Defense-in-depth: unique-constraint violation from a concurrent duplicate delivery.
            _logger.LogError(dbEx,
                $"P4-02: DbUpdateException awarding CorrectAnswer XP for student {request.StudentId} " +
                $"(eventId={request.OriginEventId}) — likely idempotency race, treated as already-applied.");
            return Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-02: Unexpected error in AwardAnswerSubmittedXpCommandHandler " +
                $"(studentId={request.StudentId}, eventId={request.OriginEventId}).");
            return ServerError<Unit>();
        }
    }
}
