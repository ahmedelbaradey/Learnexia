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

    /// <summary>
    /// Marks this lesson as the end-of-unit boss / challenge (FR-LR-2 "boss" node category).
    /// Curriculum-authored — derived in <see cref="LearningSeeder"/> as the highest-SequenceOrder
    /// lesson per <see cref="Unit"/>. Orthogonal to <see cref="NodeState"/>: a boss can be
    /// Locked, Available, or Completed.
    /// Default: false. Phase 7 admin tooling (P7-03) will allow manual override.
    /// Seeded as true for the highest-SequenceOrder lesson in each Unit (per P2-03).
    /// FE renders a boss icon / different CTA when true; orthogonal to NodeState
    /// (a boss lesson can still be Locked, Available, or Completed).
    /// </summary>
    public bool IsBoss { get; set; }
}
