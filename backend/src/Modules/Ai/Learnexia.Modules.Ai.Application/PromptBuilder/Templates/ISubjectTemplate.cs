using Learnexia.Shared.Contracts.Ai;

namespace Learnexia.Modules.Ai.Application.PromptBuilder.Templates;

/// <summary>
/// A subject-specific, intent-specific prompt template (ar + en variants).
///
/// <para>Each of the four MVP subjects (Math / Science / Arabic / English) provides one
/// implementation. There is NO Social Studies implementation (product-decision override).</para>
///
/// <para><strong>FR-AI-6:</strong> every implementation must return content-only template text.
/// No template asks the model to decide progression, difficulty, unlocking, or mastery level.</para>
///
/// <para><strong>Scope constraint:</strong> every template must include an instruction scoping
/// the model to the "active skill/question context only" (ai-helper-mvp.md §2 Layer 2).</para>
/// </summary>
internal interface ISubjectTemplate
{
    /// <summary>Returns the template text for the given intent and language variant.</summary>
    string GetTemplate(HelperIntent intent, TutorLanguage language);
}
