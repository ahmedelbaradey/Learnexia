using Learnexia.Modules.Learning.Domain.Services;

namespace Learnexia.Modules.Learning.Application.Services;

/// <summary>
/// In-process seam for per-student behavioral profile — consumable by other Learning module
/// features (P3-08 adaptivity engine, P3-03 prompt builder when built) without going through HTTP.
///
/// P3-03 CROSS-MODULE READ (deferred): the AI module's prompt builder needs
/// <see cref="GetProfile"/> to read the derived explanation style + weak areas.
/// Since AI and Learning are separate modules, this CANNOT be satisfied via this interface
/// directly when P3-03 is built — it would violate module isolation (CLAUDE rule 12).
/// When P3-03 is built, the correct approach is:
///   (a) A Shared.Contracts interface seam implemented here and consumed cross-module, OR
///   (b) An integration event / read-model exposed through Shared.Contracts.
/// This is flagged for the P3-03 story. Do NOT add a cross-module project reference here.
///
/// P3-08 SAME-MODULE READ: P3-08 (adaptivity engine, Learning module) can inject this
/// interface directly — it is within the same module boundary.
/// </summary>
public interface IStudentProfileService
{
    /// <summary>
    /// Fetches all answer signals for <paramref name="studentId"/> from the repository,
    /// builds a <see cref="StudentSignals"/> input, calls <see cref="StudentProfileEngine.Derive"/>,
    /// maps the result to a <see cref="Domain.Entities.StudentLearningProfile"/> entity, and
    /// upserts it. Should be called after the mastery upsert so derivation reads fresh mastery data.
    /// </summary>
    /// <param name="studentId">The student whose profile should be recomputed.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RecomputeProfile(int studentId, CancellationToken ct = default);

    /// <summary>
    /// Returns the current <see cref="DerivedProfile"/> for the student. If no profile row
    /// exists, returns cold-start defaults (DataPointCount = 0, Standard style, empty maps).
    /// Never throws — consumers should treat DataPointCount = 0 as "insufficient data".
    /// </summary>
    /// <param name="studentId">The student whose profile is requested.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<DerivedProfile> GetProfile(int studentId, CancellationToken ct = default);
}
