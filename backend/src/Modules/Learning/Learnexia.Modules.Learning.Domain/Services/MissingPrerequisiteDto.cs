namespace Learnexia.Modules.Learning.Domain.Services;

/// <summary>
/// Explains a single unmet prerequisite for a locked lesson.
/// Used in <see cref="LessonUnlockStateDto.MissingPrerequisites"/>.
/// All 5 fields are required for AC3 (Q9): the frontend renders
/// "You need {RequiredAccuracy}% on {PrereqSkillName} — you're at {CurrentAccuracy}%."
/// </summary>
public sealed record MissingPrerequisiteDto(
    int PrereqSkillId,
    string PrereqSkillName,
    int PrereqNodeId,
    int RequiredAccuracy,       // Skill.MasteryThreshold (int 0..100 as percentage)
    double CurrentAccuracy);    // Student's current AccuracyPercentage (0.0 if TotalAnswers == 0)
