using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Streaks.Commands.AdvanceStreak;

/// <summary>
/// Advances (or resets to 1) a student's streak on the date of a qualifying lesson completion.
/// Sent by <c>LessonCompletedIntegrationEventHandler</c> as a second <see cref="MediatR.IMediator.Send"/>
/// after the XP award command. Runs through <c>UnitOfWorkBehavior</c> so the streak update + streak-bonus
/// <c>XpAward</c> row commit atomically in their own transaction (independent of the XP award's transaction).
///
/// Idempotency: three-layer defense —
///   (a) <c>HasXpAwardAsync(OriginEventId, StreakBonus)</c> pre-check,
///   (b) date-based same-day check on <c>LastActivityDateUtc</c>,
///   (c) DB unique index <c>UX_XpAwards_OriginEventId_Reason</c> catches any concurrent race.
/// </summary>
public record AdvanceStreakCommand(
    int StudentId,
    Guid OriginEventId,
    DateTime OccurredAtUtc,
    int? LessonId,
    int? SkillId) : ICommand<BaseResponse<Unit>>;
