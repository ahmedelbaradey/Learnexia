using Learnexia.Shared.Kernel.Messaging;

namespace Learnexia.Modules.Ai.Application.Features.Explain.Commands;

/// <summary>
/// Command: student requests an AI explanation of a concept/lesson/skill (intent #1 — "اشرح السؤال").
///
/// <para>Grade, age, and language are resolved from the authenticated student's JWT claims
/// inside the handler via <c>ICurrentUserService</c> — never supplied by the client.
/// This prevents a student from spoofing a higher or lower grade for model routing.</para>
///
/// <para>At least one of <see cref="LessonId"/>, <see cref="ConceptId"/>, or
/// <see cref="SkillId"/> is required (validated by <see cref="ExplainConceptCommandValidator"/>)
/// to scope the explanation to the active curriculum context (AC3).</para>
///
/// <para>Returns an <see cref="ExplainResult"/> discriminated union that the SSE controller
/// maps to the pinned wire contract (event: message / redirect / error / done).</para>
/// </summary>
public sealed record ExplainConceptCommand(
    int? LessonId,
    int? ConceptId,
    int? SkillId,
    string? Question) : ICommand<ExplainResult>;

/// <summary>
/// Discriminated result of <see cref="ExplainConceptCommand"/>.
/// The SSE controller maps each case to the pinned SSE wire contract.
/// </summary>
public abstract record ExplainResult
{
    private ExplainResult() { }

    /// <summary>
    /// Safety-approved content to stream as <c>event: message</c> + <c>data: {"content":"…"}</c>
    /// followed by <c>event: done</c> + <c>data: [DONE]</c>.
    /// </summary>
    public sealed record Streamed(string Content) : ExplainResult;

    /// <summary>
    /// No grounding context — emit as <c>event: redirect</c> + <c>data: {"type":"lesson","targetId":"…"}</c>
    /// followed by <c>event: done</c> + <c>data: [DONE]</c>.
    /// </summary>
    public sealed record Redirect(string RedirectText, int? TargetSkillId) : ExplainResult;

    /// <summary>
    /// Safety layer blocked or gateway failure — emit as <c>event: error</c> +
    /// <c>data: {"code":"…","message":"…"}</c>. No <c>event: done</c> is emitted on error.
    /// </summary>
    public sealed record Error(string Code, string Message) : ExplainResult;
}
