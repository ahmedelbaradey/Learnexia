namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// Cross-module seam: retrieves curriculum context chunks for a given skill and grade.
///
/// <para><strong>P3-07 placeholder:</strong> the RAG retrieval implementation ships in P3-07.
/// Until then the default registration is <c>EmptyCurriculumContextQuery</c>, which returns
/// an empty list — ensuring P3-03's graceful degradation path (AC4) is exercised by default.</para>
///
/// <para>Note: when <c>ILearningContextProvider</c> (BE-9/BE-10) is active, callers use that
/// seam instead (it is richer and intent-aware). This interface remains for future fallback
/// scenarios and lower-level callers that don't need the full <c>LearningContext</c> shape.</para>
/// </summary>
public interface ICurriculumContextQuery
{
    /// <summary>
    /// Returns approved curriculum chunks for the given skill and grade.
    /// Returns an empty list (never null) when no content is available (graceful degradation — AC4).
    /// </summary>
    Task<IReadOnlyList<CurriculumContextChunk>> GetChunksAsync(
        int skillId,
        int gradeId,
        CancellationToken ct = default);
}

/// <summary>
/// A single curriculum text chunk used as RAG context in the assembled prompt.
/// </summary>
/// <param name="ChunkId">Opaque identifier (e.g. curriculum node id).</param>
/// <param name="Text">The approved curriculum text for this chunk.</param>
public sealed record CurriculumContextChunk(string ChunkId, string Text);
