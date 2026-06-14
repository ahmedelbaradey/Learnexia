using Learnexia.Shared.Contracts.Ai;

namespace Learnexia.Modules.Ai.Application.PromptBuilder;

/// <summary>
/// Assembles a personalised, child-safe AI request from a <see cref="PromptContext"/>.
///
/// <para>The builder is a <strong>pure, deterministic</strong> assembly function:
/// given the same <see cref="PromptContext"/>, it always produces the same
/// <see cref="AiRequest"/>. It performs no I/O. All seam reads (weak areas, curriculum
/// context, child learning profile) are performed by the calling handler BEFORE invoking
/// <c>Build</c>, and the results are passed in via <see cref="PromptContext"/>.</para>
///
/// <para><strong>Unconditional tone frame:</strong> every call to <c>Build</c> unconditionally
/// prepends the child-safe tone + anti-injection frame (BE-2). No caller can omit it.</para>
///
/// <para><strong>FR-AI-6:</strong> the assembled prompt requests content only — never a
/// progression, difficulty, or unlock decision.</para>
/// </para>
/// </summary>
public interface IPromptBuilder
{
    /// <summary>
    /// Assembles and returns an <see cref="AiRequest"/> carrying the complete personalised prompt.
    /// Returns a typed <see cref="UnsupportedSubjectResult"/> if the subject has no template.
    /// </summary>
    PromptBuilderResult Build(PromptContext context);
}

/// <summary>Discriminated result of <see cref="IPromptBuilder.Build"/>.</summary>
public abstract record PromptBuilderResult
{
    private PromptBuilderResult() { }

    /// <summary>The prompt was assembled successfully.</summary>
    public sealed record Success(AiRequest Request) : PromptBuilderResult;

    /// <summary>
    /// The subject has no registered template — the calling handler should surface
    /// a user-facing error (Q3 decision: typed result, no silent default).
    /// </summary>
    public sealed record UnsupportedSubjectResult(Subject Subject) : PromptBuilderResult;
}
