using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Kernel.Messaging;

namespace Learnexia.Modules.Ai.Application.Features.Hint.Commands;

/// <summary>
/// Command: student requests a hint or a why-wrong explanation for a quiz question (P3-05 BE-1).
/// Handles both <see cref="HelperIntent.Hint"/> and <see cref="HelperIntent.WhyWrong"/> intents.
///
/// <para>Grade, age, and language are resolved from the authenticated student's JWT claims
/// inside the handler via <c>ICurrentUserService</c> — never supplied by the client.
/// This prevents a student from spoofing a higher or lower grade for model routing.</para>
///
/// <para><see cref="HintLevel"/> is optional: when absent, the handler derives the current
/// level from the attempt state via <c>IQuestionAnswerContract</c> (OQ-2b server-derived)
/// so escalation cannot be gamed by the client.</para>
///
/// <para><see cref="WrongAnswer"/> is REQUIRED when <see cref="Intent"/> == <see cref="HelperIntent.WhyWrong"/>
/// and is validated by <see cref="GetHintCommandValidator"/>.</para>
///
/// <para>Returns a <see cref="HintResult"/> discriminated union that the SSE controller maps
/// to the pinned wire contract (event: message / redirect / error / done).</para>
/// </summary>
public sealed record GetHintCommand(
    int QuestionId,
    int AttemptId,
    HelperIntent Intent,
    int? HintLevel,
    string? WrongAnswer) : ICommand<HintResult>;

/// <summary>
/// Discriminated result of <see cref="GetHintCommand"/>.
/// The SSE controller maps each case to the pinned SSE wire contract (same as ExplainResult).
/// </summary>
public abstract record HintResult
{
    private HintResult() { }

    /// <summary>
    /// Safety-approved hint or why-wrong content.
    /// For Hint intent: also carries the hint level metadata for the SSE preamble frame.
    /// For WhyWrong: <see cref="CurrentHintLevel"/> and <see cref="NextHintLevel"/> are null.
    /// </summary>
    public sealed record Streamed(
        string Content,
        int? CurrentHintLevel,
        int? NextHintLevel) : HintResult;

    /// <summary>
    /// No grounding context — emit as <c>event: redirect</c>.
    /// </summary>
    public sealed record Redirect(string RedirectText, int? TargetSkillId) : HintResult;

    /// <summary>
    /// Safety layer blocked, no-reveal violation, gateway failure, or rate limit exceeded.
    /// Emit as <c>event: error</c>. No <c>event: done</c> is emitted on error.
    /// </summary>
    public sealed record Error(string Code, string Message) : HintResult;
}
