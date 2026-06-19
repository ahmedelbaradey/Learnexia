using FluentAssertions;
using Learnexia.Modules.Ai.Application.Cache;
using Learnexia.Shared.Contracts.Ai;
using Xunit;

namespace Modules.Ai.UnitTests;

/// <summary>
/// Security regression tests for <see cref="AiCacheKeyBuilder"/> — verifying the three
/// blocking findings raised by the security auditor (Cluster B review):
///
/// <list type="bullet">
///   <item>
///     <strong>Finding #1 (HIGH) — SubjectId absent from key:</strong> a Math concept with
///     the same integer id as a Science concept must never share a cache slot.
///     Tests: CK-01..04.
///   </item>
///   <item>
///     <strong>Finding #2 (MEDIUM) — Hint key had no grade/age dimension:</strong> a hint
///     generated for a Grade-1 student must not be served verbatim to a Grade-9 student.
///     Tests: CK-05..08.
///   </item>
///   <item>
///     <strong>Finding #3 (MEDIUM) — grade sourced from spoofable lesson, not JWT:</strong>
///     key builders now accept <c>jwtGrade</c> (the authenticated student's grade from the
///     JWT claim, un-spoofable). Tests confirm that a different JWT grade in the same
///     developmental age-band yields the same key, while a grade in a different age-band
///     yields a different key — scoped cross-student reuse is preserved while cross-cohort
///     serving is blocked.
///     Tests: CK-09..12.
///   </item>
/// </list>
///
/// <para><strong>Hit-requires-full-tuple invariant:</strong> all HIT tests (CK-13..16)
/// confirm that changing any single dimension of the key changes the output — so a cache
/// hit can only occur when every dimension matches.</para>
///
/// <para><strong>Determinism:</strong> CK-17 confirms that identical inputs always
/// produce the same key (pure function, no hidden state).</para>
/// </summary>
public sealed class AiCacheKeyBuilderSecurityTests
{
    // ── Shared baseline values ────────────────────────────────────────────────────

    private const int SubjectMath    = 1;
    private const int SubjectScience = 2;
    private const int ConceptId      = 42;
    private const int QuestionId     = 99;
    private const int HintLevel      = 1;
    private const int GradeJwt       = 5;   // band2 (4-6)
    private const int GradeBand1     = 2;   // band1 (1-3) — different cohort
    private const int GradeBand3     = 7;   // band3 (7-9) — different cohort
    private const string WrongAnswer  = "3";
    private const int SkillId        = 20;
    private const int VariationIndex  = 0;
    private static readonly TutorLanguage LangAr = TutorLanguage.Ar;
    private static readonly TutorLanguage LangEn = TutorLanguage.En;
    private const string PromptV1 = "v1";
    private const string CurriculumMvp = "mvp";

    // ─── Finding #1: SubjectId in key — cross-subject collision prevention ───────

    /// <summary>CK-01: ForExplain — same ConceptId, different SubjectId → different key.</summary>
    [Fact(DisplayName = "CK-01 ForExplain: same ConceptId + different SubjectId → different cache key (no cross-subject collision)")]
    public void ForExplain_DifferentSubjectId_ProducesDifferentKey()
    {
        var keyMath = AiCacheKeyBuilder.ForExplain(
            subjectId: SubjectMath, conceptId: ConceptId,
            jwtGrade: GradeJwt, language: LangAr,
            difficulty: 0, promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        var keyScience = AiCacheKeyBuilder.ForExplain(
            subjectId: SubjectScience, conceptId: ConceptId,
            jwtGrade: GradeJwt, language: LangAr,
            difficulty: 0, promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        keyMath.Should().NotBe(keyScience,
            "a Math concept explanation must NEVER share a cache key with a same-id Science concept " +
            "(Finding #1: subjectId must be a key dimension)");
    }

    /// <summary>CK-02: ForHint — same QuestionId, different SubjectId → different key.</summary>
    [Fact(DisplayName = "CK-02 ForHint: same QuestionId + different SubjectId → different cache key")]
    public void ForHint_DifferentSubjectId_ProducesDifferentKey()
    {
        var keyMath = AiCacheKeyBuilder.ForHint(
            subjectId: SubjectMath, questionId: QuestionId,
            hintLevel: HintLevel, jwtGrade: GradeJwt, language: LangAr,
            promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        var keyScience = AiCacheKeyBuilder.ForHint(
            subjectId: SubjectScience, questionId: QuestionId,
            hintLevel: HintLevel, jwtGrade: GradeJwt, language: LangAr,
            promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        keyMath.Should().NotBe(keyScience,
            "a Math question hint must NEVER share a cache key with a same-id Science question hint " +
            "(Finding #1: subjectId must be a key dimension)");
    }

    /// <summary>CK-03: ForWhyWrong — same QuestionId, different SubjectId → different key.</summary>
    [Fact(DisplayName = "CK-03 ForWhyWrong: same QuestionId + different SubjectId → different cache key")]
    public void ForWhyWrong_DifferentSubjectId_ProducesDifferentKey()
    {
        var keyMath = AiCacheKeyBuilder.ForWhyWrong(
            subjectId: SubjectMath, questionId: QuestionId,
            wrongAnswer: WrongAnswer, language: LangAr,
            jwtGrade: GradeJwt, promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        var keyScience = AiCacheKeyBuilder.ForWhyWrong(
            subjectId: SubjectScience, questionId: QuestionId,
            wrongAnswer: WrongAnswer, language: LangAr,
            jwtGrade: GradeJwt, promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        keyMath.Should().NotBe(keyScience,
            "a Math question WhyWrong entry must NEVER share a cache key with a same-id Science question " +
            "(Finding #1: subjectId must be a key dimension)");
    }

    /// <summary>CK-04: ForPractice — same SkillId, different SubjectId → different key.</summary>
    [Fact(DisplayName = "CK-04 ForPractice: same SkillId + different SubjectId → different cache key")]
    public void ForPractice_DifferentSubjectId_ProducesDifferentKey()
    {
        var keyMath = AiCacheKeyBuilder.ForPractice(
            subjectId: SubjectMath, skillKey: SkillId.ToString(),
            variationIndex: VariationIndex, jwtGrade: GradeJwt, language: LangAr,
            promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        var keyScience = AiCacheKeyBuilder.ForPractice(
            subjectId: SubjectScience, skillKey: SkillId.ToString(),
            variationIndex: VariationIndex, jwtGrade: GradeJwt, language: LangAr,
            promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        keyMath.Should().NotBe(keyScience,
            "a Math practice entry must NEVER share a cache key with a same-id Science practice entry " +
            "(Finding #1: subjectId must be a key dimension)");
    }

    // ─── Finding #2: AgeBand dimension in Hint key ────────────────────────────────

    /// <summary>CK-05: ForHint — same QuestionId but different age cohort → different key.</summary>
    [Fact(DisplayName = "CK-05 ForHint: same QuestionId + different age-band → different cache key (no cross-cohort serving)")]
    public void ForHint_DifferentAgeBand_ProducesDifferentKey()
    {
        // GradeJwt=5 → band2; GradeBand3=7 → band3 — different developmental cohorts.
        var keyGrade5 = AiCacheKeyBuilder.ForHint(
            subjectId: SubjectMath, questionId: QuestionId,
            hintLevel: HintLevel, jwtGrade: GradeJwt, language: LangAr,
            promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        var keyGrade7 = AiCacheKeyBuilder.ForHint(
            subjectId: SubjectMath, questionId: QuestionId,
            hintLevel: HintLevel, jwtGrade: GradeBand3, language: LangAr,
            promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        keyGrade5.Should().NotBe(keyGrade7,
            "a hint for a Grade-5 student must NEVER be served to a Grade-7 student " +
            "(Finding #2: Hint key must include AgeBand from JWT grade)");
    }

    /// <summary>
    /// CK-06: ForHint — same QuestionId, same age-band (different grades within band) → same key.
    /// This verifies cross-student reuse IS permitted for the same developmental cohort.
    /// </summary>
    [Fact(DisplayName = "CK-06 ForHint: same QuestionId + same age-band (different grade within band) → same key (cross-student reuse permitted within cohort)")]
    public void ForHint_SameAgeBand_ProducesSameKey()
    {
        // Grade 4 and Grade 5 are both in band2 — cross-student reuse within band is safe.
        const int gradeA = 4; // band2
        const int gradeB = 5; // band2

        var keyGradeA = AiCacheKeyBuilder.ForHint(
            subjectId: SubjectMath, questionId: QuestionId,
            hintLevel: HintLevel, jwtGrade: gradeA, language: LangAr,
            promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        var keyGradeB = AiCacheKeyBuilder.ForHint(
            subjectId: SubjectMath, questionId: QuestionId,
            hintLevel: HintLevel, jwtGrade: gradeB, language: LangAr,
            promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        keyGradeA.Should().Be(keyGradeB,
            "students in the same age-band (e.g. Grade 4 and Grade 5 both in band2) may share " +
            "a cached hint — cross-student reuse is intentionally permitted within the same developmental cohort");
    }

    /// <summary>CK-07: ForHint — Grade-1 (band1) vs Grade-9 (band3) → different key.</summary>
    [Fact(DisplayName = "CK-07 ForHint: Grade-1 (band1) vs Grade-9 (band3) → different key (extreme cohort gap)")]
    public void ForHint_Grade1VsGrade9_ProducesDifferentKey()
    {
        // The most extreme case: earliest elementary vs. latest middle school.
        var keyGrade1 = AiCacheKeyBuilder.ForHint(
            subjectId: SubjectMath, questionId: QuestionId,
            hintLevel: HintLevel, jwtGrade: 1, language: LangAr,
            promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        var keyGrade9 = AiCacheKeyBuilder.ForHint(
            subjectId: SubjectMath, questionId: QuestionId,
            hintLevel: HintLevel, jwtGrade: 9, language: LangAr,
            promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        keyGrade1.Should().NotBe(keyGrade9,
            "a hint tailored for a Grade-1 child must NEVER be served to a Grade-9 student " +
            "(age-appropriateness safety property)");
    }

    /// <summary>CK-08: ForExplain — different JWT grade across age-band boundary → different key.</summary>
    [Fact(DisplayName = "CK-08 ForExplain: JWT grade in band1 vs band3 → different key")]
    public void ForExplain_DifferentAgeBand_ProducesDifferentKey()
    {
        var keyBand1 = AiCacheKeyBuilder.ForExplain(
            subjectId: SubjectMath, conceptId: ConceptId,
            jwtGrade: GradeBand1, language: LangAr,
            difficulty: 0, promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        var keyBand3 = AiCacheKeyBuilder.ForExplain(
            subjectId: SubjectMath, conceptId: ConceptId,
            jwtGrade: GradeBand3, language: LangAr,
            difficulty: 0, promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        keyBand1.Should().NotBe(keyBand3,
            "an explanation for a younger cohort (band1) must not be served to an older cohort (band3) " +
            "(Finding #3 fix: key age dimension must be JWT-derived and cohort-scoped)");
    }

    // ─── Finding #3: JWT-grade vs lesson/context grade ───────────────────────────

    /// <summary>
    /// CK-09: ForWhyWrong — JWT grade (band2) vs lesson-grade in band3 → different key.
    /// Verifies that the key is keyed on <c>jwtGrade</c> (the server-trusted value),
    /// NOT on a lesson-provided grade that a student could spoof by picking a higher-grade lesson.
    /// </summary>
    [Fact(DisplayName = "CK-09 ForWhyWrong: JWT grade band2 vs band3 → different key (JWT grade is the key, not lesson grade)")]
    public void ForWhyWrong_DifferentJwtGradeBand_ProducesDifferentKey()
    {
        // A student is in band2 (grade 5). A spoofed/lesson grade could be band3 (grade 7).
        var keyJwtBand2 = AiCacheKeyBuilder.ForWhyWrong(
            subjectId: SubjectMath, questionId: QuestionId,
            wrongAnswer: WrongAnswer, language: LangAr,
            jwtGrade: GradeJwt,   // grade 5 → band2
            promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        var keyLessonBand3 = AiCacheKeyBuilder.ForWhyWrong(
            subjectId: SubjectMath, questionId: QuestionId,
            wrongAnswer: WrongAnswer, language: LangAr,
            jwtGrade: GradeBand3,  // grade 7 → band3 (simulating what a spoofed lesson grade would produce)
            promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        keyJwtBand2.Should().NotBe(keyLessonBand3,
            "a WhyWrong entry keyed on band2 (JWT grade) must not collide with band3 — " +
            "the key dimension is the JWT grade (Finding #3 fix: server-trusted, un-spoofable)");
    }

    /// <summary>
    /// CK-10: ForPractice — different JWT grade bands → different key.
    /// </summary>
    [Fact(DisplayName = "CK-10 ForPractice: different JWT grade bands → different key")]
    public void ForPractice_DifferentJwtGradeBand_ProducesDifferentKey()
    {
        var keyBand2 = AiCacheKeyBuilder.ForPractice(
            subjectId: SubjectMath, skillKey: SkillId.ToString(),
            variationIndex: VariationIndex, jwtGrade: GradeJwt,   // grade 5 → band2
            language: LangAr, promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        var keyBand3 = AiCacheKeyBuilder.ForPractice(
            subjectId: SubjectMath, skillKey: SkillId.ToString(),
            variationIndex: VariationIndex, jwtGrade: GradeBand3, // grade 7 → band3
            language: LangAr, promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        keyBand2.Should().NotBe(keyBand3,
            "a practice entry for band2 students must not be served to band3 students " +
            "(Finding #3 fix: jwtGrade in key)");
    }

    /// <summary>
    /// CK-11: ForExplain — same JWT grade, same content → same key (determinism across calls).
    /// Verifies that the key is stable for the same student cohort.
    /// </summary>
    [Fact(DisplayName = "CK-11 ForExplain: same inputs including same JWT grade → same key (determinism)")]
    public void ForExplain_SameJwtGrade_ProducesSameKey()
    {
        var key1 = AiCacheKeyBuilder.ForExplain(
            subjectId: SubjectMath, conceptId: ConceptId,
            jwtGrade: GradeJwt, language: LangAr,
            difficulty: 0, promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        var key2 = AiCacheKeyBuilder.ForExplain(
            subjectId: SubjectMath, conceptId: ConceptId,
            jwtGrade: GradeJwt, language: LangAr,
            difficulty: 0, promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        key1.Should().Be(key2,
            "cache keys must be deterministic — identical inputs must produce the same key on every call");
    }

    /// <summary>
    /// CK-12: ForHint — JWT grades 4 and 6 (both band2) → same key; JWT grade 7 (band3) → different key.
    /// Validates the age-band scoping boundary correctly.
    /// </summary>
    [Fact(DisplayName = "CK-12 ForHint: grade 4 and 6 (band2) same key; grade 7 (band3) different key — band boundary correct")]
    public void ForHint_AgeBandBoundaryCorrect()
    {
        const int grade4 = 4; // band2
        const int grade6 = 6; // band2 (upper edge)
        const int grade7 = 7; // band3 (lower edge of next band)

        var keyGrade4 = AiCacheKeyBuilder.ForHint(
            subjectId: SubjectMath, questionId: QuestionId,
            hintLevel: HintLevel, jwtGrade: grade4, language: LangAr,
            promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        var keyGrade6 = AiCacheKeyBuilder.ForHint(
            subjectId: SubjectMath, questionId: QuestionId,
            hintLevel: HintLevel, jwtGrade: grade6, language: LangAr,
            promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        var keyGrade7 = AiCacheKeyBuilder.ForHint(
            subjectId: SubjectMath, questionId: QuestionId,
            hintLevel: HintLevel, jwtGrade: grade7, language: LangAr,
            promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        keyGrade4.Should().Be(keyGrade6,
            "Grade 4 and Grade 6 are both in band2 — they should share a cache slot");

        keyGrade4.Should().NotBe(keyGrade7,
            "Grade 4 (band2) and Grade 7 (band3) are in different cohorts — they must NOT share a cache slot");
    }

    // ─── HIT requires the full matching tuple ────────────────────────────────────

    /// <summary>CK-13: ForExplain — changing one dimension (language) → miss.</summary>
    [Fact(DisplayName = "CK-13 ForExplain: changing language alone → different key (full-tuple HIT invariant)")]
    public void ForExplain_DifferentLanguage_ProducesDifferentKey()
    {
        var keyAr = AiCacheKeyBuilder.ForExplain(
            subjectId: SubjectMath, conceptId: ConceptId,
            jwtGrade: GradeJwt, language: LangAr,
            difficulty: 0, promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        var keyEn = AiCacheKeyBuilder.ForExplain(
            subjectId: SubjectMath, conceptId: ConceptId,
            jwtGrade: GradeJwt, language: LangEn,
            difficulty: 0, promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        keyAr.Should().NotBe(keyEn,
            "Arabic and English explanations of the same concept must have different cache keys");
    }

    /// <summary>CK-14: ForHint — changing HintLevel → different key.</summary>
    [Fact(DisplayName = "CK-14 ForHint: changing HintLevel alone → different key (full-tuple HIT invariant)")]
    public void ForHint_DifferentHintLevel_ProducesDifferentKey()
    {
        var keyLevel1 = AiCacheKeyBuilder.ForHint(
            subjectId: SubjectMath, questionId: QuestionId,
            hintLevel: 1, jwtGrade: GradeJwt, language: LangAr,
            promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        var keyLevel2 = AiCacheKeyBuilder.ForHint(
            subjectId: SubjectMath, questionId: QuestionId,
            hintLevel: 2, jwtGrade: GradeJwt, language: LangAr,
            promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        keyLevel1.Should().NotBe(keyLevel2,
            "a level-1 hint and a level-2 hint for the same question must have different cache keys");
    }

    /// <summary>CK-15: ForWhyWrong — changing wrong answer → different key.</summary>
    [Fact(DisplayName = "CK-15 ForWhyWrong: changing WrongAnswer alone → different key (full-tuple HIT invariant)")]
    public void ForWhyWrong_DifferentWrongAnswer_ProducesDifferentKey()
    {
        var keyWrong3 = AiCacheKeyBuilder.ForWhyWrong(
            subjectId: SubjectMath, questionId: QuestionId,
            wrongAnswer: "3", language: LangAr,
            jwtGrade: GradeJwt, promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        var keyWrong5 = AiCacheKeyBuilder.ForWhyWrong(
            subjectId: SubjectMath, questionId: QuestionId,
            wrongAnswer: "5", language: LangAr,
            jwtGrade: GradeJwt, promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        keyWrong3.Should().NotBe(keyWrong5,
            "different wrong answers to the same question produce different explanations — keys must differ");
    }

    /// <summary>CK-16: ForPractice — changing VariationIndex → different key.</summary>
    [Fact(DisplayName = "CK-16 ForPractice: changing VariationIndex alone → different key (full-tuple HIT invariant)")]
    public void ForPractice_DifferentVariationIndex_ProducesDifferentKey()
    {
        var keyV0 = AiCacheKeyBuilder.ForPractice(
            subjectId: SubjectMath, skillKey: SkillId.ToString(),
            variationIndex: 0, jwtGrade: GradeJwt, language: LangAr,
            promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        var keyV1 = AiCacheKeyBuilder.ForPractice(
            subjectId: SubjectMath, skillKey: SkillId.ToString(),
            variationIndex: 1, jwtGrade: GradeJwt, language: LangAr,
            promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        keyV0.Should().NotBe(keyV1,
            "different variation indices must produce different cache keys (practice pool rotation)");
    }

    // ─── Determinism ─────────────────────────────────────────────────────────────

    /// <summary>CK-17: All four builders produce stable output for repeated identical calls.</summary>
    [Fact(DisplayName = "CK-17 All builders are deterministic — identical inputs always produce the same key")]
    public void AllBuilders_AreDeterministic()
    {
        // ForExplain
        var explainA = AiCacheKeyBuilder.ForExplain(SubjectMath, ConceptId, GradeJwt, LangAr, 0, PromptV1, CurriculumMvp);
        var explainB = AiCacheKeyBuilder.ForExplain(SubjectMath, ConceptId, GradeJwt, LangAr, 0, PromptV1, CurriculumMvp);
        explainA.Should().Be(explainB, "ForExplain must be deterministic");

        // ForHint
        var hintA = AiCacheKeyBuilder.ForHint(SubjectMath, QuestionId, HintLevel, GradeJwt, LangAr, PromptV1, CurriculumMvp);
        var hintB = AiCacheKeyBuilder.ForHint(SubjectMath, QuestionId, HintLevel, GradeJwt, LangAr, PromptV1, CurriculumMvp);
        hintA.Should().Be(hintB, "ForHint must be deterministic");

        // ForWhyWrong
        var wwA = AiCacheKeyBuilder.ForWhyWrong(SubjectMath, QuestionId, WrongAnswer, LangAr, GradeJwt, PromptV1, CurriculumMvp);
        var wwB = AiCacheKeyBuilder.ForWhyWrong(SubjectMath, QuestionId, WrongAnswer, LangAr, GradeJwt, PromptV1, CurriculumMvp);
        wwA.Should().Be(wwB, "ForWhyWrong must be deterministic");

        // ForPractice
        var practiceA = AiCacheKeyBuilder.ForPractice(SubjectMath, SkillId.ToString(), VariationIndex, GradeJwt, LangAr, PromptV1, CurriculumMvp);
        var practiceB = AiCacheKeyBuilder.ForPractice(SubjectMath, SkillId.ToString(), VariationIndex, GradeJwt, LangAr, PromptV1, CurriculumMvp);
        practiceA.Should().Be(practiceB, "ForPractice must be deterministic");
    }

    // ─── Cross-intent isolation ───────────────────────────────────────────────────

    /// <summary>
    /// CK-18: Explain and Hint with the same numeric concept/question id → different keys.
    /// The "t" discriminator in the tuple prevents cross-intent collisions.
    /// </summary>
    [Fact(DisplayName = "CK-18 ForExplain and ForHint with the same numeric id → different keys (cross-intent isolation)")]
    public void ForExplain_AndForHint_WithSameNumericId_ProduceDifferentKeys()
    {
        // Use the same numeric id for both — the "t" discriminator must prevent collision.
        const int sameId = 42;

        var explainKey = AiCacheKeyBuilder.ForExplain(
            subjectId: SubjectMath, conceptId: sameId,
            jwtGrade: GradeJwt, language: LangAr,
            difficulty: 0, promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        var hintKey = AiCacheKeyBuilder.ForHint(
            subjectId: SubjectMath, questionId: sameId,
            hintLevel: 0, jwtGrade: GradeJwt, language: LangAr,
            promptVersion: PromptV1, curriculumVersion: CurriculumMvp);

        explainKey.Should().NotBe(hintKey,
            "the 't' discriminator in the key tuple must prevent Explain and Hint from sharing a cache slot " +
            "even when the numeric ids coincide");
    }

    // ─── P3-14a: Recommendation cache key varies with level + style ──────────────

    /// <summary>
    /// CK-19 (P3-14a): ForRecommendation — changing currentLevel → different key.
    /// Required correctness: a level-up event must yield a fresh narration, not a stale cached one.
    /// </summary>
    [Fact(DisplayName = "CK-19 (P3-14a) ForRecommendation: changing currentLevel → different cache key (level-up must invalidate cache)")]
    public void ForRecommendation_DifferentCurrentLevel_ProducesDifferentKey()
    {
        var baseDate    = DateOnly.FromDateTime(DateTime.UtcNow);
        const string contentHash = "abc123";

        var keyLevel5 = AiCacheKeyBuilder.ForRecommendation(
            studentId: 42, recommendationDate: baseDate, recommendationContentHash: contentHash,
            jwtGrade: GradeJwt, language: LangAr, promptVersion: PromptV1, curriculumVersion: CurriculumMvp,
            currentLevel: 5);

        var keyLevel6 = AiCacheKeyBuilder.ForRecommendation(
            studentId: 42, recommendationDate: baseDate, recommendationContentHash: contentHash,
            jwtGrade: GradeJwt, language: LangAr, promptVersion: PromptV1, curriculumVersion: CurriculumMvp,
            currentLevel: 6);

        keyLevel5.Should().NotBe(keyLevel6,
            "P3-14a (OQ-7): a level-up (5→6) must produce a different cache key so the student receives " +
            "a fresh motivational narration rather than a stale cached one");
    }

    /// <summary>
    /// CK-20 (P3-14a): ForRecommendation — same inputs including currentLevel → same key (determinism).
    /// </summary>
    [Fact(DisplayName = "CK-20 (P3-14a) ForRecommendation: same inputs + same level → same key (determinism)")]
    public void ForRecommendation_SameLevel_ProducesSameKey()
    {
        var baseDate    = DateOnly.FromDateTime(DateTime.UtcNow);
        const string contentHash = "abc123";

        var key1 = AiCacheKeyBuilder.ForRecommendation(
            studentId: 42, recommendationDate: baseDate, recommendationContentHash: contentHash,
            jwtGrade: GradeJwt, language: LangAr, promptVersion: PromptV1, curriculumVersion: CurriculumMvp,
            currentLevel: 5);

        var key2 = AiCacheKeyBuilder.ForRecommendation(
            studentId: 42, recommendationDate: baseDate, recommendationContentHash: contentHash,
            jwtGrade: GradeJwt, language: LangAr, promptVersion: PromptV1, curriculumVersion: CurriculumMvp,
            currentLevel: 5);

        key1.Should().Be(key2,
            "ForRecommendation must be deterministic — identical inputs (including same level) produce the same key");
    }

    /// <summary>
    /// CK-21 (P3-14a): ForRecommendation — changing encouragementStyleHint → different key.
    /// Required correctness: a changed profile style must yield a fresh narration.
    /// </summary>
    [Fact(DisplayName = "CK-21 (P3-14a) ForRecommendation: changing encouragementStyleHint → different cache key (profile change must invalidate)")]
    public void ForRecommendation_DifferentEncouragementStyle_ProducesDifferentKey()
    {
        var baseDate    = DateOnly.FromDateTime(DateTime.UtcNow);
        const string contentHash = "abc123";

        var keyBalanced = AiCacheKeyBuilder.ForRecommendation(
            studentId: 42, recommendationDate: baseDate, recommendationContentHash: contentHash,
            jwtGrade: GradeJwt, language: LangAr, promptVersion: PromptV1, curriculumVersion: CurriculumMvp,
            currentLevel: 5, encouragementStyleHint: "Balanced");

        var keyShort = AiCacheKeyBuilder.ForRecommendation(
            studentId: 42, recommendationDate: baseDate, recommendationContentHash: contentHash,
            jwtGrade: GradeJwt, language: LangAr, promptVersion: PromptV1, curriculumVersion: CurriculumMvp,
            currentLevel: 5, encouragementStyleHint: "Short");

        keyBalanced.Should().NotBe(keyShort,
            "P3-14a (OQ-7): a changed encouragement style must produce a different cache key " +
            "so the student receives a fresh narration tailored to their updated profile");
    }
}
