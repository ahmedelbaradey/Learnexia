using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Gamification.Infrastructure.Persistence.Seed;

/// <summary>
/// Idempotent SEED-IF-ABSENT of the mission definition catalog (11 rows: 5 daily + 3 weekly + 3 weekly challenges).
/// Runs in ALL environments (missions are product-as-code catalog, not demo data — same
/// convention as <c>BadgeSeeder</c>). Registered as Scoped via <c>AddGamificationInfrastructure</c>;
/// wired into <c>GamificationModule.InitializeAsync</c> in Batch 4.
///
/// P7-13 SEED-IF-ABSENT RULE: inserts rows that are missing-by-Code only.
/// Does NOT drift-correct / overwrite existing rows — admin edits take precedence (AC8).
///
/// P4-11 Batch 1-C: Three weekly challenge rows (CHALLENGE_* prefix) appended after the WEEKLY_* rows.
/// These reuse <see cref="MissionType.Weekly"/> cadence so the existing <c>MissionRolloverJob</c>
/// (cron "10 0 * * 1") and <c>IncrementMissionProgressCommandHandler</c> pick them up automatically.
/// FE differentiates challenge rows from classic weekly rows via <c>Code.StartsWith("CHALLENGE_")</c>.
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

            // ── P4-11 Batch 1-C: weekly challenge rows (CHALLENGE_* prefix) ──────────────
            // Higher RewardXp (300-500) signals "challenge" tier vs regular weekly (150-200).
            // MissionType.Weekly reused (Q3=reuse lock) so the rollover job + progress engine
            // picks them up with zero code change. FE discriminates by Code prefix.
            ("CHALLENGE_25_LESSONS_WEEK", "icon.mission.challenge_lessons", "mission.challenge_25_lessons.title", MissionType.Weekly, MissionTargetType.CompleteLessons, 25, 500, 140),
            ("CHALLENGE_100_CORRECT_WEEK", "icon.mission.challenge_correct", "mission.challenge_100_correct.title", MissionType.Weekly, MissionTargetType.CorrectAnswers, 100, 400, 150),
            ("CHALLENGE_5_DAY_NO_BREAK",  "icon.mission.challenge_streak",  "mission.challenge_5_day_streak.title", MissionType.Weekly, MissionTargetType.MaintainStreak,   5, 300, 160),
        };

        int inserted = 0;

        foreach (var (code, iconKey, titleKey, cadence, targetType, target, rewardXp, sortOrder) in catalog)
        {
            var existing = await _db.MissionDefinitions
                .FirstOrDefaultAsync(d => d.Code == code, ct);

            if (existing is null)
            {
                // P7-13 seed-if-absent: insert only when the row is missing; never overwrite.
                _db.MissionDefinitions.Add(
                    MissionDefinition.Create(code, iconKey, titleKey, cadence, targetType, target, rewardXp, sortOrder));
                inserted++;
            }
            // Existing rows are intentionally skipped — admin edits take precedence (AC8).
        }

        if (inserted > 0)
        {
            await _db.SaveChangesAsync(0);
            _logger.LogInfo($"P7-13/P4-11: MissionSeeder — inserted={inserted}, total catalog={catalog.Length}.");
        }
        else
        {
            _logger.LogInfo($"P7-13/P4-11: MissionSeeder — catalog already up-to-date ({catalog.Length} rows).");
        }
    }
}
