namespace Learnexia.Shared.Contracts.AiTutor;

/// <summary>
/// Cross-module seam: resolves grounding context for the AI helper's four allowed intents.
///
/// <para>All four helper intents (Explain / Hint / WhyWrong / SimilarExample) fetch grounding
/// through this single interface. The active implementation is selected via the
/// <c>AiHelper:ContextProvider</c> configuration key:</para>
/// <list type="bullet">
///   <item><c>"SeededCorpus"</c> (default) — <c>SeededCorpusContextProvider</c> reads from the
///     P2-11 <c>KnowledgeNode</c>/<c>KnowledgeEdge</c> graph and the hand-seeded corpus
///     (ships now, MVP).</item>
///   <item><c>"Rag"</c> — <c>RagContextProvider</c> wraps the P3-07 pgvector RAG retrieval
///     (ships when P3-07 merges — no code change required in the helper).</item>
/// </list>
///
/// <para><strong>Refuse-and-redirect rule (AC4, ai-helper-mvp.md §4):</strong> when
/// <see cref="GetContextAsync"/> returns a <see cref="LearningContext"/> with empty
/// <see cref="LearningContext.Chunks"/> and no relevant skill grounding, callers must
/// redirect (not answer). Do NOT call the AI gateway with an empty-context prompt.</para>
///
/// <para><strong>PII rule:</strong> implementations must never populate
/// <see cref="LearningContext"/> with the student's name, email, birthdate, parent info,
/// or any direct identifier. Carry only anonymous proxies: grade, age, skill/question ids,
/// wrong answer text.</para>
/// </summary>
public interface ILearningContextProvider
{
    /// <summary>
    /// Returns the grounding context for the helper request.
    /// Returns a <see cref="LearningContext"/> with empty <see cref="LearningContext.Chunks"/>
    /// when no approved content is available for the skill — callers treat empty as
    /// "no context" and redirect (AC4).
    /// </summary>
    /// <param name="studentId">The student id (used for DB lookup; NOT injected into the prompt).</param>
    /// <param name="skillId">The active skill.</param>
    /// <param name="questionId">The active question (null when not in quiz mode).</param>
    /// <param name="wrongAnswer">The student's wrong answer (non-null for the WhyWrong intent only).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<LearningContext> GetContextAsync(
        int studentId,
        int skillId,
        int? questionId,
        string? wrongAnswer,
        CancellationToken ct = default);
}
