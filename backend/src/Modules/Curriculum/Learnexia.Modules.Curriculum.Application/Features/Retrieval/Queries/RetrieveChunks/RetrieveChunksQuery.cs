using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Curriculum.Application.Features.Retrieval.Queries.RetrieveChunks;

/// <summary>
/// Retrieves the top-k most semantically relevant curriculum chunks for a given query text,
/// filtered by grade, subject, and optionally skill (weak-area filter).
///
/// <para>This is a <see cref="IQuery{TResponse}"/> — it is NOT auto-validated by
/// <c>ValidationBehavior</c> (which only runs for <c>ICommand</c>).
/// The handler performs its own input guards.</para>
///
/// <para>Retrieval flow:
/// 1. Embed <see cref="Text"/> via <c>IEmbeddingProvider</c>.
/// 2. JOIN <c>chunk_embeddings_bge_m3</c> (IsActive) ⋈ <c>CurriculumChunk</c> ⋈ <c>CurriculumVersion</c>
///    WHERE <c>Status = Active</c> AND <c>GradeId</c> AND <c>SubjectId</c>
///    (AND <c>SkillId</c> when <see cref="WeakAreaSkillId"/> is set).
///    ORDER BY <c>Vector &lt;=&gt; queryVector</c> LIMIT <see cref="TopK"/>.
/// 3. Apply similarity floor — reject rows whose cosine distance exceeds the threshold.
/// 4. Return <see cref="RetrievalResult"/> (empty list when floor not met or provider returned null).
/// </para>
///
/// <para><strong>Security:</strong> raw embedding vectors are never included in the response.</para>
/// </summary>
public sealed class RetrieveChunksQuery : IQuery<BaseResponse<RetrievalResult>>
{
    /// <summary>The query text to embed for similarity search (e.g. skill name or question text).</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Grade identifier (loose ref — no cross-module FK).</summary>
    public int GradeId { get; init; }

    /// <summary>Subject identifier (loose ref — no cross-module FK).</summary>
    public int SubjectId { get; init; }

    /// <summary>
    /// Optional skill identifier for weak-area filtering.
    /// When set, only chunks whose <c>CurriculumChunk.SkillId</c> matches are considered.
    /// </summary>
    public int? WeakAreaSkillId { get; init; }

    /// <summary>Maximum number of chunks to return (before similarity-floor filtering).</summary>
    public int TopK { get; init; } = 5;
}
