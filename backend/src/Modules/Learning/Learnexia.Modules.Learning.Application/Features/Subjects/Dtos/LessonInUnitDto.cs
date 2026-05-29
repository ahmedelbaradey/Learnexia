using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;

/// <summary>
/// Student-facing lesson summary nested inside <see cref="UnitWithLessonsDto"/>.
/// </summary>
public record LessonInUnitDto
{
    public int LessonId { get; init; }
    public string Name { get; init; } = null!;
    public DifficultyLevel Difficulty { get; init; }
    public int SequenceOrder { get; init; }

    /// <remarks>
    /// Deprecated. Replaced by <see cref="State"/> (engine-derived per-student NodeState from P2-04).
    /// Kept for one wave (back-compat). Will be removed in P2-09 or P6-06.
    /// </remarks>
    [Obsolete("Replaced by LearningPathEngine in P2-04. Will be removed in P2-09 or P6-06.")]
    public bool IsLocked { get; init; }

    public int? SkillId { get; init; }

    /// <summary>Engine-derived per-student node state. Available/Locked/Completed.</summary>
    public NodeState State { get; init; }

    /// <summary>
    /// Explains unmet prerequisites when <see cref="State"/> is <see cref="NodeState.Locked"/>.
    /// Empty when Available or Completed.
    /// </summary>
    public IReadOnlyList<MissingPrerequisiteDto> MissingPrerequisites { get; init; }
        = Array.Empty<MissingPrerequisiteDto>();
}
