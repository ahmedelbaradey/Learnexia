using Learnexia.Shared.Contracts.Ai;

namespace Learnexia.Shared.Contracts.AiTutor;

/// <summary>
/// Emitted fire-and-forget when the helper refuses to answer (no grounding context).
/// Consumed by P5-03 product analytics and P6-02 AI quality evaluation.
/// </summary>
public sealed record HelpDeclinedIntegrationEvent(
    int StudentId,
    HelperIntent Intent,
    int SkillId,
    string Reason) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
