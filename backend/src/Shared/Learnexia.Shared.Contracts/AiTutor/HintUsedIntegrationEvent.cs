namespace Learnexia.Shared.Contracts.AiTutor;

/// <summary>
/// Emitted fire-and-forget when a hint (or why-wrong explanation) is successfully delivered
/// to a student. Consumed by the Learning module's <c>HintUsedIntegrationEventHandler</c>,
/// which increments <c>StudentAnswer.HintUsed = true</c> and <c>Attempt.HintsUsedCount++</c>.
///
/// <para>ADR 0002 direct-publish pattern — no cross-module FK between Ai and Learning.</para>
/// </summary>
public sealed record HintUsedIntegrationEvent(
    int QuestionId,
    int AttemptId,
    int HintLevel,
    int StudentId) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
