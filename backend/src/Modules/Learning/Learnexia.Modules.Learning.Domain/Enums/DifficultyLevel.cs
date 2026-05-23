namespace Learnexia.Modules.Learning.Domain.Enums;

/// <summary>
/// Difficulty rating for curriculum content. Stored as int (HasConversion&lt;int&gt;) per the
/// project no-free-text rule — referenced by member, never a magic string/int.
/// Used by <c>Lesson.Difficulty</c> and <c>Concept.DifficultyLevel</c> (cf. FR-AD-1).
/// </summary>
public enum DifficultyLevel
{
    Easy = 1,
    Medium = 2,
    Hard = 3,
}
