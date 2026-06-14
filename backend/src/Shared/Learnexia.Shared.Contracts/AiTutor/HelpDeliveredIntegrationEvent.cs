using Learnexia.Shared.Contracts.Ai;

namespace Learnexia.Shared.Contracts.AiTutor;

/// <summary>
/// Emitted fire-and-forget when the AI helper response is successfully delivered to the student.
/// ContextSource distinguishes a live-generated response from a future cache hit.
/// Consumed by P5-03 product analytics and P6-02 AI quality evaluation.
/// </summary>
public sealed record HelpDeliveredIntegrationEvent(
    int StudentId,
    HelperIntent Intent,
    int SkillId,
    int? QuestionId,
    string ModelUsed,
    string ContextSource) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
