using Learnexia.Shared.Kernel.Messaging;

namespace Learnexia.Modules.Ai.Application.Features.Simplify.Commands;

/// <summary>
/// Command: student requests a simpler re-explanation of a concept/lesson (P3-05 BE-2 / AC-5).
/// Reuses the P3-04 explain pipeline with a "simplify / lower reading level" directive.
///
/// <para>Grade, age, and language are resolved from the authenticated student's JWT claims
/// inside the handler via <c>ICurrentUserService</c> — never supplied by the client.</para>
///
/// <para>At least one of <see cref="ConceptId"/> or <see cref="LessonId"/> is required
/// (validated by <see cref="SimplifyExplanationCommandValidator"/>).</para>
///
/// <para><see cref="PreviousExplanationRef"/> is optional — an opaque reference the FE can
/// pass to future cache-lookup logic. Not consumed in the MVP slice (DEFERRED).</para>
///
/// <para>Returns the same <c>ExplainResult</c> union as the Explain endpoint, allowing the
/// SSE controller to reuse the same emit logic unchanged.</para>
/// </summary>
public sealed record SimplifyExplanationCommand(
    int? ConceptId,
    int? LessonId,
    string? PreviousExplanationRef) : ICommand<SimplifyResult>;

/// <summary>
/// Discriminated result of <see cref="SimplifyExplanationCommand"/>.
/// Identical shape to <c>ExplainResult</c> — the SSE controller maps each case to the same
/// pinned wire contract.
/// </summary>
public abstract record SimplifyResult
{
    private SimplifyResult() { }

    /// <summary>Safety-approved simpler re-explanation content.</summary>
    public sealed record Streamed(string Content) : SimplifyResult;

    /// <summary>No grounding context — emit as <c>event: redirect</c>.</summary>
    public sealed record Redirect(string RedirectText, int? TargetSkillId) : SimplifyResult;

    /// <summary>Safety blocked or gateway failure — emit as <c>event: error</c>.</summary>
    public sealed record Error(string Code, string Message) : SimplifyResult;
}
