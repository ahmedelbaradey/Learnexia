using System.Text.Json;
using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Domain.Services;

/// <summary>
/// Pure static helper for server-side answer correctness comparison.
/// One method per <see cref="QuestionType"/> — plain switch, no Strategy/Factory (CLAUDE.md rule 8).
/// Mirrors the shape of <see cref="SkillGraphValidator"/> (static, no DI).
///
/// <para><b>Matching wire contract (CO-BE-1/CO-BE-2, locked decision — do not redesign):</b></para>
/// <para>
/// Question content (<c>QuizQuestion.Options</c>, jsonb, student-visible):
/// <c>{"left":[{"id":"l1","text":"…"}],"right":[{"id":"r1","text":"…"}]}</c> — stable item ids,
/// localized display text. Authoring should store <c>right</c> in non-aligned order (and the FE
/// shuffles) so array position never leaks the pairing.
/// </para>
/// <para>
/// Stored correct answer (<c>QuizQuestion.CorrectAnswer</c>, jsonb, never sent to students):
/// <c>{"pairs":[{"leftId":"l1","rightId":"r1"}]}</c>.
/// </para>
/// <para>
/// Student answer payload (<c>SubmitAnswerCommand.AnswerPayload</c>):
/// <c>{"pairs":[{"leftId":"…","rightId":"…"}],"attemptOrder":1,"timeMs":12345}</c>.
/// Correctness = <b>order-independent pair-set equality</b>: the submitted set of
/// (leftId, rightId) pairs must equal the stored set regardless of array order.
/// <c>attemptOrder</c>/<c>timeMs</c> are attempt metadata only and never affect correctness.
/// Ids compare ordinal (case-sensitive). A duplicate <c>leftId</c>, a missing/extra pair, or an
/// unparseable payload grades as incorrect — this method never throws. Structurally invalid JSON
/// is additionally rejected upstream with 422 by <c>SubmitAnswerValidation</c>.
/// </para>
/// </summary>
public static class AnswerComparator
{
    // Matching wire-contract JSON property names (technical identifiers, not user-facing text).
    private const string PairsProperty = "pairs";
    private const string LeftIdProperty = "leftId";
    private const string RightIdProperty = "rightId";

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

            // CO-BE-2 (closes TODO P2-07.b): order-independent pair-set equality on the
            // {"pairs":[{"leftId","rightId"}]} contract documented on this class.
            QuestionType.Matching =>
                MatchingPairSetsEqual(student, correct),

            _ => false,
        };
    }

    /// <summary>
    /// Order-independent pair-set equality for <see cref="QuestionType.Matching"/>.
    /// Parses both sides as the <c>{"pairs":[{"leftId","rightId"}]}</c> contract and compares the
    /// (leftId, rightId) sets. Any malformed/incomplete side — including a duplicate leftId in the
    /// submission — grades as incorrect. Never throws.
    /// </summary>
    private static bool MatchingPairSetsEqual(string studentJson, string correctJson)
    {
        var studentPairs = TryParseMatchingPairs(studentJson);
        var correctPairs = TryParseMatchingPairs(correctJson);

        // An empty correct set means the question content is broken — never grade it as correct.
        if (studentPairs is null || correctPairs is null || correctPairs.Count == 0)
            return false;

        return studentPairs.Count == correctPairs.Count && studentPairs.SetEquals(correctPairs);
    }

    /// <summary>
    /// Parses a Matching pairs document into a set of (LeftId, RightId) tuples.
    /// Returns <c>null</c> when the JSON is malformed, the root is not an object, the
    /// <c>pairs</c> array is missing, an element lacks <c>leftId</c>/<c>rightId</c>, or a
    /// <c>leftId</c> appears more than once. Extra properties (e.g. the attempt metadata
    /// <c>attemptOrder</c>/<c>timeMs</c>) are ignored. Never throws.
    /// </summary>
    private static HashSet<(string LeftId, string RightId)>? TryParseMatchingPairs(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty(PairsProperty, out var pairsElement) ||
                pairsElement.ValueKind != JsonValueKind.Array)
                return null;

            var pairs = new HashSet<(string LeftId, string RightId)>();
            var seenLeftIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var pairElement in pairsElement.EnumerateArray())
            {
                if (pairElement.ValueKind != JsonValueKind.Object)
                    return null;

                var leftId = TryReadId(pairElement, LeftIdProperty);
                var rightId = TryReadId(pairElement, RightIdProperty);
                if (leftId is null || rightId is null)
                    return null;

                // A left item may be paired only once — duplicates invalidate the whole document.
                if (!seenLeftIds.Add(leftId))
                    return null;

                pairs.Add((leftId, rightId));
            }

            return pairs;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads an id property from a pair element. Accepts a non-empty JSON string (trimmed) or a
    /// JSON number (raw text, so <c>1</c> and <c>"1"</c> compare equal). Returns <c>null</c> for
    /// anything else.
    /// </summary>
    private static string? TryReadId(JsonElement pairElement, string propertyName)
    {
        if (!pairElement.TryGetProperty(propertyName, out var idElement))
            return null;

        return idElement.ValueKind switch
        {
            JsonValueKind.String when !string.IsNullOrWhiteSpace(idElement.GetString())
                => idElement.GetString()!.Trim(),
            JsonValueKind.Number => idElement.GetRawText(),
            _ => null,
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
