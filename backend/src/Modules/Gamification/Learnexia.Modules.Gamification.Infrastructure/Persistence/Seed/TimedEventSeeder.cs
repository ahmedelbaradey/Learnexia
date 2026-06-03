using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Gamification.Infrastructure.Persistence.Seed;

/// <summary>
/// Idempotent upsert of the timed-event catalog demo rows (P4-11-B1-B).
///
/// Seeds ONE demo event (<c>WELCOME_BOOST</c>) for testing and dev validation. This row is a
/// non-destructive example; ops can deactivate it by setting <c>EndUtc</c> to a past date via DB.
/// Production events are created via the P7 admin editor or direct SQL — no code change required.
///
/// Idempotency: checks by <c>Code</c>. Inserts missing rows only — does NOT drift-correct start/end
/// times (those are mutable op state, not product-as-code catalog metadata). Mirrors
/// <see cref="BadgeSeeder"/> / <see cref="MissionSeeder"/> shape exactly.
///
/// System user id convention: 0 (same as all other seeders).
/// </summary>
public sealed class TimedEventSeeder
{
    private readonly GamificationDbContext _db;
    private readonly ILoggerManager _logger;

    public TimedEventSeeder(GamificationDbContext db, ILoggerManager logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // Demo event: starts 30 days from seed time, runs for 7 days, AllXp ×1.5.
        // StartUtc is intentionally set 30 days ahead so the event is not immediately active on
        // deploy — ops must adjust StartUtc via direct SQL to schedule the actual boost window.
        // Setting StartUtc=now would activate the event within 2 minutes (TimedEventSweepJob cron),
        // which pollutes integration tests and production metrics. Ops can edit via direct SQL without
        // a code deploy. This row is a template only.
        var nowUtc = DateTime.UtcNow;

        var catalog = new[]
        {
            (
                Code:          "WELCOME_BOOST",
                NameEn:        "Welcome XP Boost",
                NameAr:        "مكافأة نقاط الترحيب",
                DescriptionEn: (string?)"Earn 1.5× XP on everything this week!",
                DescriptionAr: (string?)"احصل على 1.5× نقاط الخبرة على كل شيء هذا الأسبوع!",
                StartUtc:      nowUtc.AddDays(30),
                EndUtc:        nowUtc.AddDays(37),
                Multiplier:    1.5m,
                Scope:         TimedEventScope.AllXp
            ),
        };

        int inserted = 0;

        foreach (var row in catalog)
        {
            var existing = await _db.TimedEvents
                .FirstOrDefaultAsync(e => e.Code == row.Code, ct);

            if (existing is null)
            {
                _db.TimedEvents.Add(TimedEvent.Create(
                    code:          row.Code,
                    nameEn:        row.NameEn,
                    nameAr:        row.NameAr,
                    descriptionEn: row.DescriptionEn,
                    descriptionAr: row.DescriptionAr,
                    startUtc:      row.StartUtc,
                    endUtc:        row.EndUtc,
                    multiplier:    row.Multiplier,
                    scope:         row.Scope));
                inserted++;
            }
            // No drift-correction for timed events — start/end are operational state, not catalog metadata.
        }

        if (inserted > 0)
        {
            await _db.SaveChangesAsync(0);
            _logger.LogInfo($"P4-11: TimedEventSeeder — inserted={inserted}, total catalog={catalog.Length}.");
        }
        else
        {
            _logger.LogInfo($"P4-11: TimedEventSeeder — catalog already up-to-date ({catalog.Length} rows).");
        }
    }
}
