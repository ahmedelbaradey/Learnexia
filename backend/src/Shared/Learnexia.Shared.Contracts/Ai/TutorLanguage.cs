namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// The two supported medium-of-instruction languages for the AI tutor.
///
/// <para>Named <c>TutorLanguage</c> to avoid collision with any future general-purpose
/// language enum in Shared.Contracts. The identity layer uses the string codes "ar"/"en"
/// (see <see cref="LearningLanguageChangedIntegrationEvent"/>); this enum provides
/// a type-safe variant for the prompt builder and seam interfaces.</para>
///
/// <para>NFR-5: Arabic-first — prompts default to <see cref="Ar"/> when the student's
/// language preference is Arabic.</para>
/// </summary>
public enum TutorLanguage
{
    /// <summary>Arabic (العربية) — the student should receive Arabic output.</summary>
    Ar = 1,

    /// <summary>English — the student should receive English output.</summary>
    En = 2,
}
