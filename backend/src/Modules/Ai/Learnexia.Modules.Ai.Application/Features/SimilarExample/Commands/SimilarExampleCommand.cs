using Learnexia.Shared.Kernel.Messaging;

namespace Learnexia.Modules.Ai.Application.Features.SimilarExample.Commands;

/// <summary>
/// Command: student requests a similar worked example for the active skill (intent #4 — "اديني مثال مشابه").
///
/// <para>Grade, age, and language are resolved from the authenticated student's JWT claims
/// inside the handler via <c>ICurrentUserService</c> — never supplied by the client.
/// This prevents a student from spoofing a higher or lower grade for model routing.</para>
///
/// <para><see cref="SkillId"/> is required (validated by <see cref="SimilarExampleCommandValidator"/>)
/// to scope the example to the active skill context (AC-7 scope guard).</para>
///
/// <para><see cref="QuestionId"/> is optional: when the student is in quiz mode and a specific
/// question is active, the handler passes it to <c>ILearningContextProvider</c> so the grounding
/// context can include the question text.</para>
///
/// <para>Returns a <see cref="SimilarExampleResult"/> discriminated union that the SSE controller
/// maps to the pinned wire contract (event: message / redirect / error / done).</para>
/// </summary>
public sealed record SimilarExampleCommand(
    int SkillId,
    int? QuestionId) : ICommand<SimilarExampleResult>;

/// <summary>
/// Discriminated result of <see cref="SimilarExampleCommand"/>.
/// The SSE controller maps each case to the pinned SSE wire contract.
/// </summary>
public abstract record SimilarExampleResult
{
    private SimilarExampleResult() { }

    /// <summary>
    /// Safety-approved content to stream as <c>event: message</c> + <c>data: {"content":"…"}</c>
    /// followed by <c>event: done</c> + <c>data: [DONE]</c>.
    /// </summary>
    public sealed record Streamed(string Content) : SimilarExampleResult;

    /// <summary>
    /// No grounding context — emit as <c>event: redirect</c> + <c>data: {"type":"lesson","targetId":"…"}</c>
    /// followed by <c>event: done</c> + <c>data: [DONE]</c>.
    /// </summary>
    public sealed record Redirect(string RedirectText, int? TargetSkillId) : SimilarExampleResult;

    /// <summary>
    /// Safety layer blocked or gateway failure — emit as <c>event: error</c> +
    /// <c>data: {"code":"…","message":"…"}</c>. No <c>event: done</c> is emitted on error.
    /// </summary>
    public sealed record Error(string Code, string Message) : SimilarExampleResult;
}
