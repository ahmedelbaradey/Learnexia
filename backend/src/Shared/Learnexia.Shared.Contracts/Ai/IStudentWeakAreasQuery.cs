namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// Cross-module seam: returns a student's weak curriculum areas for a given subject.
///
/// <para><strong>P3-09 placeholder:</strong> the real implementation ships with P3-09
/// (student mastery / weak-area computation). Until then the default registration is
/// <c>EmptyWeakAreasQuery</c> which returns an empty list — ensuring P3-03's graceful
/// degradation path (AC4) is exercised by default.</para>
///
/// <para>Implementations must be pure read-only; never mutate mastery state.</para>
/// </summary>
public interface IStudentWeakAreasQuery
{
    /// <summary>
    /// Returns the weak areas for <paramref name="studentId"/> in the given <paramref name="subject"/>.
    /// Returns an empty list (never null) when no weak areas are recorded or when the
    /// real implementation is not yet available (graceful degradation — AC4).
    /// </summary>
    Task<IReadOnlyList<WeakArea>> GetWeakAreasAsync(
        int studentId,
        Subject subject,
        CancellationToken ct = default);
}
