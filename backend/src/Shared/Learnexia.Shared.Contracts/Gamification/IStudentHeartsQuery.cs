namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Cross-module read seam: Gamification implements, Learning (and any future consumer) injects.
/// Mirrors <see cref="IStudentStreakQuery"/> line-for-line in shape — P4-04.
///
/// Returns a sentinel snapshot (full hearts) for brand-new students — never null (D8).
/// This avoids leaking <c>HeartsOptions.Cap</c> into the Learning module.
///
/// Registered in <c>AddGamificationInfrastructure()</c> as Scoped.
/// </summary>
public interface IStudentHeartsQuery
{
    /// <summary>
    /// Returns the hearts snapshot for <paramref name="studentId"/>.
    /// For brand-new students who have never had a heart-loss event, returns a sentinel
    /// snapshot with <c>Hearts = Cap = 5</c> and <c>InPracticeMode = false</c>.
    /// Never returns null — the sentinel guarantees callers always have a valid hearts value.
    /// </summary>
    Task<StudentHeartsSnapshot> GetByStudentIdAsync(int studentId, CancellationToken ct = default);
}

/// <summary>
/// Lightweight hearts projection crossing the module boundary (P4-04).
/// Contains only the fields that consumers need — no navigation properties, no PII.
/// <see cref="StudentId"/> is intentionally omitted (the consumer already has it — per F8 principle).
/// </summary>
/// <param name="Hearts">Current hearts count (0..Cap).</param>
/// <param name="Cap">Maximum hearts (e.g., 5). Allows the consumer to render a hearts bar without
/// coupling to <c>HeartsOptions</c>.</param>
/// <param name="LastHeartRefillAtUtc">Clock timestamp of the last refill tick; null when clock is idle (full hearts).</param>
/// <param name="NextRefillAtUtc">
/// Computed next refill time: <c>LastHeartRefillAtUtc + RefillInterval</c>.
/// Null when hearts are at cap or clock is idle (no pending refill).
/// </param>
/// <param name="InPracticeMode">True when <c>Hearts == 0</c> (derived from Hearts — not a stored column).</param>
public sealed record StudentHeartsSnapshot(
    int Hearts,
    int Cap,
    DateTime? LastHeartRefillAtUtc,
    DateTime? NextRefillAtUtc,
    bool InPracticeMode);
