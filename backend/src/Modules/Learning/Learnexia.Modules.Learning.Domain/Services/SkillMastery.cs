namespace Learnexia.Modules.Learning.Domain.Services;

/// <summary>
/// Immutable per-skill mastery snapshot for a student, pre-fetched by the caller.
/// <see cref="AccuracyPercentage"/> is in the range 0..100 (percentage scale). <see cref="TotalAnswers"/> &gt;= 1
/// is required for a skill to be considered mastered — a student with zero answers is never mastered
/// even if <c>Skill.MasteryThreshold == 0</c> (Q2 guard).
///
/// CONTRACT: the caller must supply a <see cref="SkillMastery"/> entry for every skill in the subject
/// (even skills with zero student-answer rows). For zero-answer skills, set
/// <c>AccuracyPercentage = 0</c> and <c>TotalAnswers = 0</c>. This gives <see cref="LearningPathEngine"/>
/// the skill identity without requiring a separate look-up.
/// </summary>
public sealed record SkillMastery(
    int SkillId,
    double AccuracyPercentage,
    int TotalAnswers);
