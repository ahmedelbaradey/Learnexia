using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Learning.Domain.Entities;

/// <summary>
/// A concept within a <see cref="Subject"/>. Concept 1—* Skill.
/// </summary>
public class Concept : AggregateRoot
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DifficultyLevel DifficultyLevel { get; set; }

    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    public List<Skill> Skills { get; set; } = null!;
}
