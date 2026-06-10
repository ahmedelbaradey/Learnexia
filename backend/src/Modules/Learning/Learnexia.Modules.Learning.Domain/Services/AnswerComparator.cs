using System.Text.Json;
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

        // CorrectAnswer is persisted as jsonb (JSON-encoded): the string "6" is stored as the
        // 3-char text "6", "true" as "true", etc. The student submits the RAW value (6 / true).
        // Decode the JSON-string wrapping on both sides before comparing so MCQ / TrueFalse /
        // FillInBlank grade correctly (fixes DEF-P205FE-01 — previously every answer graded wrong).
        var student = NormalizeJsonScalar(studentPayload);
        var correct = NormalizeJsonScalar(correctAnswer);

        return type switch
        {
            QuestionType.MCQ =>
                string.Equals(student, correct, StringComparison.OrdinalIgnoreCase),

            QuestionType.TrueFalse =>
                bool.TryParse(student, out var studentBool)
                && bool.TryParse(correct, out var correctBool)
                && studentBool == correctBool,

            QuestionType.FillInBlank =>
                string.Equals(student, correct, StringComparison.OrdinalIgnoreCase),

            // TODO P2-07.b: define Matching answer payload shape with FE;
            // current behavior falls through to string compare until the wire-shape is defined.
            QuestionType.Matching =>
                string.Equals(student, correct, StringComparison.OrdinalIgnoreCase),

            _ => false,
        };
    }

    /// <summary>
    /// Unwraps a JSON-encoded scalar string literal (<c>"6"</c> → <c>6</c>, <c>"true"</c> → <c>true</c>)
    /// so a jsonb-stored <c>CorrectAnswer</c> compares equal to the student's raw payload.
    /// Trims; leaves non-quoted values (and JSON arrays/objects, e.g. Matching) unchanged.
    /// </summary>
    private static string NormalizeJsonScalar(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            try
            {
                return JsonSerializer.Deserialize<string>(trimmed)?.Trim() ?? trimmed;
            }
            catch (JsonException)
            {
                return trimmed;
            }
        }
        return trimmed;
    }
}
