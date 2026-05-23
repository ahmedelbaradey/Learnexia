using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Learning.Domain.Entities;

/// <summary>
/// A lesson within a <see cref="Unit"/>. A lesson optionally teaches one <see cref="Skill"/>
/// (Lesson *—o1 Skill; <see cref="SkillId"/> nullable).
/// </summary>
public class Lesson : AggregateRoot
{
    public string Name { get; set; } = null!;
    public DifficultyLevel Difficulty { get; set; }
    public int SequenceOrder { get; set; }
    public bool IsLocked { get; set; }

    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public int? SkillId { get; set; }
    public Skill? Skill { get; set; }
}
