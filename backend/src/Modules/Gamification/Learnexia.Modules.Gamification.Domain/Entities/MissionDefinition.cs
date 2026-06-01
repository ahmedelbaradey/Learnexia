using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Gamification.Domain.Entities;

/// <summary>
/// Mission catalog row. One row per mission template; rows are seeded by <c>MissionSeeder</c>.
/// Derives from <see cref="FullAuditedEntity"/> so that admin (P7) can eventually update
/// metadata (icon, target, reward XP) with full audit trail. Mirrors <see cref="BadgeDefinition"/>.
///
/// <see cref="Code"/> is the stable string key (e.g., "DAILY_3_LESSONS") referenced by the
/// mission engine and by FE i18n bundles.
///
/// <see cref="TitleKey"/> is an i18n key for the FE (e.g., "mission.daily_3_lessons.title").
/// <see cref="IconKey"/> is an FE asset bundle key (e.g., "mission-book-3").
/// </summary>
public class MissionDefinition : FullAuditedEntity
{
    /// <summary>Stable, unique mission code — e.g. "DAILY_3_LESSONS". Max 40 chars.</summary>
    public string Code { get; private set; } = null!;

    /// <summary>FE asset bundle key — e.g. "mission-book-3". Max 60 chars.</summary>
    public string IconKey { get; private set; } = null!;

    /// <summary>FE i18n title key — e.g. "mission.daily_3_lessons.title". Max 80 chars.</summary>
    public string TitleKey { get; private set; } = null!;

    /// <summary>Mission cadence: Daily or Weekly. Stored as int.</summary>
    public MissionType Cadence { get; private set; }

    /// <summary>What the student must do to progress this mission. Stored as int.</summary>
    public MissionTargetType TargetType { get; private set; }

    /// <summary>The integer completion threshold (e.g., 3 for "complete 3 lessons").</summary>
    public int Target { get; private set; }

    /// <summary>XP awarded to the student upon completing this mission.</summary>
    public int RewardXp { get; private set; }

    /// <summary>UI sort order within cadence tier. Lower = first.</summary>
    public int SortOrder { get; private set; }

    // ---------------------------------------------------------------------------
    // EF constructor
    // ---------------------------------------------------------------------------

    private MissionDefinition() { }

    // ---------------------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------------------

    /// <summary>Creates a new mission definition (used by <c>MissionCatalog.Defaults()</c>).</summary>
    public static MissionDefinition Create(
        string code,
        string iconKey,
        string titleKey,
        MissionType cadence,
        MissionTargetType targetType,
        int target,
        int rewardXp,
        int sortOrder = 0)
        => new()
        {
            Code = code,
            IconKey = iconKey,
            TitleKey = titleKey,
            Cadence = cadence,
            TargetType = targetType,
            Target = target,
            RewardXp = rewardXp,
            SortOrder = sortOrder,
        };

    // ---------------------------------------------------------------------------
    // Metadata update (for MissionSeeder upsert on re-seed)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Allows <c>MissionSeeder</c> to upsert mutable catalog metadata when the seeded definition
    /// has drifted (e.g., RewardXp or Target changed). Code is immutable once seeded; other fields
    /// may change via a re-seed. Mirrors <see cref="BadgeDefinition.UpdateMetadata"/>.
    /// </summary>
    public void UpdateMetadata(
        string iconKey,
        string titleKey,
        MissionType cadence,
        MissionTargetType targetType,
        int target,
        int rewardXp,
        int sortOrder)
    {
        IconKey = iconKey;
        TitleKey = titleKey;
        Cadence = cadence;
        TargetType = targetType;
        Target = target;
        RewardXp = rewardXp;
        SortOrder = sortOrder;
    }
}
