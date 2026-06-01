using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Gamification.Infrastructure.Queries;

/// <summary>
/// Implements <see cref="IStudentMissionsQuery"/> against <see cref="GamificationDbContext"/> — P4-06.
///
/// Resolves the student's <c>StudentXpProfile.Id</c> first, then:
///   1. Computes the current daily and weekly period keys via <see cref="MissionPeriodCalculator"/>.
///   2. Lazy-instantiates missions for any period that has no rows yet (D2 decision — AC4).
///      Race safety: the unique index <c>UX_StudentMissions_StudentXpProfileId_MissionDefinitionId_PeriodKey</c>
///      prevents duplicate instantiation under concurrent dashboard reads; the <see cref="DbUpdateException"/>
///      catch in <see cref="EnsureMissionsForPeriodAsync"/> treats a constraint violation as
///      "already instantiated by a concurrent request" and re-reads.
///   3. Projects the current-period missions to <see cref="MissionSummary"/> records.
///
/// Brand-new students (no profile row): returns sentinel <c>StudentMissionsSnapshot([], null)</c> —
/// never null. Mirrors the <see cref="IStudentBadgesQuery"/> sentinel-snapshot pattern (D2).
///
/// Registered in <c>AddGamificationInfrastructure()</c> as Scoped.
/// </summary>
public sealed class StudentMissionsQuery : IStudentMissionsQuery
{
    private readonly GamificationDbContext _db;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;

    public StudentMissionsQuery(
        GamificationDbContext db,
        ISystemClock clock,
        ILoggerManager logger)
    {
        _db     = db;
        _clock  = clock;
        _logger = logger;
    }

    public async Task<StudentMissionsSnapshot> GetByStudentIdAsync(
        int studentId, CancellationToken ct = default)
    {
        // ── 1. Resolve StudentXpProfile ───────────────────────────────────────────────────────
        var profile = await _db.StudentXpProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == studentId, ct);

        // Brand-new student (no profile row) — return sentinel; lazy instantiation skipped.
        if (profile is null)
            return new StudentMissionsSnapshot(Array.Empty<MissionSummary>(), null);

        // ── 2. Compute current-period keys ────────────────────────────────────────────────────
        var now = _clock.UtcNow.ToUniversalTime();

        var (dailyKey, dailyStart, dailyEnd) =
            MissionPeriodCalculator.GetCurrentPeriod(MissionType.Daily, now);
        var (weeklyKey, weeklyStart, weeklyEnd) =
            MissionPeriodCalculator.GetCurrentPeriod(MissionType.Weekly, now);

        // ── 3. Lazy-instantiate if period has no rows yet ─────────────────────────────────────
        await EnsureMissionsForPeriodAsync(
            profile.Id, MissionType.Daily, dailyKey, dailyStart, dailyEnd, ct);
        await EnsureMissionsForPeriodAsync(
            profile.Id, MissionType.Weekly, weeklyKey, weeklyStart, weeklyEnd, ct);

        // ── 4. Read the active rows for both periods ──────────────────────────────────────────
        var missions = await _db.StudentMissions
            .AsNoTracking()
            .Include(m => m.MissionDefinition)
            .Where(m => m.StudentXpProfileId == profile.Id
                     && (m.PeriodKey == dailyKey || m.PeriodKey == weeklyKey))
            .ToListAsync(ct);

        // ── 5. Project to snapshot ────────────────────────────────────────────────────────────
        var daily = missions
            .Where(m => m.MissionDefinition.Cadence == MissionType.Daily)
            .OrderBy(m => m.MissionDefinition.SortOrder)
            .ThenBy(m => m.MissionDefinition.Id)
            .Select(ToSummary)
            .ToList();

        var weekly = missions
            .Where(m => m.MissionDefinition.Cadence == MissionType.Weekly)
            .OrderBy(m => m.MissionDefinition.SortOrder)
            .ThenBy(m => m.MissionDefinition.Id)
            .Select(ToSummary)
            .FirstOrDefault();   // dashboard surface exposes 1 weekly mission summary (D4)

        return new StudentMissionsSnapshot(daily, weekly);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Checks whether any <see cref="StudentMission"/> rows exist for <paramref name="periodKey"/>.
    /// If none exist, loads the <see cref="MissionDefinition"/> catalog for the given
    /// <paramref name="cadence"/> and inserts one row per definition.
    ///
    /// Thread-safety: the unique index <c>UX_StudentMissions_StudentXpProfileId_MissionDefinitionId_PeriodKey</c>
    /// is the authoritative guard. A concurrent request that races here will receive a
    /// <see cref="DbUpdateException"/> with error code 23505 (Postgres UNIQUE violation) and treat
    /// it as "already instantiated" — harmless.
    /// </summary>
    private async Task EnsureMissionsForPeriodAsync(
        int studentXpProfileId,
        MissionType cadence,
        string periodKey,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken ct)
    {
        // Fast exit: if any rows exist for this period, skip instantiation.
        bool any = await _db.StudentMissions
            .AsNoTracking()
            .AnyAsync(m => m.StudentXpProfileId == studentXpProfileId
                        && m.PeriodKey == periodKey, ct);
        if (any) return;

        // Load definitions for this cadence.
        var definitions = await _db.MissionDefinitions
            .Where(d => d.Cadence == cadence)
            .ToListAsync(ct);

        if (definitions.Count == 0)
        {
            _logger.LogInfo(
                $"P4-06: StudentMissionsQuery — no {cadence} definitions in catalog; " +
                $"skipping instantiation for studentXpProfileId={studentXpProfileId}.");
            return;
        }

        // Load a tracked profile so EF can resolve the FK on StudentMission.StudentXpProfile.
        // This is a small overhead (1 row) but required for graph-navigation on AddAsync.
        var profile = await _db.StudentXpProfiles
            .FirstOrDefaultAsync(p => p.Id == studentXpProfileId, ct);
        if (profile is null) return;   // race: profile was deleted between the outer check and here

        foreach (var def in definitions)
        {
            var mission = StudentMission.Create(
                profile, def, periodKey, periodStartUtc, periodEndUtc);
            await _db.StudentMissions.AddAsync(mission, ct);
        }

        try
        {
            await _db.SaveChangesAsync(0);
            _logger.LogInfo(
                $"P4-06: lazy-instantiated {definitions.Count} {cadence} missions " +
                $"for studentXpProfileId={studentXpProfileId}, periodKey={periodKey}.");
        }
        catch (DbUpdateException dbEx)
            when (dbEx.InnerException?.Message?.Contains(
                      "UX_StudentMissions_StudentXpProfileId_MissionDefinitionId_PeriodKey") == true)
        {
            // Concurrent instantiation race: another request just instantiated these rows.
            // The unique index won; both reads will succeed — treat as already-instantiated.
            // Narrowed to the specific constraint name only (F2 security fix — bare "23505" was
            // too broad and would silently swallow unrelated unique-constraint violations).
            _logger.LogInfo(
                $"P4-06: concurrent lazy-instantiation race for " +
                $"studentXpProfileId={studentXpProfileId}, periodKey={periodKey} — " +
                $"treated as already-instantiated.");
        }
    }

    private static MissionSummary ToSummary(StudentMission m) => new(
        Code:          m.MissionDefinition.Code,
        IconKey:       m.MissionDefinition.IconKey,
        TitleKey:      m.MissionDefinition.TitleKey,
        TargetType:    (MissionTargetTypeDto)(int)m.MissionDefinition.TargetType,
        Progress:      m.Progress,
        Target:        m.Target,
        Status:        (MissionStatusDto)(int)m.Status,
        CompletedAtUtc: m.CompletedAtUtc);
}
