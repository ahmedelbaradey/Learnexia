using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Dtos;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Dtos;

public record LessonDto : BaseDto
{
    public string Name { get; set; } = null!;
    public DifficultyLevel Difficulty { get; set; }
    public int SequenceOrder { get; set; }
    public bool IsLocked { get; set; }
    public int UnitId { get; set; }

    /// <summary>Optional — a lesson teaches at most one skill (Lesson *—o1 Skill).</summary>
    public int? SkillId { get; set; }

    /// <summary>
    /// P7-02: Estimated lesson duration in minutes. 0 when not yet set.
    /// Writable via EditLessonCommand; guarded from mass-assignment on IsActive/SequenceOrder.
    /// </summary>
    public int EstimatedMinutes { get; set; }
}
