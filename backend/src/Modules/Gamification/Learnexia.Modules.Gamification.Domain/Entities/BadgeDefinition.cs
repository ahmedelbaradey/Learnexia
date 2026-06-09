using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Gamification.Domain.Entities;

/// <summary>
/// Badge catalog row. One row per badge type; rows are seeded by <c>BadgeSeeder</c>.
/// P7-13: Promoted to <see cref="AggregateRoot"/> so admin write handlers can raise
/// <c>AdminActionPerformedDomainEvent</c> post-commit via <c>UnitOfWorkBehavior</c> (ADR 0002).
///
/// <see cref="Code"/> is the stable string key (e.g., "STREAK_7") referenced by the badge
/// engine and by FE i18n bundles.
///
/// Name / Description live in backend as English fallbacks; FE i18n resolves the user-visible
/// text from <see cref="IconKey"/> (asset bundle key, e.g., "badge-flame-7").
/// </summary>
public class BadgeDefinition : AggregateRoot
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

    /// <summary>
    /// P7-13: Admin-controlled active/inactive toggle. Inactive badges are excluded from the earn
    /// engine. Does NOT retroactively strip already-earned <c>StudentBadge</c> rows (AC2).
    /// Default <c>true</c> — all existing rows backfill to active.
    /// </summary>
    public bool IsActive { get; private set; } = true;

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
    /// P7-13: Admin CRUD update — allows changing Name, Description, IconKey, Rarity,
    /// SortOrder, TriggerType, Threshold, and RewardXp. <see cref="Code"/> stays immutable.
    /// Supersedes the narrower <c>UpdateMetadata</c> used by the seeder.
    /// </summary>
    public void AdminUpdate(
        string name,
        string description,
        string iconKey,
        BadgeRarity rarity,
        int sortOrder,
        BadgeTriggerType triggerType,
        int? threshold,
        int rewardXp)
    {
        Name = name;
        Description = description;
        IconKey = iconKey;
        Rarity = rarity;
        SortOrder = sortOrder;
        TriggerType = triggerType;
        Threshold = threshold;
        RewardXp = rewardXp;
    }

    /// <summary>P7-13: Toggles <see cref="IsActive"/>. Idempotent.</summary>
    public void SetActive(bool isActive) => IsActive = isActive;

    /// <summary>
    /// Seeder-only: drift-correct mutable catalog metadata (IconKey / Rarity / SortOrder /
    /// TriggerType / Threshold / RewardXp). Name / Description / Code are immutable once seeded.
    /// P7-13 seed-if-absent rule: the seeder only calls this when the row was NOT admin-edited
    /// (i.e., we use seed-if-absent; this method is kept for backward-compat but the seeder
    /// no longer calls it on existing rows — see <c>BadgeSeeder</c>).
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
