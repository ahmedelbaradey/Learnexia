using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Gamification.Infrastructure.Persistence.Seed;

/// <summary>
/// Idempotent upsert of the mission definition catalog (8 rows: 5 daily + 3 weekly).
/// Runs in ALL environments (missions are product-as-code catalog, not demo data — same
/// convention as <c>BadgeSeeder</c>). Registered as Scoped via <c>AddGamificationInfrastructure</c>;
/// wired into <c>GamificationModule.InitializeAsync</c> in Batch 4.
///
/// Idempotency: checks by <see cref="MissionDefinition.Code"/>. Inserts missing rows; drift-corrects
/// changed metadata (IconKey / TitleKey / Cadence / TargetType / Target / RewardXp / SortOrder) via
/// <see cref="MissionDefinition.UpdateMetadata"/>. Code is immutable once seeded.
///
/// System user id convention: 0 (same as <c>BadgeSeeder.SystemUserId</c> and <c>LearningSeeder.SystemUserId</c>).
/// </summary>
public sealed class MissionSeeder
{
    private readonly GamificationDbContext _db;
    private readonly ILoggerManager _logger;

    public MissionSeeder(GamificationDbContext db, ILoggerManager logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // (Code, IconKey, TitleKey, Cadence, TargetType, Target, RewardXp, SortOrder)
        var catalog = new[]
        {
            ("DAILY_3_LESSONS",      "icon.mission.daily_3_lessons",      "mission.daily_3_lessons.title",      MissionType.Daily,  MissionTargetType.CompleteLessons,  3, 30,  10),
            ("DAILY_10_CORRECT",     "icon.mission.daily_10_correct",     "mission.daily_10_correct.title",     MissionType.Daily,  MissionTargetType.CorrectAnswers,  10, 30,  20),
            ("DAILY_KEEP_STREAK",    "icon.mission.daily_keep_streak",    "mission.daily_keep_streak.title",    MissionType.Daily,  MissionTargetType.MaintainStreak,   1, 50,  30),
            ("DAILY_1_LESSON_EARLY", "icon.mission.daily_1_lesson_early", "mission.daily_1_lesson_early.title", MissionType.Daily,  MissionTargetType.CompleteLessons,  1, 20,  40),
            ("DAILY_20_CORRECT",     "icon.mission.daily_20_correct",     "mission.daily_20_correct.title",     MissionType.Daily,  MissionTargetType.CorrectAnswers,  20, 50,  50),
            ("WEEKLY_15_LESSONS",    "icon.mission.weekly_15_lessons",    "mission.weekly_15_lessons.title",    MissionType.Weekly, MissionTargetType.CompleteLessons, 15, 150, 110),
            ("WEEKLY_75_CORRECT",    "icon.mission.weekly_75_correct",    "mission.weekly_75_correct.title",    MissionType.Weekly, MissionTargetType.CorrectAnswers,  75, 150, 120),
            ("WEEKLY_5_DAY_STREAK",  "icon.mission.weekly_5_day_streak",  "mission.weekly_5_day_streak.title",  MissionType.Weekly, MissionTargetType.MaintainStreak,   5, 200, 130),
        };

        int inserted = 0, updated = 0;

        foreach (var (code, iconKey, titleKey, cadence, targetType, target, rewardXp, sortOrder) in catalog)
        {
            var existing = await _db.MissionDefinitions
                .FirstOrDefaultAsync(d => d.Code == code, ct);

            if (existing is null)
            {
                _db.MissionDefinitions.Add(
                    MissionDefinition.Create(code, iconKey, titleKey, cadence, targetType, target, rewardXp, sortOrder));
                inserted++;
            }
            else if (existing.IconKey != iconKey
                  || existing.TitleKey != titleKey
                  || existing.Cadence != cadence
                  || existing.TargetType != targetType
                  || existing.Target != target
                  || existing.RewardXp != rewardXp
                  || existing.SortOrder != sortOrder)
            {
                existing.UpdateMetadata(iconKey, titleKey, cadence, targetType, target, rewardXp, sortOrder);
                updated++;
            }
        }

        if (inserted > 0 || updated > 0)
        {
            await _db.SaveChangesAsync(0);
            _logger.LogInfo($"P4-06: MissionSeeder — inserted={inserted}, updated={updated}, total catalog={catalog.Length}.");
        }
        else
        {
            _logger.LogInfo($"P4-06: MissionSeeder — catalog already up-to-date ({catalog.Length} rows).");
        }
    }
}
