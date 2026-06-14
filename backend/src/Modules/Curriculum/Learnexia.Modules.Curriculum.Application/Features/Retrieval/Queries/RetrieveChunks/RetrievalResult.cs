namespace Learnexia.Modules.Curriculum.Application.Features.Retrieval.Queries.RetrieveChunks;

/// <summary>
/// The result of a <see cref="RetrieveChunksQuery"/> handler invocation.
///
/// <para><see cref="Chunks"/> may be empty — this is NOT an error. An empty list is the
/// clean signal for "no curriculum context clears the similarity floor" (AC-4).
/// Callers must treat empty chunks as the "no context" branch and redirect rather than
/// calling the AI gateway with an empty prompt.</para>
/// </summary>
/// <param name="Chunks">
/// Top-k chunks ordered by cosine similarity (descending), filtered by the similarity floor.
/// Empty when no embedding was available (provider returned null) or when no chunk exceeded
/// the floor threshold.
/// </param>
public sealed record RetrievalResult(IReadOnlyList<CurriculumChunkDto> Chunks);
