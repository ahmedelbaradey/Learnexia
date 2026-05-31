namespace Learnexia.Modules.Gamification.Domain.Enums;

// Discriminator for badge award predicates. Drives dispatch in BadgePredicateEvaluator. FR-GM-4.
public enum BadgeTriggerType
{
    FirstLesson = 1,
    StreakThreshold = 2,
    LevelThreshold = 3,
}
