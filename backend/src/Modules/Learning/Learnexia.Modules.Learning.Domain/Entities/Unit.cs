using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Learning.Domain.Entities;

/// <summary>
/// An ordered teaching unit within a <see cref="Subject"/>. Unit 1—* Lesson.
/// </summary>
public class Unit : AggregateRoot
{
    public string Name { get; set; } = null!;
    public int SequenceOrder { get; set; }

    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    public List<Lesson> Lessons { get; set; } = null!;
}
