using Learnexia.Shared.Kernel.Messaging;

namespace Learnexia.Modules.Ai.Application.Features.Recommendation.Commands;

/// <summary>
/// Command: student (or parent) requests a Lexi narration of the child's persisted daily
/// recommendation set (P3-14 — "Ask Lexi" intent).
///
/// <para><strong>Client-blind scoping (security-critical):</strong> the child id is resolved
/// from the authenticated student's JWT (<c>ICurrentUserService.UserId</c>) inside the handler.
/// The command body carries NO child-id field — this prevents any caller from requesting
/// narration for another child.</para>
///
/// <para>Returns a <see cref="RecommendationNarrationResult"/> discriminated union that the
/// SSE controller maps to the pinned wire contract (event: message / declined / error / done).</para>
/// </summary>
public sealed record RecommendationNarrationCommand : ICommand<RecommendationNarrationResult>;

/// <summary>
/// Discriminated result of <see cref="RecommendationNarrationCommand"/>.
/// The SSE controller maps each case to the pinned SSE wire contract (mirroring ExplainResult).
/// </summary>
public abstract record RecommendationNarrationResult
{
    private RecommendationNarrationResult() { }

    /// <summary>
    /// Safety-approved narration to stream as <c>event: message</c> +
    /// <c>data: {"content":"…"}</c> followed by <c>event: done</c> + <c>data: [DONE]</c>.
    /// </summary>
    public sealed record Streamed(string Content) : RecommendationNarrationResult;

    /// <summary>
    /// No persisted recommendations yet — decline gracefully as <c>event: declined</c> +
    /// <c>data: {"reason":"NoData"}</c> followed by <c>event: done</c>.
    /// No debit on decline.
    /// </summary>
    public sealed record Declined(string Reason) : RecommendationNarrationResult;

    /// <summary>
    /// Safety layer blocked or gateway failure — emit as <c>event: error</c> +
    /// <c>data: {"code":"…","message":"…"}</c>. No <c>event: done</c> on error.
    /// No debit on error.
    /// </summary>
    public sealed record Error(string Code, string Message) : RecommendationNarrationResult;
}
