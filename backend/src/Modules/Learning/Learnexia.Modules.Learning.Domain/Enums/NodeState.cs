namespace Learnexia.Modules.Learning.Domain.Enums;

/// <summary>
/// Skill-tree node state visible to the student.
/// P2-02: values are static placeholders derived from Lesson.IsLocked.
/// P2-03/P2-04 will replace the derivation with real per-student progress.
/// </summary>
public enum NodeState
{
    Locked    = 0,
    Available = 1,
    Completed = 2
}
