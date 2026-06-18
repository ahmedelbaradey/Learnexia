namespace Learnexia.Shared.Contracts.Identity;

/// <summary>
/// Cross-module read seam: Identity implements, Learning's daily recommendation job injects.
/// Returns the grade (int) for a given student user.
///
/// <para>This seam exists because the <c>RecommendationRecomputeJob</c> runs as a background
/// Hangfire job with NO JWT / HttpContext. The child's grade lives in the Identity module's
/// <c>User.Grade</c> column and must be read without a cross-module FK or direct project
/// reference.</para>
///
/// <para>Sentinel contract: <em>never throws</em>. Returns <c>null</c> when the student user
/// is not found or has no grade assigned (null Grade in the User row). Callers degrade
/// gracefully on null (use a default grade scope or skip the student).</para>
///
/// <para>Registered in <c>Identity.Infrastructure/DependencyInjection.cs</c> as Scoped
/// (mirrors <see cref="IUserLookup"/> registration pattern).</para>
///
/// <para>P5-09-BE-3a.</para>
/// </summary>
public interface IChildGradeQuery
{
    /// <summary>
    /// Returns the grade of the student identified by <paramref name="studentId"/>.
    /// </summary>
    /// <param name="studentId">
    /// Loose int reference to the Identity student user (no cross-module FK).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The student's grade as a nullable int (1–6 in the product, extensible).
    /// Returns <c>null</c> when the student is not found or has no grade assigned.
    /// Never throws.
    /// </returns>
    Task<int?> GetGradeAsync(int studentId, CancellationToken ct = default);
}
