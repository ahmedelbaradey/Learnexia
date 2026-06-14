using Learnexia.Shared.Contracts.Ai;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Ai.Application.Services;

/// <summary>
/// Produces the localized refuse-and-redirect copy for the AI helper intents when
/// <c>ILearningContextProvider</c> returns no approved grounding context (ai-helper-mvp.md §4).
///
/// <para>Shared across P3-04 (Explain), P3-05 (Hint/WhyWrong), and P3-06 (SimilarExample).</para>
///
/// <para>The redirect text is localizable: Arabic when <see cref="TutorLanguage.Ar"/>,
/// English otherwise. The skill/lesson name is interpolated into the template via
/// <c>string.Format</c> — no external library required (plain formatting, not a pattern).</para>
/// </summary>
public sealed class RedirectResponseBuilder
{
    private readonly IStringLocalizer<SharedResources> _localizer;

    public RedirectResponseBuilder(IStringLocalizer<SharedResources> localizer)
    {
        _localizer = localizer;
    }

    /// <summary>
    /// Returns the localized redirect message for the given skill name and language.
    /// </summary>
    /// <param name="skillName">
    /// The active skill or lesson name (used as {0} in the template).
    /// When null or empty, a generic placeholder is substituted.
    /// </param>
    /// <param name="language">The student's medium-of-instruction language.</param>
    public string Build(string? skillName, TutorLanguage language)
    {
        var name = string.IsNullOrWhiteSpace(skillName) ? "?" : skillName;

        var key = language == TutorLanguage.Ar
            ? SharedResourcesKey.AiTutorRedirectAr
            : SharedResourcesKey.AiTutorRedirectEn;

        var template = _localizer[key].Value;
        return string.Format(template, name);
    }
}
