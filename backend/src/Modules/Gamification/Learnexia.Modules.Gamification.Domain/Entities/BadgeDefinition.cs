using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Gamification.Domain.Entities;

/// <summary>
/// Badge catalog row. One row per badge type; rows are seeded by <c>BadgeSeeder</c>.
/// Derives from <see cref="FullAuditedEntity"/> so that admin (P7-03) can eventually
/// update metadata (icon, threshold, reward XP) with full audit trail.
///
/// <see cref="Code"/> is the stable string key (e.g., "STREAK_7") referenced by the badge
/// engine and by FE i18n bundles.
///
/// Name / Description live in backend as English fallbacks; FE i18n resolves the user-visible
/// text from <see cref="IconKey"/> (asset bundle key, e.g., "badge-flame-7").
/// </summary>
public class BadgeDefinition : FullAuditedEntity
{
    /// <summary>Stable, unique badge code — e.g. "FIRST_LESSON", "STREAK_7". Max 64 chars.</summary>
    public string Code { get; private set; } = null!;

    /// <summary>English display name. Kid-safe. Max 80 chars.</summary>
    public string Name { get; private set; } = null!;

    /// <summary>One-line achievement description. Max 240 chars.</summary>
    public string Description { get; private set; } = null!;

    /// <summary>FE asset bundle key — e.g. "badge-first-steps". Max 128 chars.</summary>
    public string IconKey { get; private set; } = null!;

    /// <summary>Badge rarity tier. Stored as int.</summary>
    public BadgeRarity Rarity { get; private set; }

    /// <summary>UI sort order within the rarity tier. Lower = first.</summary>
    public int SortOrder { get; private set; }

    /// <summary>Trigger type — drives dispatch in <c>BadgePredicateEvaluator</c>. Stored as int.</summary>
    public BadgeTriggerType TriggerType { get; private set; }

    /// <summary>Numeric threshold for StreakThreshold / LevelThreshold triggers. Null for FirstLesson.</summary>
    public int? Threshold { get; private set; }

    /// <summary>Rarity-scaled XP bonus awarded when the badge is earned (20/50/100/250).</summary>
    public int RewardXp { get; private set; }

    // ---------------------------------------------------------------------------
    // EF constructor
    // ---------------------------------------------------------------------------

    private BadgeDefinition() { }

    // ---------------------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------------------

    /// <summary>Creates a new badge definition (used by <c>BadgeCatalog.Defaults()</c>).</summary>
    public static BadgeDefinition Create(
        string code,
        string name,
        string description,
        string iconKey,
        BadgeRarity rarity,
        int sortOrder,
        BadgeTriggerType triggerType,
        int? threshold,
        int rewardXp)
        => new()
        {
            Code = code,
            Name = name,
            Description = description,
            IconKey = iconKey,
            Rarity = rarity,
            SortOrder = sortOrder,
            TriggerType = triggerType,
            Threshold = threshold,
            RewardXp = rewardXp,
        };

    // ---------------------------------------------------------------------------
    // Metadata update (for BadgeSeeder upsert on re-seed)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Allows <c>BadgeSeeder</c> to upsert mutable catalog metadata when the seeded definition
    /// has drifted (e.g., RewardXp changed for a rarity tier). Name / Description / Code are
    /// immutable once seeded; IconKey / Rarity / SortOrder / TriggerType / Threshold / RewardXp
    /// may change over time via a re-seed.
    /// </summary>
    public void UpdateMetadata(
        string iconKey,
        BadgeRarity rarity,
        int sortOrder,
        BadgeTriggerType triggerType,
        int? threshold,
        int rewardXp)
    {
        IconKey = iconKey;
        Rarity = rarity;
        SortOrder = sortOrder;
        TriggerType = triggerType;
        Threshold = threshold;
        RewardXp = rewardXp;
    }
}
