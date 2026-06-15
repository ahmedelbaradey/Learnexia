using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Learnexia.Shared.Contracts.Ai;

namespace Learnexia.Modules.Ai.Application.Cache;

/// <summary>
/// Computes stable, deterministic SHA-256 cache keys for all 4 AI response cache entry types.
///
/// <para><strong>Security property:</strong> the key MUST include every safety-/grounding-relevant
/// dimension so one student/context's answer can NEVER be served for a different one.
/// Specifically:</para>
/// <list type="bullet">
///   <item><see cref="ForExplain"/>: SubjectId + ConceptId + AgeBand(JWT grade) + Language + Difficulty + PromptVersion + CurriculumVersion</item>
///   <item><see cref="ForHint"/>: SubjectId + QuestionId + HintLevel + AgeBand(JWT grade) + Language + PromptVersion + CurriculumVersion</item>
///   <item><see cref="ForWhyWrong"/>: SubjectId + QuestionId + SHA256(NormalizedWrongAnswer) + Language + AgeBand(JWT grade) + PromptVersion + CurriculumVersion</item>
///   <item><see cref="ForPractice"/>: SubjectId + SkillKey + VariationIndex + AgeBand(JWT grade) + Language + PromptVersion + CurriculumVersion</item>
/// </list>
///
/// <para><strong>SubjectId (security fix — Finding #1):</strong> all intents now include
/// <c>subjectId</c> so a Math concept/question can never collide with a same-id Science
/// concept/question.</para>
///
/// <para><strong>JWT grade / AgeBand (security fix — Findings #2 and #3):</strong> the grade
/// dimension in the key is always derived from the authenticated student's JWT grade claim
/// (server-trusted, un-spoofable). It is mapped to a coarse age-band so cross-student reuse
/// is scoped to the same developmental cohort (per <c>ai-cost-routing.md §5</c>). Using the
/// lesson/context grade for the key was removed; lesson grade is still used for prompt
/// grounding where appropriate, but the key always uses the JWT band.</para>
///
/// <para>The normalized wrong-answer inner-hash prevents PII leakage through the outer CacheKey
/// (the raw wrong answer is never stored in the key column).</para>
/// </summary>
public static class AiCacheKeyBuilder
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = null };

    /// <summary>
    /// Cache key for <see cref="AiCacheEntryTypeDto.Explain"/> entries.
    /// Tuple: (SubjectId, ConceptId, AgeBand(jwtGrade), Language, Difficulty, PromptVersion, CurriculumVersion).
    /// <para><c>jwtGrade</c> must come from the authenticated student's JWT claim — not from
    /// the lesson context — so the key dimension is server-trusted.</para>
    /// </summary>
    public static string ForExplain(
        int subjectId,
        int conceptId,
        int jwtGrade,
        TutorLanguage language,
        int difficulty,
        string promptVersion,
        string curriculumVersion)
    {
        var tuple = new
        {
            t    = "Explain",
            sid  = subjectId,
            cid  = conceptId,
            ab   = GradeToAgeBand(jwtGrade),
            lang = (int)language,
            diff = difficulty,
            pv   = promptVersion,
            cv   = curriculumVersion,
        };
        return Hash(tuple);
    }

    /// <summary>
    /// Cache key for <see cref="AiCacheEntryTypeDto.Hint"/> entries.
    /// Tuple: (SubjectId, QuestionId, HintLevel, AgeBand(jwtGrade), Language, PromptVersion, CurriculumVersion).
    /// <para><c>jwtGrade</c> must come from the authenticated student's JWT claim — server-trusted.
    /// AgeBand ensures a Grade-1 hint is never served verbatim to a Grade-9 student.</para>
    /// </summary>
    public static string ForHint(
        int subjectId,
        int questionId,
        int hintLevel,
        int jwtGrade,
        TutorLanguage language,
        string promptVersion,
        string curriculumVersion)
    {
        var tuple = new
        {
            t    = "Hint",
            sid  = subjectId,
            qid  = questionId,
            hl   = hintLevel,
            ab   = GradeToAgeBand(jwtGrade),
            lang = (int)language,
            pv   = promptVersion,
            cv   = curriculumVersion,
        };
        return Hash(tuple);
    }

    /// <summary>
    /// Cache key for <see cref="AiCacheEntryTypeDto.WhyWrong"/> entries.
    /// Tuple: (SubjectId, QuestionId, SHA256(NormalizedWrongAnswer), Language, AgeBand(jwtGrade), PromptVersion, CurriculumVersion).
    /// The inner SHA-256 prevents PII exposure in the stored CacheKey column.
    /// <para><c>jwtGrade</c> must come from the authenticated student's JWT claim — server-trusted.</para>
    /// </summary>
    public static string ForWhyWrong(
        int subjectId,
        int questionId,
        string wrongAnswer,
        TutorLanguage language,
        int jwtGrade,
        string promptVersion,
        string curriculumVersion)
    {
        var normalizedWrongAnswerHash = HashRawString(NormalizeWrongAnswer(wrongAnswer));
        var ageBand = GradeToAgeBand(jwtGrade);
        var tuple = new
        {
            t    = "WhyWrong",
            sid  = subjectId,
            qid  = questionId,
            wah  = normalizedWrongAnswerHash,
            lang = (int)language,
            ab   = ageBand,
            pv   = promptVersion,
            cv   = curriculumVersion,
        };
        return Hash(tuple);
    }

    /// <summary>
    /// Cache key for <see cref="AiCacheEntryTypeDto.Practice"/> (SimilarExample) entries.
    /// Tuple: (SubjectId, SkillKey, VariationIndex, AgeBand(jwtGrade), Language, PromptVersion, CurriculumVersion).
    /// <para><c>jwtGrade</c> must come from the authenticated student's JWT claim — server-trusted.</para>
    /// </summary>
    public static string ForPractice(
        int subjectId,
        string skillKey,
        int variationIndex,
        int jwtGrade,
        TutorLanguage language,
        string promptVersion,
        string curriculumVersion)
    {
        var tuple = new
        {
            t    = "Practice",
            sid  = subjectId,
            sk   = skillKey,
            vi   = variationIndex,
            ab   = GradeToAgeBand(jwtGrade),
            lang = (int)language,
            pv   = promptVersion,
            cv   = curriculumVersion,
        };
        return Hash(tuple);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static string Hash(object tuple)
    {
        var json = JsonSerializer.Serialize(tuple, JsonOptions);
        return HashRawString(json);
    }

    private static string HashRawString(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(bytes);
    }

    /// <summary>
    /// Normalizes a wrong answer for the WhyWrong cache key inner hash.
    /// Trim, lower-case, collapse whitespace — the same normalization used by the
    /// no-reveal check in <c>GetHintCommandHandler</c>.
    /// </summary>
    private static string NormalizeWrongAnswer(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return System.Text.RegularExpressions.Regex
            .Replace(input.Trim().ToLowerInvariant(), @"\s+", " ");
    }

    /// <summary>
    /// Maps a grade level to a coarse age-band string so cross-student WhyWrong caching
    /// is limited to the same developmental cohort (per <c>ai-cost-routing.md §5</c>).
    /// </summary>
    private static string GradeToAgeBand(int grade) => grade switch
    {
        <= 3  => "band1",  // Grades 1-3 (~6-9 years)
        <= 6  => "band2",  // Grades 4-6 (~9-12 years)
        <= 9  => "band3",  // Grades 7-9 (~12-15 years)
        _     => "band4",  // Grades 10+ (~15+ years)
    };
}
