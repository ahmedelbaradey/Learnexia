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

    /// <summary>
    /// Optional seeded/static explanation (Markdown). Phase 2: populated by seeder.
    /// Phase 3: source will be the AI tutor (P3-04). NULL-allowed.
    /// </summary>
    public string? Explanation { get; set; }

    /// <summary>
    /// Optional visual example — a URL or short asset key rendered by the FE.
    /// Max 1024 characters. NULL-allowed.
    /// </summary>
    public string? Visual { get; set; }
}
