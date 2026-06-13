namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// The four MVP curriculum subjects supported by the AI tutor.
///
/// <para><strong>Invariant — exactly 4 members:</strong> Math, Science, Arabic, English.
/// Social Studies is intentionally excluded per the product-decision override (story Notes,
/// P3-03 brief §1). A unit test in <c>Modules.Ai.UnitTests</c> asserts
/// <c>Enum.GetValues&lt;Subject&gt;().Length == 4</c> and that the set equals
/// {Math, Science, Arabic, English}.</para>
/// </summary>
public enum Subject
{
    /// <summary>Mathematics — step-by-step instruction style.</summary>
    Math = 1,

    /// <summary>Science — visual / exploratory instruction style.</summary>
    Science = 2,

    /// <summary>Arabic language — vocabulary + grammar in Fusha (Modern Standard Arabic).</summary>
    Arabic = 3,

    /// <summary>English language — vocabulary + grammar for Arabic-speaking learners.</summary>
    English = 4,

    // NOTE: Social Studies is explicitly omitted. Do not add it.
}
