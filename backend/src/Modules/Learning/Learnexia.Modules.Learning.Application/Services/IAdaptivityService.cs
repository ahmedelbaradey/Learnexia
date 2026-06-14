using Learnexia.Modules.Learning.Domain.Services;

namespace Learnexia.Modules.Learning.Application.Services;

/// <summary>
/// In-process seam for per-skill adaptive difficulty — consumable by other Learning module features
/// (P3-11 quiz selection, P3-06 question generation) without going through HTTP.
///
/// AC4 (P3-08): this interface is the "exposed to P3-11 and P3-06 paths" seam.
/// P3-11 calls <see cref="GetTargetDifficulty"/> at quiz-build time to filter questions by difficulty.
/// P3-06 (AI module, not yet built) will call the same method once the AI module lands.
///
/// P3-13 optional fatigue hook: the implementation accepts an optional <c>fatigueSignal</c>
/// parameter (defaulted null) so P3-13 can wire in a fatigue value without a signature change.
/// </summary>
public interface IAdaptivityService
{
    /// <summary>
    /// Computes the target <see cref="AdaptivityDecision"/> for the given student and skill.
    /// </summary>
    /// <param name="studentId">The student whose difficulty is being computed.</param>
    /// <param name="skillId">
    ///     The skill to compute difficulty for. When <c>null</c>, returns a cold-start default
    ///     (Medium, IsDefault=true) without touching the database — safe for lessons with no skill.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    ///     An <see cref="AdaptivityDecision"/> with the recommended difficulty, an <c>IsDefault</c>
    ///     flag, and the raw score. Never throws — returns a default (Medium) on error paths.
    /// </returns>
    Task<AdaptivityDecision> GetTargetDifficulty(int studentId, int? skillId, CancellationToken ct = default);
}
