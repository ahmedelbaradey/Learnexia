using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Learning.Domain.Entities;

/// <summary>
/// A learnable skill within a <see cref="Concept"/>. Carries the mastery threshold (% required to be
/// considered mastered, cf. FR-AD-3) and an estimated time to learn. A skill may be taught by many lessons.
/// </summary>
public class Skill : AggregateRoot
{
    public string Name { get; set; } = null!;
    public int MasteryThreshold { get; set; }
    public int EstimatedTimeMinutes { get; set; }

    public int ConceptId { get; set; }
    public Concept Concept { get; set; } = null!;

    public List<Lesson> Lessons { get; set; } = null!;
}
