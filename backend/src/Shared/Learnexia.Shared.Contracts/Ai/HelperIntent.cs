namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// The five allowed AI Helper intents.
///
/// <para>This is a closed, exhaustive set — no intent may be added without a lead approval
/// (rule #8) and a corresponding subject template update. If a sixth intent is added, the
/// compile-time exhaustive switch in <c>TemplateSelector</c> will fail, surfacing the gap
/// immediately.</para>
///
/// <para>The fifth intent (<see cref="Recommendation"/>) was approved by the lead 2026-06-18
/// per rule #8 (P3-14 narration intent).</para>
///
/// <para>Supersedes the earlier <c>TutorTask</c> sketch — the intents here map directly
/// to the helper user-facing actions.</para>
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

    /// <summary>
    /// "Lexi recommendation narration" — narrates the child's persisted daily recommendation
    /// set kid-style, grade-tuned, grounded ONLY on <c>IStudentRecommendationsQuery</c> persisted
    /// content. Cost = 5 (Practice tier). Lead-approved 2026-06-18 (P3-14, rule #8).
    /// Maps to <see cref="AiTaskKind.Explain"/> (mid tier — Sonnet).
    /// </summary>
    Recommendation = 5,
}
