using Learnexia.Modules.Ai.Application.PromptBuilder.Templates;
using Learnexia.Shared.Contracts.Ai;

namespace Learnexia.Modules.Ai.Application.PromptBuilder;

/// <summary>
/// Pure, stateless template lookup: (Subject, TutorLanguage) → <see cref="ISubjectTemplate"/>.
///
/// <para><strong>Design rule (CLAUDE.md rule 8):</strong> this is a plain
/// <see cref="Dictionary{TKey,TValue}"/> lookup — NOT a Strategy or Factory pattern.
/// No abstraction beyond the dictionary is introduced.</para>
///
/// <para><strong>Exactly 4 subjects:</strong> the dictionary contains entries for
/// Math, Science, Arabic, and English. Social Studies is explicitly absent.
/// A unit test asserts exactly 4 entries and that the set equals {Math, Science, Arabic, English}.</para>
///
/// <para><strong>Unsupported subject:</strong> subjects not in the dictionary return
/// <see cref="TemplateLookupResult.Unsupported"/> (Q3 — no silent default).</para>
///
/// <para>Registered as Singleton in DI — pure, stateless, thread-safe.</para>
/// </summary>
public sealed class TemplateSelector
{
    // One template instance per subject. Each implementation handles all 4 intents × 2 languages.
    private static readonly Dictionary<Subject, ISubjectTemplate> Templates =
        new()
        {
            { Subject.Math,    new MathTemplate()    },
            { Subject.Science, new ScienceTemplate() },
            { Subject.Arabic,  new ArabicTemplate()  },
            { Subject.English, new EnglishTemplate() },
            // NOTE: Subject.SocialStudies is intentionally absent.
            // Do not add a 5th entry without a product-decision override and a plan update.
        };

    /// <summary>
    /// Returns the template text for the given subject, intent, and language.
    /// Returns <see cref="TemplateLookupResult.Unsupported"/> when the subject has no registered template.
    /// </summary>
    public TemplateLookupResult Select(Subject subject, HelperIntent intent, TutorLanguage language)
    {
        if (!Templates.TryGetValue(subject, out var template))
            return TemplateLookupResult.Unsupported;

        var text = template.GetTemplate(intent, language);
        return TemplateLookupResult.Found(text);
    }

    /// <summary>
    /// Returns the count of registered subject entries (used by unit tests to assert the 4-member invariant).
    /// </summary>
    public static int RegisteredSubjectCount => Templates.Count;
}

/// <summary>Discriminated result of <see cref="TemplateSelector.Select"/>.</summary>
public abstract record TemplateLookupResult
{
    private TemplateLookupResult() { }

    /// <summary>A template was found; <see cref="TemplateText"/> is the assembled template body.</summary>
    public sealed record FoundResult(string TemplateText) : TemplateLookupResult;

    /// <summary>
    /// No template is registered for the requested subject.
    /// The calling handler should surface a user-facing error (Q3 decision).
    /// </summary>
    public sealed record UnsupportedResult : TemplateLookupResult;

    /// <summary>Convenience factory for a successful lookup.</summary>
    public static TemplateLookupResult Found(string text) => new FoundResult(text);

    /// <summary>Singleton unsupported result.</summary>
    public static readonly TemplateLookupResult Unsupported = new UnsupportedResult();
}
