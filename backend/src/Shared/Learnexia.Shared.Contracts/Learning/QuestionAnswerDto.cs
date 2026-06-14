namespace Learnexia.Shared.Contracts.Learning;

/// <summary>
/// Minimal question context returned by <see cref="IQuestionAnswerContract"/> to the Ai module.
///
/// <para>Carries only what the Ai handler needs — the correct answer (for the no-reveal post-check
/// per OQ-4) and the current server-derived hint level (for escalation per OQ-2b).
/// Never exposes QuestionText, Options, or student PII to the Ai module.</para>
/// </summary>
/// <param name="CorrectAnswer">
/// The question's authoritative correct answer. Used by the no-reveal guard:
/// if the generated hint contains this string, the hint is blocked.
/// </param>
/// <param name="CurrentHintLevel">
/// The current hint level for this question in this attempt, derived from
/// <c>Attempt.HintsUsedCount</c>. Starts at 1 for the first hint request.
/// </param>
public sealed record QuestionAnswerDto(
    string CorrectAnswer,
    int CurrentHintLevel);
