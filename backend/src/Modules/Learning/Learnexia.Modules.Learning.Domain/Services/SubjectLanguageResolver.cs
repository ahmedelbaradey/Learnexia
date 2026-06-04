using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Domain.Services;

/// <summary>
/// Pure, deterministic resolver that maps a <see cref="SubjectCode"/> + a student's
/// <see cref="ContentLanguage"/> learning language to the effective content language for that subject.
///
/// Resolution rules (per localization-architecture.md §2):
///   ARABIC  → ContentLanguage.Ar  (pinned — language-learning subject)
///   ENGLISH → ContentLanguage.En  (pinned — language-learning subject)
///   MATH    → learnerLang          (follows the student's medium of instruction)
///   SCIENCE → learnerLang          (follows the student's medium of instruction)
///
/// CONTRACT:
/// - Pure static — no database, no DI. Caller supplies both inputs.
/// - Thread-safe: no shared mutable state.
/// - Mirrors <see cref="LearningPathEngine"/> and <see cref="SkillGraphValidator"/> static-pure patterns.
/// </summary>
public static class SubjectLanguageResolver
{
    /// <summary>
    /// Returns the effective <see cref="ContentLanguage"/> for a subject given the student's
    /// learning language.
    /// </summary>
    /// <param name="code">The stable curriculum identifier for the subject.</param>
    /// <param name="learnerLang">The student's learning language (from the JWT claim).</param>
    /// <returns>
    /// <see cref="ContentLanguage.Ar"/> for ARABIC (pinned);
    /// <see cref="ContentLanguage.En"/> for ENGLISH (pinned);
    /// <paramref name="learnerLang"/> for MATH or SCIENCE (follows the student's medium).
    /// </returns>
    public static ContentLanguage Resolve(SubjectCode code, ContentLanguage learnerLang)
        => code switch
        {
            SubjectCode.ARABIC  => ContentLanguage.Ar,
            SubjectCode.ENGLISH => ContentLanguage.En,
            SubjectCode.MATH    => learnerLang,
            SubjectCode.SCIENCE => learnerLang,
            _                   => learnerLang,   // defensive default for any future code
        };
}
