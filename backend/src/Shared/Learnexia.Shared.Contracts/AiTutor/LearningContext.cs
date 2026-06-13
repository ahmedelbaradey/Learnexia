using Learnexia.Shared.Contracts.Ai;

namespace Learnexia.Shared.Contracts.AiTutor;

/// <summary>
/// A single approved curriculum chunk used as AI grounding context.
/// </summary>
/// <param name="ChunkId">Opaque identifier (e.g. curriculum node or chunk id).</param>
/// <param name="Text">The approved, QA-passed curriculum text for this chunk.</param>
public sealed record ChunkDto(string ChunkId, string Text);

/// <summary>
/// The grounding context passed from <see cref="ILearningContextProvider"/> to the prompt builder.
/// Carries the curriculum chunks, question context, and student's wrong answer (when applicable).
///
/// <para><strong>PII rule:</strong> this record must NOT carry the student's name, email,
/// birthdate, parent info, or any direct identifier. Only anonymous proxies are permitted:
/// grade, age, language, skill/question ids, wrong answer text.</para>
/// </summary>
/// <param name="Chunks">
/// Approved, QA-passed curriculum text chunks for the active skill.
/// Empty list means no grounding context is available — the prompt builder omits
/// the context section entirely (AC4 graceful degradation).
/// </param>
/// <param name="QuestionText">
/// The text of the active question (if in quiz mode). Null when no question is active.
/// </param>
/// <param name="WrongAnswer">
/// The student's specific wrong answer (used for the <c>WhyWrong</c> intent).
/// Null for all other intents — the prompt builder omits the wrong-answer slot when null.
/// </param>
/// <param name="SkillId">The active skill identifier.</param>
/// <param name="QuestionId">The active question identifier (null when not in quiz mode).</param>
/// <param name="GradeId">The grade level (anonymous proxy — not the student id).</param>
/// <param name="SubjectId">The subject identifier (maps to <see cref="Subject"/> enum value).</param>
/// <param name="Language">The medium-of-instruction language for this context.</param>
public sealed record LearningContext(
    IReadOnlyList<ChunkDto> Chunks,
    string? QuestionText,
    string? WrongAnswer,
    int SkillId,
    int? QuestionId,
    int GradeId,
    int SubjectId,
    TutorLanguage Language);
