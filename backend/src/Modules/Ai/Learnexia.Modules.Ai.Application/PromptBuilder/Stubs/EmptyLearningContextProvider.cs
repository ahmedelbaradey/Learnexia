using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Contracts.AiTutor;

namespace Learnexia.Modules.Ai.Application.PromptBuilder.Stubs;

/// <summary>
/// Default (stub) implementation of <see cref="ILearningContextProvider"/>.
/// Returns a <see cref="LearningContext"/> with empty chunks — enabling P3-03 to ship
/// before <c>SeededCorpusContextProvider</c> (BE-9) or <c>RagContextProvider</c> (P3-07).
///
/// <para>The prompt builder handles empty chunks by omitting the context section entirely
/// (AC4 graceful degradation). Callers that need grounding (P3-04/P3-05/P3-06) must
/// check for empty chunks and apply the refuse-and-redirect rule (ai-helper-mvp.md §4).</para>
///
/// <para>Registered with <c>TryAddTransient</c> so that the real <c>SeededCorpusContextProvider</c>
/// registered in BE-10 (or later by P3-07's <c>RagContextProvider</c>) overrides this stub.</para>
/// </summary>
public sealed class EmptyLearningContextProvider : ILearningContextProvider
{
    public Task<LearningContext> GetContextAsync(
        int studentId,
        int skillId,
        int? questionId,
        string? wrongAnswer,
        CancellationToken ct = default)
        => Task.FromResult(new LearningContext(
            Chunks: Array.Empty<ChunkDto>(),
            QuestionText: null,
            WrongAnswer: null,
            SkillId: skillId,
            QuestionId: questionId,
            GradeId: 0,
            SubjectId: 0,
            Language: TutorLanguage.Ar));
}
