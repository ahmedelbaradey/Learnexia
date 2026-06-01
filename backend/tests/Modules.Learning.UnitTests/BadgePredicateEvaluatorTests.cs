using FluentAssertions;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Services;
using Xunit;

namespace Modules.Learning.UnitTests;

/// <summary>
/// Unit tests for <see cref="BadgePredicateEvaluator.Match"/> (P4-05, Batch 5).
///
/// Pure static function — no DbContext or DI required. All cases are deterministic.
///
/// Method signature under test:
///   Match(BadgeTriggerType triggerType, int value,
///         IEnumerable{BadgeDefinition} definitions, ISet{int} alreadyEarned)
///   → IEnumerable{BadgeDefinition}
///
/// Key rules:
///   - FirstLesson: matches any FirstLesson definition not already earned (value is ignored).
///   - StreakThreshold: matches definitions where Threshold &lt;= value (and not already earned).
///   - LevelThreshold: matches definitions where Threshold &lt;= value (and not already earned).
///   - Null Threshold: never matches (safely skipped).
///   - Wrong trigger type: filtered out (def.TriggerType != requested triggerType).
///
/// Coverage map (12 test cases from P4-05 plan B5-1 spec):
///   B1   FirstLesson, value=0, [FIRST_LESSON], earned=∅          → [FIRST_LESSON]
///   B2   FirstLesson, value=0, [FIRST_LESSON], earned={id}        → [] (idempotency)
///   B3   StreakThreshold, value=3, [S3, S7], earned=∅             → [S3]
///   B4   StreakThreshold, value=7, [S3, S7], earned=∅             → [S3, S7]
///   B5   StreakThreshold, value=7, [S3, S7], earned={S3.Id}       → [S7]
///   B6   StreakThreshold, value=2, [S3, S7], earned=∅             → []
///   B7   LevelThreshold, value=5, [L5, L10], earned=∅            → [L5]
///   B8   LevelThreshold, value=10, [L5, L10], earned=∅           → [L5, L10]
///   B9   LevelThreshold, value=100, [L5, L10, LEG50], earned=∅   → all 3
///   B10  StreakThreshold, value=10, [L5], earned=∅               → [] (wrong trigger)
///   B11  FirstLesson, value=0, [], earned=∅                       → []
///   B12  StreakThreshold, value=3, [S3-nullThresh], earned=∅      → [] (null threshold)
/// </summary>
public sealed class BadgePredicateEvaluatorTests
{
    // -------------------------------------------------------------------------
    // Helper to build a deterministic BadgeDefinition for test use.
    // Uses negative IDs (never 0) so they are distinct from seeded rows.
    // -------------------------------------------------------------------------

    private static BadgeDefinition MakeDef(
        int id,
        BadgeTriggerType triggerType,
        BadgeRarity rarity = BadgeRarity.Common,
        int? threshold = null)
    {
        var def = BadgeDefinition.Create(
            code:        $"TEST_{id}",
            name:        $"Test Badge {id}",
            description: "Test",
            iconKey:     $"icon.test_{id}",
            rarity:      rarity,
            sortOrder:   id * 10,
            triggerType: triggerType,
            threshold:   threshold,
            rewardXp:    20);

        // Force the Id via reflection since EF normally sets it from the DB.
        // BadgeDefinition inherits FullAuditedEntity → BaseEntity → has int Id { get; ... }.
        // We need a non-zero Id to make alreadyEarned set lookups work correctly.
        var idProp = typeof(BadgeDefinition).BaseType?.BaseType?.BaseType?
                         .GetProperty("Id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                     ?? typeof(BadgeDefinition).BaseType?.BaseType?
                         .GetProperty("Id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                     ?? typeof(BadgeDefinition).BaseType?
                         .GetProperty("Id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                     ?? typeof(BadgeDefinition)
                         .GetProperty("Id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        // Walk type hierarchy to find the Id property that has a setter
        var type = def.GetType();
        while (type != null)
        {
            var prop = type.GetProperty("Id",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (prop?.CanWrite == true)
            {
                prop.SetValue(def, id);
                break;
            }

            // Try backing field approach
            var field = type.GetField("<Id>k__BackingField",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(def, id);
                break;
            }

            type = type.BaseType;
        }

        return def;
    }

    // =========================================================================
    // B1 — FirstLesson: single definition, not yet earned → returns it
    // =========================================================================

    [Fact(DisplayName = "P405-B1 Match(FirstLesson, value=0, [FIRST_LESSON], earned=∅) → [FIRST_LESSON]")]
    public void B1_FirstLesson_NotYetEarned_ReturnsDefinition()
    {
        var firstLesson = MakeDef(1, BadgeTriggerType.FirstLesson);
        var definitions = new[] { firstLesson };
        var earned = new HashSet<int>();

        var result = BadgePredicateEvaluator.Match(
            BadgeTriggerType.FirstLesson, value: 0, definitions, earned).ToList();

        result.Should().HaveCount(1, "one FirstLesson badge in catalog, not yet earned");
        result[0].Code.Should().Be(firstLesson.Code);
    }

    // =========================================================================
    // B2 — FirstLesson: already earned → idempotency, returns empty
    // =========================================================================

    [Fact(DisplayName = "P405-B2 Match(FirstLesson, value=0, [FIRST_LESSON], earned={id}) → [] (idempotency)")]
    public void B2_FirstLesson_AlreadyEarned_ReturnsEmpty()
    {
        var firstLesson = MakeDef(1, BadgeTriggerType.FirstLesson);
        var definitions = new[] { firstLesson };
        var earned = new HashSet<int> { firstLesson.Id };

        var result = BadgePredicateEvaluator.Match(
            BadgeTriggerType.FirstLesson, value: 0, definitions, earned).ToList();

        result.Should().BeEmpty("FIRST_LESSON already in earned set — idempotency must filter it out");
    }

    // =========================================================================
    // B3 — StreakThreshold value=3: only STREAK_3 threshold crossed
    // =========================================================================

    [Fact(DisplayName = "P405-B3 Match(StreakThreshold, value=3, [STREAK_3, STREAK_7], earned=∅) → [STREAK_3] only")]
    public void B3_StreakThreshold_Value3_OnlyStreak3Matches()
    {
        var streak3 = MakeDef(10, BadgeTriggerType.StreakThreshold, BadgeRarity.Common, threshold: 3);
        var streak7 = MakeDef(11, BadgeTriggerType.StreakThreshold, BadgeRarity.Rare,   threshold: 7);
        var definitions = new[] { streak3, streak7 };
        var earned = new HashSet<int>();

        var result = BadgePredicateEvaluator.Match(
            BadgeTriggerType.StreakThreshold, value: 3, definitions, earned).ToList();

        result.Should().HaveCount(1, "streak=3 satisfies STREAK_3 (threshold 3 ≤ 3) but NOT STREAK_7 (threshold 7 > 3)");
        result[0].Code.Should().Be(streak3.Code);
    }

    // =========================================================================
    // B4 — StreakThreshold value=7: both STREAK_3 and STREAK_7 crossed
    // =========================================================================

    [Fact(DisplayName = "P405-B4 Match(StreakThreshold, value=7, [STREAK_3, STREAK_7], earned=∅) → [STREAK_3, STREAK_7]")]
    public void B4_StreakThreshold_Value7_BothMatch()
    {
        var streak3 = MakeDef(10, BadgeTriggerType.StreakThreshold, BadgeRarity.Common, threshold: 3);
        var streak7 = MakeDef(11, BadgeTriggerType.StreakThreshold, BadgeRarity.Rare,   threshold: 7);
        var definitions = new[] { streak3, streak7 };
        var earned = new HashSet<int>();

        var result = BadgePredicateEvaluator.Match(
            BadgeTriggerType.StreakThreshold, value: 7, definitions, earned).ToList();

        result.Should().HaveCount(2, "streak=7 satisfies both STREAK_3 (3 ≤ 7) and STREAK_7 (7 ≤ 7)");
        result.Select(d => d.Code).Should().BeEquivalentTo(new[] { streak3.Code, streak7.Code });
    }

    // =========================================================================
    // B5 — StreakThreshold value=7, STREAK_3 already earned → only STREAK_7 returned
    // =========================================================================

    [Fact(DisplayName = "P405-B5 Match(StreakThreshold, value=7, [STREAK_3, STREAK_7], earned={STREAK_3.Id}) → [STREAK_7]")]
    public void B5_StreakThreshold_Value7_Streak3AlreadyEarned_OnlyStreak7Returned()
    {
        var streak3 = MakeDef(10, BadgeTriggerType.StreakThreshold, BadgeRarity.Common, threshold: 3);
        var streak7 = MakeDef(11, BadgeTriggerType.StreakThreshold, BadgeRarity.Rare,   threshold: 7);
        var definitions = new[] { streak3, streak7 };
        var earned = new HashSet<int> { streak3.Id }; // STREAK_3 already earned

        var result = BadgePredicateEvaluator.Match(
            BadgeTriggerType.StreakThreshold, value: 7, definitions, earned).ToList();

        result.Should().HaveCount(1, "STREAK_3 already earned → filtered out; STREAK_7 newly matched");
        result[0].Code.Should().Be(streak7.Code);
    }

    // =========================================================================
    // B6 — StreakThreshold value=2: no threshold reached → empty
    // =========================================================================

    [Fact(DisplayName = "P405-B6 Match(StreakThreshold, value=2, [STREAK_3, STREAK_7], earned=∅) → [] (no threshold crossed)")]
    public void B6_StreakThreshold_Value2_NoThresholdCrossed_ReturnsEmpty()
    {
        var streak3 = MakeDef(10, BadgeTriggerType.StreakThreshold, BadgeRarity.Common, threshold: 3);
        var streak7 = MakeDef(11, BadgeTriggerType.StreakThreshold, BadgeRarity.Rare,   threshold: 7);
        var definitions = new[] { streak3, streak7 };
        var earned = new HashSet<int>();

        var result = BadgePredicateEvaluator.Match(
            BadgeTriggerType.StreakThreshold, value: 2, definitions, earned).ToList();

        result.Should().BeEmpty("streak=2 is below STREAK_3 threshold (3) — no badge earned");
    }

    // =========================================================================
    // B7 — LevelThreshold value=5: only LEVEL_5 threshold crossed
    // =========================================================================

    [Fact(DisplayName = "P405-B7 Match(LevelThreshold, value=5, [LEVEL_5, LEVEL_10], earned=∅) → [LEVEL_5]")]
    public void B7_LevelThreshold_Value5_OnlyLevel5Matches()
    {
        var level5  = MakeDef(20, BadgeTriggerType.LevelThreshold, BadgeRarity.Common, threshold: 5);
        var level10 = MakeDef(21, BadgeTriggerType.LevelThreshold, BadgeRarity.Rare,   threshold: 10);
        var definitions = new[] { level5, level10 };
        var earned = new HashSet<int>();

        var result = BadgePredicateEvaluator.Match(
            BadgeTriggerType.LevelThreshold, value: 5, definitions, earned).ToList();

        result.Should().HaveCount(1, "level=5 satisfies LEVEL_5 (threshold 5 ≤ 5) but NOT LEVEL_10 (threshold 10 > 5)");
        result[0].Code.Should().Be(level5.Code);
    }

    // =========================================================================
    // B8 — LevelThreshold value=10: both LEVEL_5 and LEVEL_10 crossed
    // =========================================================================

    [Fact(DisplayName = "P405-B8 Match(LevelThreshold, value=10, [LEVEL_5, LEVEL_10], earned=∅) → [LEVEL_5, LEVEL_10]")]
    public void B8_LevelThreshold_Value10_BothMatch()
    {
        var level5  = MakeDef(20, BadgeTriggerType.LevelThreshold, BadgeRarity.Common, threshold: 5);
        var level10 = MakeDef(21, BadgeTriggerType.LevelThreshold, BadgeRarity.Rare,   threshold: 10);
        var definitions = new[] { level5, level10 };
        var earned = new HashSet<int>();

        var result = BadgePredicateEvaluator.Match(
            BadgeTriggerType.LevelThreshold, value: 10, definitions, earned).ToList();

        result.Should().HaveCount(2, "level=10 satisfies both LEVEL_5 (5 ≤ 10) and LEVEL_10 (10 ≤ 10)");
        result.Select(d => d.Code).Should().BeEquivalentTo(new[] { level5.Code, level10.Code });
    }

    // =========================================================================
    // B9 — LevelThreshold value=100: massive jump covers all 3 threshold badges
    // =========================================================================

    [Fact(DisplayName = "P405-B9 Match(LevelThreshold, value=100, [L5, L10, LEG50], earned=∅) → all 3")]
    public void B9_LevelThreshold_Value100_AllThreeMatch()
    {
        var level5    = MakeDef(20, BadgeTriggerType.LevelThreshold, BadgeRarity.Common,    threshold: 5);
        var level10   = MakeDef(21, BadgeTriggerType.LevelThreshold, BadgeRarity.Rare,      threshold: 10);
        var legendary = MakeDef(22, BadgeTriggerType.LevelThreshold, BadgeRarity.Legendary, threshold: 50);
        var definitions = new[] { level5, level10, legendary };
        var earned = new HashSet<int>();

        var result = BadgePredicateEvaluator.Match(
            BadgeTriggerType.LevelThreshold, value: 100, definitions, earned).ToList();

        result.Should().HaveCount(3, "level=100 satisfies all thresholds (5 ≤ 100, 10 ≤ 100, 50 ≤ 100) — massive jump");
        result.Select(d => d.Code)
            .Should().BeEquivalentTo(new[] { level5.Code, level10.Code, legendary.Code });
    }

    // =========================================================================
    // B10 — Wrong trigger type: LevelThreshold def passed to StreakThreshold query → filtered
    // =========================================================================

    [Fact(DisplayName = "P405-B10 Match(StreakThreshold, value=10, [LEVEL_5-def], earned=∅) → [] (wrong trigger type)")]
    public void B10_WrongTriggerType_FilteredOut_ReturnsEmpty()
    {
        // This definition is a LevelThreshold badge, but we're calling Match with StreakThreshold.
        var levelBadge = MakeDef(20, BadgeTriggerType.LevelThreshold, BadgeRarity.Common, threshold: 5);
        var definitions = new[] { levelBadge };
        var earned = new HashSet<int>();

        var result = BadgePredicateEvaluator.Match(
            BadgeTriggerType.StreakThreshold, value: 10, definitions, earned).ToList();

        result.Should().BeEmpty("def.TriggerType=LevelThreshold ≠ StreakThreshold → filtered out by the evaluator");
    }

    // =========================================================================
    // B11 — Empty definitions list → always returns empty
    // =========================================================================

    [Fact(DisplayName = "P405-B11 Match(FirstLesson, value=0, [], earned=∅) → [] (empty catalog)")]
    public void B11_EmptyDefinitions_ReturnsEmpty()
    {
        var result = BadgePredicateEvaluator.Match(
            BadgeTriggerType.FirstLesson, value: 0,
            definitions: Array.Empty<BadgeDefinition>(),
            alreadyEarned: new HashSet<int>()).ToList();

        result.Should().BeEmpty("no definitions in catalog → nothing to match");
    }

    // =========================================================================
    // B12 — StreakThreshold definition with null Threshold → safely skipped
    // =========================================================================

    [Fact(DisplayName = "P405-B12 Match(StreakThreshold, value=3, [STREAK_3 with Threshold=null], earned=∅) → [] (null threshold)")]
    public void B12_NullThreshold_SafelySkipped_ReturnsEmpty()
    {
        // A StreakThreshold badge with Threshold=null is a misconfigured catalog row.
        // The evaluator must NOT throw; it must safely skip the null-threshold definition.
        var malformed = MakeDef(10, BadgeTriggerType.StreakThreshold, BadgeRarity.Common, threshold: null);
        var definitions = new[] { malformed };
        var earned = new HashSet<int>();

        var result = BadgePredicateEvaluator.Match(
            BadgeTriggerType.StreakThreshold, value: 3, definitions, earned).ToList();

        result.Should().BeEmpty("null Threshold must be safely skipped — no exception, no match");
    }

    // =========================================================================
    // Bonus: BadgeRarityDto enum values must mirror BadgeRarity domain enum (D10)
    // =========================================================================

    [Fact(DisplayName = "P405-EnumDrift BadgeRarityDto integer values match BadgeRarity domain enum (Common=1, Rare=2, Epic=3, Legendary=4)")]
    public void EnumDrift_BadgeRarityDto_MatchesBadgeRarity()
    {
        // D10: BadgeRarityDto is a mirror of Gamification.Domain.Enums.BadgeRarity.
        // Both must have identical integer values so that casts between them are safe.
        using var assertionScope = new FluentAssertions.Execution.AssertionScope();

        ((int)Learnexia.Shared.Contracts.Gamification.BadgeRarityDto.Common)
            .Should().Be((int)BadgeRarity.Common,    "Common must map to 1 in both enums");

        ((int)Learnexia.Shared.Contracts.Gamification.BadgeRarityDto.Rare)
            .Should().Be((int)BadgeRarity.Rare,      "Rare must map to 2 in both enums");

        ((int)Learnexia.Shared.Contracts.Gamification.BadgeRarityDto.Epic)
            .Should().Be((int)BadgeRarity.Epic,      "Epic must map to 3 in both enums");

        ((int)Learnexia.Shared.Contracts.Gamification.BadgeRarityDto.Legendary)
            .Should().Be((int)BadgeRarity.Legendary, "Legendary must map to 4 in both enums");
    }
}
