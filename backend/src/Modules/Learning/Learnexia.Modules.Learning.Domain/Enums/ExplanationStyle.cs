namespace Learnexia.Modules.Learning.Domain.Enums;

/// <summary>
/// Derived preference for how a student receives explanations — populated by
/// <c>StudentProfileEngine.DeriveExplanationStyle</c> from affinity + hint-behavior signals.
///
/// Stored as <c>int</c> via <c>HasConversion&lt;int&gt;()</c> in EF Core.
/// Values are zero-based so the EF column default (0) maps to <c>Standard</c> — the safe
/// cold-start default — without needing an explicit seed value in the DB migration.
///
/// Provisional — values pending confirmation against P3-03 prompt builder (AI module, not yet built).
/// </summary>
public enum ExplanationStyle
{
    /// <summary>
    /// Default / cold-start value. Standard textual explanation; no special adaptation applied.
    /// Used when data is insufficient to derive a preference.
    /// </summary>
    Standard   = 0,

    /// <summary>
    /// Simplified language and shorter sentences. Derived when accuracy on text-heavy question
    /// types is low and hint usage is high.
    /// </summary>
    Simplified = 1,

    /// <summary>
    /// Visual / diagram-oriented explanation. Derived when affinity for Matching / visual
    /// question formats is high relative to the student's overall accuracy.
    /// </summary>
    Visual     = 2,

    /// <summary>
    /// Step-by-step breakdown. Derived when FillInBlank accuracy is high and hint usage is
    /// low, indicating the student follows structured procedural reasoning.
    /// </summary>
    StepByStep = 3,
}
