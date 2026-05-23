using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Learning.Domain.Entities;

/// <summary>
/// A school grade (1–6). Lead decision Q1: Grade is a separate entity, not an int column on Subject.
/// A Subject belongs to exactly one Grade (Grade 1—* Subject).
/// </summary>
public class Grade : AggregateRoot
{
    public int Number { get; set; }
    public string DisplayName { get; set; } = null!;

    public List<Subject> Subjects { get; set; } = null!;
}
