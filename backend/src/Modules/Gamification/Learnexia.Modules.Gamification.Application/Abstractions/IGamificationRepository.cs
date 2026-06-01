using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;

namespace Learnexia.Modules.Gamification.Application.Abstractions;

/// <summary>
/// Gamification module repository surface. Deferred-commit — write methods stage changes only;
/// <see cref="SaveChangesAsync"/> is called by <c>UnitOfWorkBehavior</c> after the handler returns.
/// Mirrors <c>ILearningRepository</c> shape (ADR 0001).
///
/// No <c>UpdateAsync</c> / <c>RemoveAsync</c> for <see cref="XpAward"/> — the ledger is append-only (AC2).
/// </summary>
public interface IGamificationRepository
{
    // ─────────────────────────── Badges (P4-05) ───────────────────────────

    /// <summary>Returns all catalog rows (no active filter — all are active in P4-05). Uses AsNoTracking.</summary>
    Task<List<BadgeDefinition>> GetAllBadgeDefinitionsAsync(CancellationToken ct = default);

    /// <summary>Returns catalog rows for one trigger type (used by notification handlers). Uses AsNoTracking.</summary>
    Task<List<BadgeDefinition>> GetBadgeDefinitionsByTriggerAsync(BadgeTriggerType triggerType, CancellationToken ct = default);

    /// <summary>Returns earned BadgeDefinitionIds for a student (idempotency pre-check). Uses AsNoTracking.</summary>
    Task<HashSet<int>> GetEarnedBadgeIdsAsync(int studentId, CancellationToken ct = default);

    /// <summary>Idempotency check: returns true when the profile already holds a badge for the definition.</summary>
    Task<bool> HasBadgeAsync(int studentXpProfileId, int badgeDefinitionId, CancellationToken ct = default);

    /// <summary>Stages a new <see cref="StudentBadge"/> for insertion. Does NOT save — UoW commits.</summary>
    Task AddStudentBadgeAsync(StudentBadge badge, CancellationToken ct = default);

    /// <summary>Returns all earned <see cref="StudentBadge"/> rows for the student, including <see cref="BadgeDefinition"/> navigation. Used by BadgesController.GetMine.</summary>
    Task<List<StudentBadge>> GetStudentBadgesAsync(int studentId, CancellationToken ct = default);

    /// <summary>Returns the N most recently earned <see cref="StudentBadge"/> rows for the student (ordered by AwardedAtUtc DESC), including <see cref="BadgeDefinition"/> navigation. Used by IStudentBadgesQuery dashboard recent-3 strip.</summary>
    Task<List<StudentBadge>> GetRecentStudentBadgesAsync(int studentId, int take, CancellationToken ct = default);

    // ─────────────────────────── Seeder (P4-05) ───────────────────────────

    /// <summary>Returns a single catalog row by Code. Used by <c>BadgeSeeder</c> upsert. Uses AsNoTracking.</summary>
    Task<BadgeDefinition?> GetBadgeDefinitionByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>Stages a new <see cref="BadgeDefinition"/> for insertion. Does NOT save — seeder calls SaveChangesAsync directly.</summary>
    Task AddBadgeDefinitionAsync(BadgeDefinition definition, CancellationToken ct = default);

    /// <summary>
    /// Attaches an existing (untracked) <see cref="BadgeDefinition"/> to the current DbContext
    /// as <c>Unchanged</c> so that adding a <see cref="StudentBadge"/> with the navigation property
    /// set does NOT cause EF to attempt a duplicate INSERT. Call this before <see cref="AddStudentBadgeAsync"/>.
    /// No-op if the entity is already tracked.
    /// </summary>
    void AttachBadgeDefinition(BadgeDefinition definition);
    /// <summary>
    /// Returns the XP profile for <paramref name="studentId"/>, or <c>null</c> if no profile exists yet
    /// (brand-new student). The returned entity is change-tracked so <c>ApplyAward</c> mutations
    /// are picked up by the UoW before commit.
    /// </summary>
    Task<StudentXpProfile?> GetProfileByStudentIdAsync(int studentId, CancellationToken ct = default);

    /// <summary>
    /// Acquires a <c>SELECT ... FOR UPDATE</c> row-lock on the profile row for
    /// <paramref name="studentId"/> within the current transaction (Q7 concurrency strategy).
    /// A concurrent writer blocks until the lock holder commits, preventing lost-update of TotalXp.
    /// No-op when no row exists yet — Postgres FOR UPDATE skips missing rows.
    /// Must be called inside an active transaction (i.e., inside <c>UnitOfWorkBehavior</c>).
    /// </summary>
    Task AcquireProfileLockAsync(int studentId, CancellationToken ct = default);

    /// <summary>
    /// Returns <c>true</c> when an <see cref="XpAward"/> with the given
    /// (<paramref name="originEventId"/>, <paramref name="reason"/>) already exists.
    /// Fast idempotency pre-check (AC4 happy path). Uses <c>AsNoTracking</c>.
    /// </summary>
    Task<bool> HasXpAwardAsync(Guid originEventId, XpReason reason, CancellationToken ct = default);

    /// <summary>
    /// Stages a new <see cref="XpAward"/> for insertion. Does NOT save — UoW commits.
    /// </summary>
    Task AddXpAwardAsync(XpAward award, CancellationToken ct = default);

    /// <summary>
    /// Stages the <see cref="StudentXpProfile"/> for insert (new) or update (existing).
    /// Discriminated by <c>profile.Id == 0</c> (new) vs non-zero (existing). Does NOT save — UoW commits.
    /// </summary>
    void UpsertXpProfile(StudentXpProfile profile);

    /// <summary>
    /// Persists all staged changes. Called exclusively by <c>UnitOfWorkBehavior</c> (for commands)
    /// and by <c>StreakSweepJob</c> (directly, since the job is not a MediatR command).
    /// Mirrors <c>ILearningRepository.SaveChangesAsync</c>.
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns up to 1 000 <see cref="StudentXpProfile"/> rows where
    /// <c>CurrentStreak &gt; 0 AND LastActivityDateUtc &lt; threshold</c>.
    /// Used exclusively by <c>StreakSweepJob</c> to identify students whose streaks have broken
    /// while they were silent (lazy-detection complement). Batched — caller loops while count == 1000.
    /// </summary>
    Task<List<StudentXpProfile>> GetBrokenProfilesAsync(DateOnly threshold, CancellationToken ct = default);

    // ─────── Missions (P4-06) ───────

    /// <summary>Returns all mission catalog rows. Uses AsNoTracking.</summary>
    Task<List<MissionDefinition>> GetAllMissionDefinitionsAsync(CancellationToken ct = default);

    /// <summary>Returns catalog rows for one cadence (Daily or Weekly). Uses AsNoTracking.</summary>
    Task<List<MissionDefinition>> GetMissionDefinitionsByCadenceAsync(MissionType cadence, CancellationToken ct = default);

    /// <summary>Returns a single catalog row by Code (used by <c>MissionSeeder</c> upsert). Uses AsNoTracking.</summary>
    Task<MissionDefinition?> GetMissionDefinitionByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>Stages a new <see cref="MissionDefinition"/> for insertion. Does NOT save — seeder calls SaveChangesAsync directly.</summary>
    Task AddMissionDefinitionAsync(MissionDefinition definition, CancellationToken ct = default);

    /// <summary>
    /// Attaches an existing (untracked) <see cref="MissionDefinition"/> to the current DbContext
    /// as <c>Unchanged</c> so that adding a <see cref="StudentMission"/> with the navigation property
    /// set does NOT cause EF to attempt a duplicate INSERT. Call before <see cref="AddStudentMissionAsync"/>.
    /// No-op if the entity is already tracked. Mirrors <see cref="AttachBadgeDefinition"/>.
    /// </summary>
    void AttachMissionDefinition(MissionDefinition definition);

    /// <summary>
    /// Returns all active (NotStarted or InProgress) <see cref="StudentMission"/> rows for a student
    /// whose period has not yet ended, ordered by SortOrder. Includes the <see cref="MissionDefinition"/>
    /// navigation. Used by the dashboard lazy-instantiation query.
    /// </summary>
    Task<List<StudentMission>> GetActiveMissionsForStudentAsync(int studentId, CancellationToken ct = default);

    /// <summary>
    /// Returns active (NotStarted or InProgress) <see cref="StudentMission"/> rows for a student
    /// filtered by <paramref name="targetType"/>. Includes the <see cref="MissionDefinition"/>
    /// navigation. Used by notification handlers to find which missions to increment.
    /// </summary>
    Task<List<StudentMission>> GetActiveMissionsForStudentByTargetTypeAsync(
        int studentId, MissionTargetType targetType, CancellationToken ct = default);

    /// <summary>
    /// Returns all <see cref="StudentMission"/> rows for a student in the given period (by PeriodKey).
    /// Uses AsNoTracking. Used by <c>StudentMissionsQuery</c> lazy-instantiation check.
    /// </summary>
    Task<List<StudentMission>> GetMissionsForStudentInPeriodAsync(
        int studentId, string periodKey, CancellationToken ct = default);

    /// <summary>Returns a single <see cref="StudentMission"/> by its PK. Includes the <see cref="MissionDefinition"/> navigation. Change-tracked for handler mutations.</summary>
    Task<StudentMission?> GetMissionByIdAsync(int studentMissionId, CancellationToken ct = default);

    /// <summary>Stages a new <see cref="StudentMission"/> for insertion. Does NOT save — UoW commits.</summary>
    Task AddStudentMissionAsync(StudentMission mission, CancellationToken ct = default);

    /// <summary>
    /// Attaches an existing (AsNoTracking) <see cref="StudentMission"/> to the current DbContext
    /// as <c>Modified</c> so that handler mutations on <c>Progress</c>, <c>Status</c>, and
    /// <c>CompletedAtUtc</c> (all <c>internal set</c>) are picked up by the UoW before commit.
    /// No-op if the entity is already tracked. Mirrors <see cref="AttachBadgeDefinition"/>.
    /// </summary>
    void AttachStudentMission(StudentMission mission);

    /// <summary>Idempotency pre-check: returns true when a <see cref="MissionProgressLog"/> row already exists for (studentMissionId, originEventId). Uses AsNoTracking.</summary>
    Task<bool> HasMissionProgressLogAsync(int studentMissionId, Guid originEventId, CancellationToken ct = default);

    /// <summary>Stages a new <see cref="MissionProgressLog"/> row for insertion. Does NOT save — UoW commits.</summary>
    Task AddMissionProgressLogAsync(MissionProgressLog log, CancellationToken ct = default);

    // ---------------------------------------------------------------------------
    // Hearts (P4-04-B2a-4)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Returns <c>true</c> when a <see cref="HeartLoss"/> with the given
    /// <paramref name="originEventId"/> already exists (idempotency pre-check).
    /// Fast check. Uses <c>AsNoTracking</c>.
    /// </summary>
    Task<bool> HasHeartLossAsync(Guid originEventId, CancellationToken ct = default);

    /// <summary>
    /// Stages a new <see cref="HeartLoss"/> row for insertion. Does NOT save — UoW commits.
    /// </summary>
    Task AddHeartLossAsync(HeartLoss heartLoss, CancellationToken ct = default);
}
