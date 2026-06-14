using Learnexia.Shared.Contracts.Ai;

namespace Learnexia.Modules.Ai.Application.PromptBuilder.Stubs;

/// <summary>
/// Default (stub) implementation of <see cref="ICurriculumContextQuery"/>.
/// Always returns an empty list — enables P3-03 to ship before P3-07 (RAG retrieval)
/// is implemented (AC4 graceful degradation).
///
/// <para>P3-07 overrides this registration with the real RAG-backed implementation.
/// The prompt builder handles empty chunk lists by omitting the context section entirely
/// (no empty-header artifact).</para>
/// </summary>
internal sealed class EmptyCurriculumContextQuery : ICurriculumContextQuery
{
    public Task<IReadOnlyList<CurriculumContextChunk>> GetChunksAsync(
        int skillId,
        int gradeId,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CurriculumContextChunk>>(Array.Empty<CurriculumContextChunk>());
}
