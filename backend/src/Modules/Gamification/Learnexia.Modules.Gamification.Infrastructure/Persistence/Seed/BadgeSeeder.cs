using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Application.Common;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Gamification.Infrastructure.Persistence.Seed;

/// <summary>
/// Idempotent upsert of the badge definition catalog (~10 rows).
/// Runs in ALL environments (badges are product-as-code catalog, not demo data — unlike
/// <c>LearningSeeder</c> which is Development-only). Registered as Scoped via
/// <c>AddGamificationInfrastructure()</c>; wired into <c>GamificationModule.InitializeAsync</c>
/// in Batch 4.
///
/// Idempotency: checks by <see cref="BadgeDefinition.Code"/>. Inserts missing rows; drift-corrects
/// changed metadata (IconKey / Rarity / SortOrder / TriggerType / Threshold / RewardXp) via
/// <see cref="BadgeDefinition.UpdateMetadata"/>. Code / Name / Description are immutable once seeded.
///
/// System user id convention: 0 (same as <c>LearningSeeder.SystemUserId</c>).
/// </summary>
public sealed class BadgeSeeder
{
    private readonly GamificationDbContext _db;
    private readonly ILoggerManager _logger;

    public BadgeSeeder(GamificationDbContext db, ILoggerManager logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // (Code, Name, Description, IconKey, Rarity, SortOrder, TriggerType, Threshold, RewardXp)
        var catalog = new[]
        {
            ("FIRST_LESSON",  "First Steps",         "Complete your first lesson",         "icon.first_lesson",  BadgeRarity.Common,    10, BadgeTriggerType.FirstLesson,     (int?)null, GamificationConstants.XpRewards.BadgeEarned.Common),
            ("STREAK_3",      "Warming Up",          "Maintain a 3-day streak",            "icon.streak_3",      BadgeRarity.Common,    20, BadgeTriggerType.StreakThreshold,  3,          GamificationConstants.XpRewards.BadgeEarned.Common),
            ("STREAK_7",      "On Fire",             "Maintain a 7-day streak",            "icon.streak_7",      BadgeRarity.Rare,      30, BadgeTriggerType.StreakThreshold,  7,          GamificationConstants.XpRewards.BadgeEarned.Rare),
            ("STREAK_14",     "Two-Week Champion",   "Maintain a 14-day streak",           "icon.streak_14",     BadgeRarity.Rare,      31, BadgeTriggerType.StreakThreshold,  14,         GamificationConstants.XpRewards.BadgeEarned.Rare),
            ("STREAK_30",     "Unstoppable",         "Maintain a 30-day streak",           "icon.streak_30",     BadgeRarity.Epic,      32, BadgeTriggerType.StreakThreshold,  30,         GamificationConstants.XpRewards.BadgeEarned.Epic),
            ("LEVEL_5",       "Rookie",              "Reach Level 5",                      "icon.level_5",       BadgeRarity.Common,    40, BadgeTriggerType.LevelThreshold,   5,          GamificationConstants.XpRewards.BadgeEarned.Common),
            ("LEVEL_10",      "Scholar",             "Reach Level 10",                     "icon.level_10",      BadgeRarity.Rare,      41, BadgeTriggerType.LevelThreshold,   10,         GamificationConstants.XpRewards.BadgeEarned.Rare),
            ("LEVEL_20",      "Master",              "Reach Level 20",                     "icon.level_20",      BadgeRarity.Epic,      42, BadgeTriggerType.LevelThreshold,   20,         GamificationConstants.XpRewards.BadgeEarned.Epic),
            ("LEGENDARY_50",  "Legend",              "Reach Level 50",                     "icon.legendary_50",  BadgeRarity.Legendary, 50, BadgeTriggerType.LevelThreshold,   50,         GamificationConstants.XpRewards.BadgeEarned.Legendary),
            ("STREAK_100",    "Centurion",           "Maintain a 100-day streak",          "icon.streak_100",    BadgeRarity.Legendary, 33, BadgeTriggerType.StreakThreshold,  100,        GamificationConstants.XpRewards.BadgeEarned.Legendary),
        };

        int inserted = 0, updated = 0;

        foreach (var (code, name, description, iconKey, rarity, sortOrder, triggerType, threshold, rewardXp) in catalog)
        {
            var existing = await _db.BadgeDefinitions
                .FirstOrDefaultAsync(d => d.Code == code, ct);

            if (existing is null)
            {
                var def = BadgeDefinition.Create(code, name, description, iconKey, rarity, sortOrder, triggerType, threshold, rewardXp);
                _db.BadgeDefinitions.Add(def);
                inserted++;
            }
            else
            {
                // Drift-correct: if any mutable catalog metadata differs, upsert it.
                if (existing.IconKey != iconKey
                 || existing.Rarity != rarity
                 || existing.SortOrder != sortOrder
                 || existing.TriggerType != triggerType
                 || existing.Threshold != threshold
                 || existing.RewardXp != rewardXp)
                {
                    existing.UpdateMetadata(iconKey, rarity, sortOrder, triggerType, threshold, rewardXp);
                    updated++;
                }
            }
        }

        if (inserted > 0 || updated > 0)
        {
            await _db.SaveChangesAsync(0);
            _logger.LogInfo($"P4-05: BadgeSeeder — inserted={inserted}, updated={updated}, total catalog={catalog.Length}.");
        }
        else
        {
            _logger.LogInfo($"P4-05: BadgeSeeder — catalog already up-to-date ({catalog.Length} rows).");
        }
    }
}
