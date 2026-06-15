using Learnexia.Modules.Gamification.Application.Features.Hearts.Commands.LoseHeart;
using Learnexia.Modules.Gamification.Application.Features.Hearts.Commands.RegainHeartFromPractice;
using Learnexia.Shared.Kernel.Responses;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Abstractions;

/// <summary>
/// Workflow service for the Hearts aggregate.
/// Owns idempotency, lazy refill, profile load/upsert, heart mutation, and audit row staging.
/// Transactions are owned by UnitOfWorkBehavior.
/// </summary>
public interface IHeartService
{
    /// <summary>
    /// Records a heart loss for a wrong answer.
    /// Applies stale-event guard, idempotency check, lazy-refill, and heart decrement.
    /// Always writes the HeartLoss audit row even when already in Practice Mode.
    /// Returns Success(Unit.Value) in all non-error paths.
    /// </summary>
    Task<BaseResponse<Unit>> LoseHeartAsync(
        LoseHeartCommand command,
        CancellationToken ct = default);

    /// <summary>
    /// Restores +1 heart from a correct answer while in Practice Mode.
    /// No-op when student is not at Hearts == 0.
    /// Returns Success(Unit.Value) in all non-error paths.
    /// </summary>
    Task<BaseResponse<Unit>> RegainHeartFromPracticeAsync(
        RegainHeartFromPracticeCommand command,
        CancellationToken ct = default);
}
