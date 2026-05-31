using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Hearts.Commands.RegainHeartFromPractice;

/// <summary>
/// Restores +1 heart for a correct answer submitted while in Practice Mode (<c>Hearts == 0</c>).
/// Sent by <see cref="IntegrationEventHandlers.AnswerSubmittedIntegrationEventHandler"/> via
/// <c>AwardAnswerSubmittedXpCommandHandler</c>'s Practice Mode gate (B2b-2) when
/// <c>RegenOnCorrectAnswerInPracticeMode = true</c> (D2 / Q3 = yes).
///
/// Idempotency: the regen is bounded by the <c>Hearts == 0</c> condition. If multiple redelivered
/// events arrive after the first regen raises Hearts to 1, they all no-op at the Practice Mode check.
/// No HeartLoss row is written (regen is not audited — symmetry with XP not auditing per-tick).
///
/// Runs through <c>UnitOfWorkBehavior</c> for an atomic profile update.
/// </summary>
public record RegainHeartFromPracticeCommand(
    int StudentId,
    Guid OriginEventId) : ICommand<BaseResponse<Unit>>;
