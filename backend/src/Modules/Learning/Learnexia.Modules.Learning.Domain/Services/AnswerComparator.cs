using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Domain.Services;

/// <summary>
/// Pure static helper for server-side answer correctness comparison.
/// One method per <see cref="QuestionType"/> — plain switch, no Strategy/Factory (CLAUDE.md rule 8).
/// Mirrors the shape of <see cref="SkillGraphValidator"/> (static, no DI).
/// </summary>
public static class AnswerComparator
{
    /// <summary>
    /// Compares a student's submitted answer payload to the question's stored correct answer
    /// using per-<see cref="QuestionType"/> rules.
    /// Returns <c>true</c> if the answer is correct.
    /// Treats null/empty payload as incorrect (does not throw).
    /// </summary>
    /// <param name="type">The <see cref="QuestionType"/> that governs the comparison semantics.</param>
    /// <param name="studentPayload">The raw answer payload submitted by the student. May be null or empty.</param>
    /// <param name="correctAnswer">The authoritative correct answer stored on the question. May be null.</param>
    /// <returns><c>true</c> when the student's answer matches the correct answer under the type's rules.</returns>
    public static bool AreEqual(QuestionType type, string? studentPayload, string? correctAnswer)
    {
        // Guard: treat null/empty as incorrect (do not throw)
        if (string.IsNullOrWhiteSpace(studentPayload) || string.IsNullOrWhiteSpace(correctAnswer))
            return false;

        return type switch
        {
            QuestionType.MCQ =>
                string.Equals(studentPayload.Trim(), correctAnswer.Trim(), StringComparison.OrdinalIgnoreCase),

            QuestionType.TrueFalse =>
                bool.TryParse(studentPayload.Trim(), out var studentBool)
                && bool.TryParse(correctAnswer.Trim(), out var correctBool)
                && studentBool == correctBool,

            QuestionType.FillInBlank =>
                string.Equals(studentPayload.Trim(), correctAnswer.Trim(), StringComparison.OrdinalIgnoreCase),

            // TODO P2-07.b: define Matching answer payload shape with FE;
            // current behavior falls through to string compare until the wire-shape is defined.
            QuestionType.Matching =>
                string.Equals(studentPayload.Trim(), correctAnswer.Trim(), StringComparison.OrdinalIgnoreCase),

            _ => false,
        };
    }
}
