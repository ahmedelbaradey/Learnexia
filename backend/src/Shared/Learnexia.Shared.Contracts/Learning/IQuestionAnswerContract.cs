namespace Learnexia.Shared.Contracts.Learning;

/// <summary>
/// Cross-module seam: allows the Ai module to read a question's <see cref="QuestionAnswerDto.CorrectAnswer"/>
/// (for the no-reveal post-generation check — OQ-4) and the student's current hint level for that
/// question/attempt (OQ-2b server-derived hint level) without a cross-module FK or project reference.
///
/// <para>Implemented by <c>QuestionAnswerContractAdapter</c> in
/// <c>Learnexia.Modules.Learning.Infrastructure</c>, registered in
/// <c>AddLearningInfrastructure</c>. The Ai handler injects this interface only — it never
/// references Learning's projects directly (module isolation rule).</para>
///
/// <para>Returns <c>null</c> when the question or attempt is not found, so callers must
/// handle the null case.</para>
/// </summary>
public interface IQuestionAnswerContract
{
    /// <summary>
    /// Returns the correct answer for <paramref name="questionId"/> and the current hint level
    /// derived from the attempt's state, or <c>null</c> when either is not found OR the attempt
    /// does not belong to <paramref name="studentId"/> (ownership guard — IDOR prevention).
    /// </summary>
    /// <param name="questionId">The question to look up.</param>
    /// <param name="attemptId">The quiz attempt whose hint count drives the server-derived hint level.</param>
    /// <param name="studentId">
    ///   The authenticated student's id. The adapter rejects attempts that are not owned by this student.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    ///   A populated <see cref="QuestionAnswerDto"/> when the question exists and the attempt belongs
    ///   to <paramref name="studentId"/>; <c>null</c> otherwise (not found OR not owned).
    ///   Callers must treat <c>null</c> as "not available" and refuse before calling the LLM.
    /// </returns>
    Task<QuestionAnswerDto?> GetQuestionAnswerAsync(
        int questionId,
        int attemptId,
        int studentId,
        CancellationToken ct = default);
}
