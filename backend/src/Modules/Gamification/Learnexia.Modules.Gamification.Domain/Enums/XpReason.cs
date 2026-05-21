namespace Learnexia.Modules.Gamification.Domain.Enums;

// The deterministic source of an XP award. The default point values per reason live in
// GamificationConstants.XpRewards (not magic numbers at the call site). FR-GM-1.
public enum XpReason
{
    CorrectAnswer = 1,
    QuizCompleted = 2,
    LessonCompleted = 3,
    StreakBonus = 4,
}
