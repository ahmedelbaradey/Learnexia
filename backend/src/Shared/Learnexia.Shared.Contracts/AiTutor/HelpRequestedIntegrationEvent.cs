using Learnexia.Shared.Contracts.Ai;

namespace Learnexia.Shared.Contracts.AiTutor;

/// <summary>
/// Emitted fire-and-forget at the entry of a helper handler (before any LLM call).
/// Consumed by P5-03 product analytics and P6-02 AI quality evaluation.
/// </summary>
public sealed record HelpRequestedIntegrationEvent(
    int StudentId,
    HelperIntent Intent,
    int SkillId,
    int? QuestionId) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
