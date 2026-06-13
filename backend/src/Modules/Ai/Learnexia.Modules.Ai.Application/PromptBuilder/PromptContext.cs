using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Contracts.AiTutor;

namespace Learnexia.Modules.Ai.Application.PromptBuilder;

/// <summary>
/// Input context for <see cref="IPromptBuilder.Build"/>. Carries all data the builder
/// needs to assemble a personalised, child-safe prompt.
///
/// <para><strong>PII minimisation rule (security-auditor gate):</strong>
/// this record carries <see cref="StudentId"/> for seam lookups performed BEFORE <c>Build</c>
/// is called (e.g. fetching weak areas). The builder MUST NOT inject <see cref="StudentId"/>
/// into the assembled prompt text — only the anonymous proxies <see cref="Grade"/> and
/// <see cref="Age"/> may appear in the prompt body.</para>
/// </summary>
/// <param name="StudentId">
/// The internal student identifier. Used by callers to query seams (e.g. weak areas)
/// BEFORE calling <c>Build</c>. MUST NOT appear in the assembled prompt text.
/// </param>
/// <param name="Intent">The validated helper intent (one of the four allowed intents).</param>
/// <param name="Subject">The curriculum subject for this request (exactly 4 members, no Social Studies).</param>
/// <param name="Grade">The student's current school grade (anonymous proxy — used in prompt text).</param>
/// <param name="Age">The student's approximate age in years (anonymous proxy — used in prompt text).</param>
/// <param name="Language">The student's medium-of-instruction language preference.</param>
/// <param name="WeakAreas">
/// Optional weak curriculum areas. When null or empty the builder omits the weak-areas
/// section entirely (AC4 graceful degradation). Populated from <c>IStudentWeakAreasQuery</c>
/// by the calling handler before invoking <c>Build</c>.
/// </param>
/// <param name="Context">
/// Optional learning context (curriculum chunks, question text, wrong answer, skill metadata).
/// When null or its <c>Chunks</c> collection is empty the builder omits the context section
/// entirely (AC4 graceful degradation). Populated from <c>ILearningContextProvider</c>
/// by the calling handler before invoking <c>Build</c>.
/// </param>
public sealed record PromptContext(
    int StudentId,
    HelperIntent Intent,
    Subject Subject,
    int Grade,
    int Age,
    TutorLanguage Language,
    IReadOnlyList<WeakArea>? WeakAreas,
    LearningContext? Context);
