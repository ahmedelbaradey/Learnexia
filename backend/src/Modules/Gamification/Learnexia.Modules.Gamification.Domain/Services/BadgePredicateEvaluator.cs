using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;

namespace Learnexia.Modules.Gamification.Domain.Services;

/// <summary>
/// Pure static badge predicate evaluator. No DI, no DB access — fully unit-testable in isolation.
/// Mirrors the shape of <see cref="LevelCurve"/>, <see cref="StreakDayCalculator"/>, and
/// <see cref="LazyHeartRefiller"/> (pure static services in the Domain layer).
///
/// Called by the three notification handlers in <c>Gamification.Application/Features/Badges/EventHandlers/</c>
/// after they load the catalog and the student's earned-badge set from the repository.
/// </summary>
public static class BadgePredicateEvaluator
{
    /// <summary>
    /// Returns the <see cref="BadgeDefinition"/>s that match the given trigger AND have not been
    /// earned yet.
    ///
    /// <list type="bullet">
    ///   <item><term>FirstLesson</term><description>Matches all definitions with TriggerType = FirstLesson, regardless of value.</description></item>
    ///   <item><term>StreakThreshold</term><description>Matches definitions with TriggerType = StreakThreshold AND Threshold &lt;= value.</description></item>
    ///   <item><term>LevelThreshold</term><description>Matches definitions with TriggerType = LevelThreshold AND Threshold &lt;= value.</description></item>
    /// </list>
    ///
    /// Filters out any definition whose Id is in <paramref name="alreadyEarned"/> (idempotency at
    /// the predicate layer — the DB unique constraint is the hard backstop).
    /// </summary>
    /// <param name="triggerType">Type of trigger event.</param>
    /// <param name="value">Current streak (for StreakThreshold) / current level (for LevelThreshold) / 0 for FirstLesson.</param>
    /// <param name="definitions">All catalog rows for the relevant trigger type (caller filters or passes all).</param>
    /// <param name="alreadyEarned">Set of BadgeDefinitionIds the student has already earned.</param>
    public static IEnumerable<BadgeDefinition> Match(
        BadgeTriggerType triggerType,
        int value,
        IEnumerable<BadgeDefinition> definitions,
        ISet<int> alreadyEarned)
    {
        foreach (var def in definitions)
        {
            if (alreadyEarned.Contains(def.Id))
                continue;

            if (def.TriggerType != triggerType)
                continue;

            switch (triggerType)
            {
                case BadgeTriggerType.FirstLesson:
                    yield return def;
                    break;

                case BadgeTriggerType.StreakThreshold:
                case BadgeTriggerType.LevelThreshold:
                    if (def.Threshold.HasValue && def.Threshold.Value <= value)
                        yield return def;
                    break;
            }
        }
    }
}
