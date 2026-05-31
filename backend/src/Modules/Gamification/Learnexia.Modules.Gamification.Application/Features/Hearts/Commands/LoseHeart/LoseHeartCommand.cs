using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Hearts.Commands.LoseHeart;

/// <summary>
/// Decrements the student's Hearts counter by 1 for a wrong answer submission (P4-04, FR-GM-3).
/// Sent by <see cref="IntegrationEventHandlers.AnswerSubmittedIntegrationEventHandler"/> only
/// when <c>CorrectAnswerCount == 0</c>.
///
/// Idempotency: three-layer defense —
///   (a) <see cref="Abstractions.IGamificationRepository.HasHeartLossAsync"/> pre-check,
///   (b) Out-of-order guard (stale event with OccurredAtUtc older than last refill interval),
///   (c) DB unique index <c>UX_HeartLosses_OriginEventId</c> catches concurrent races.
///
/// Runs through <c>UnitOfWorkBehavior</c> for an atomic profile update + HeartLoss insert.
/// </summary>
public record LoseHeartCommand(
    int StudentId,
    Guid OriginEventId,
    DateTime OccurredAtUtc,
    int? LessonId,
    int? SkillId) : ICommand<BaseResponse<Unit>>;
