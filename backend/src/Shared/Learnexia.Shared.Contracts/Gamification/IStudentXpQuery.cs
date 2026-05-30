namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Cross-module read seam: Gamification implements, Learning and any future consumer injects.
/// Mirrors <see cref="Parent.IParentChildQuery"/>.
///
/// Registered in <c>AddGamificationApplication()</c> as Scoped.
/// Future: P4-10 swaps the implementation for a Redis-backed one behind this same seam without
/// changing any consumer (Open-Closed for the dashboard handler).
///
/// Returns <c>null</c> when no profile exists yet (brand-new student who has never earned XP).
/// Callers default to <c>(XpTotal=0, CurrentLevel=1)</c> on null — the student sees a clean L1
/// dashboard rather than a 404.
/// </summary>
public interface IStudentXpQuery
{
    /// <summary>
    /// Returns the XP snapshot for <paramref name="studentId"/>, or <c>null</c> if no profile
    /// exists yet (brand-new student).
    /// </summary>
    Task<StudentXpSnapshot?> GetByStudentIdAsync(int studentId, CancellationToken ct = default);
}

/// <summary>
/// Lightweight XP projection crossing the module boundary.
/// Contains only the fields that consumers need — no navigation properties, no PII.
/// </summary>
public record StudentXpSnapshot(int StudentId, int TotalXp, int CurrentLevel);
