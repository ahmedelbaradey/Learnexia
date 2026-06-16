namespace Learnexia.Modules.Learning.Application.Abstractions;

/// <summary>
/// Service seam for student progress management operations (P8-04).
/// All EF queries and DeleteRange staging are encapsulated here — Application handlers
/// inject this interface only (Option C). No IQueryable / DbSet / EF extension methods
/// cross this boundary.
/// </summary>
public interface IProgressService
{
    /// <summary>
    /// Performs the two-step query-then-stage to reset all Math/Science Attempt rows
    /// for the specified student:
    ///   Step A — Collects Lesson IDs whose Subject.SubjectCode is MATH or SCIENCE.
    ///   Step B — Loads and stages a DeleteRange on Attempt rows matching (studentId, Math/Science lessons).
    /// StudentAnswer rows cascade via DeleteBehavior.Cascade (no explicit delete needed).
    /// Stages only — UnitOfWorkBehavior owns the single commit (ADR 0001).
    /// Returns <c>HadCurriculumLessons</c> (false → no Math/Science lessons exist in the curriculum
    /// at all) and <c>DeletedAttempts</c> (count staged for deletion; 0 with HadCurriculumLessons=true
    /// → the student simply had no Math/Science attempts). The caller uses the pair to distinguish the
    /// two idempotent no-op cases in diagnostics; both yield the same success/DB outcome.
    /// </summary>
    Task<(bool HadCurriculumLessons, int DeletedAttempts)> ResetMathScienceProgressAsync(int studentId, CancellationToken ct = default);
}
