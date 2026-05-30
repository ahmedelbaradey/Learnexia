using Learnexia.Modules.Gamification.Domain.Enums;

namespace Learnexia.Modules.Gamification.Application.Common;

/// <summary>
/// Deterministic XP rule values for the Gamification module (FR-GM-1).
/// All reward amounts are centralised here — no magic numbers at call sites.
/// Stub paths: <see cref="XpRewards.QuizCompleted"/> and <see cref="XpRewards.StreakBonus"/>
/// values are defined but no producer exists this cycle (P4-03 / future quiz-boundary story).
/// </summary>
public static class GamificationConstants
{
    public static class XpRewards
    {
        public const int CorrectAnswer = 10;
        public const int QuizPass = 20;                    // stub — no producer this cycle
        public const int LessonComplete = 50;
        public const int StreakBonus = 30;                 // stub — no producer this cycle
        public const int QuizPassAccuracyThreshold = 70;  // percent
    }
}
