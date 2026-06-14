namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// The four allowed AI Helper intents (approved per <c>docs/briefs/ai-helper-mvp.md §1</c>).
///
/// <para>This is a closed, exhaustive set — no intent may be added without a lead approval
/// and a corresponding subject template update. If a fifth intent is added, the compile-time
/// exhaustive switch in <c>TemplateSelector</c> will fail, surfacing the gap immediately.</para>
///
/// <para>Supersedes the earlier <c>TutorTask</c> sketch — the four intents here map directly
/// to the four helper user-facing actions.</para>
/// </summary>
public enum HelperIntent
{
    /// <summary>
    /// "اشرح السؤال / Explain this concept" — scoped to the active skill/question only.
    /// Maps to <see cref="AiTaskKind.Explain"/> (mid tier — Sonnet).
    /// </summary>
    Explain = 1,

    /// <summary>
    /// "اديني تلميح / Give me a hint" — nudge without revealing the answer, scoped to
    /// the current wrong answer. Maps to <see cref="AiTaskKind.Hint"/> (mid tier — Sonnet).
    /// </summary>
    Hint = 2,

    /// <summary>
    /// "ليه إجابتي غلط / Why is my answer wrong" — uses the student's actual wrong answer
    /// as a dynamic slot. Always runtime (no cache). Maps to <see cref="AiTaskKind.Hint"/>
    /// (mid tier — Sonnet).
    /// </summary>
    WhyWrong = 3,

    /// <summary>
    /// "اديني مثال مشابه / Give me a similar example" — grounded in the current skill context.
    /// Maps to <see cref="AiTaskKind.Explain"/> (mid tier — Sonnet).
    /// </summary>
    SimilarExample = 4,
}
