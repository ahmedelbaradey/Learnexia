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

        /// <summary>
        /// Rarity-scaled XP bonus awarded when a badge is earned (P4-05 — D3).
        /// Common +20, Rare +50, Epic +100, Legendary +250.
        /// </summary>
        public static class BadgeEarned
        {
            public const int Common    = 20;
            public const int Rare      = 50;
            public const int Epic      = 100;
            public const int Legendary = 250;

            /// <summary>Returns the XP bonus for the given <paramref name="rarity"/>.</summary>
            public static int ForRarity(BadgeRarity rarity) => rarity switch
            {
                BadgeRarity.Common    => Common,
                BadgeRarity.Rare      => Rare,
                BadgeRarity.Epic      => Epic,
                BadgeRarity.Legendary => Legendary,
                _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, "Unknown BadgeRarity"),
            };
        }
    }

    /// <summary>
    /// Hearts engine constants (P4-04). Used to initialise <see cref="Configuration.HeartsOptions"/>
    /// defaults and to seed brand-new <see cref="Domain.Entities.StudentXpProfile"/> rows.
    /// </summary>
    public static class Hearts
    {
        /// <summary>Maximum hearts a student may hold at once. Default = 5.</summary>
        public const int Cap = 5;

        /// <summary>Minutes required for one heart to regenerate. Default = 30.</summary>
        public const int RefillIntervalMinutes = 30;
    }
}
