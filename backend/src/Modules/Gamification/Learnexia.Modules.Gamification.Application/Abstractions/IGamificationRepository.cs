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
    /// Persists all staged changes. Called exclusively by <c>UnitOfWorkBehavior</c>.
    /// Mirrors <c>ILearningRepository.SaveChangesAsync</c>.
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
