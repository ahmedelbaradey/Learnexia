using FluentAssertions;
using Learnexia.Modules.Ai.Application.PromptBuilder;
using Learnexia.Modules.Ai.Application.PromptBuilder.Templates;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Contracts.AiTutor;
using Xunit;

namespace Modules.Ai.UnitTests;

/// <summary>
/// Unit tests for the full <see cref="PromptBuilder.Build"/> assembly (BE-7 gate).
///
/// Coverage:
///   PBAS-01  AC1 full-assembly: all 6 elements present (grade, age, language instruction,
///            subject template content, weak-areas section, context section, tone frame).
///   PBAS-02  Determinism: same PromptContext → identical prompt string (called twice).
///   PBAS-03  FR-AI-6 guard: no template requests a progression/unlock/mastery decision.
///   PBAS-04  Tone-frame invariant: tone frame is present regardless of subject/language/task.
///   PBAS-05  Unsupported subject → PromptBuilderResult.UnsupportedSubjectResult (Q3).
///   PBAS-06  TutorTask → AiTaskKind routing: Hint/WhyWrong → Hint; Explain/SimilarExample → Explain.
///   PBAS-07  Scope-constraint: all 4 intent templates contain "active skill" or "context" scope wording.
///   PBAS-08  StudentId never appears in the assembled prompt text (PII minimisation).
/// </summary>
public sealed class PromptBuilderAssemblyTests
{
    private static readonly PromptBuilder Sut = new(new TemplateSelector());

    // ── PBAS-01: AC1 Full assembly — all 6 elements ───────────────────────────────

    [Fact(DisplayName = "P303-PBAS-01 AC1: assembled prompt contains all 6 required elements")]
    public void Build_FullContext_ContainsAllSixElements()
    {
        // Grade=4, Age=10, Language=Ar, Subject=Math, Intent=Explain
        var weakAreas = new List<WeakArea>
        {
            new WeakArea(SkillId: 5, SkillName: "Fractions", MasteryScore: 0.3),
        };
        var learningCtx = new LearningContext(
            Chunks: [new ChunkDto("c1", "A fraction represents a part of a whole number.")],
            QuestionText: "What is 1/2 + 1/4?",
            WrongAnswer: null,
            SkillId: 5,
            QuestionId: 10,
            GradeId: 4,
            SubjectId: (int)Subject.Math,
            Language: TutorLanguage.Ar);

        var ctx = new PromptContext(
            StudentId: 999,
            Intent: HelperIntent.Explain,
            Subject: Subject.Math,
            Grade: 4,
            Age: 10,
            Language: TutorLanguage.Ar,
            WeakAreas: weakAreas,
            Context: learningCtx);

        var result  = Sut.Build(ctx);
        result.Should().BeOfType<PromptBuilderResult.Success>("all 6 elements are present");

        var prompt = ((PromptBuilderResult.Success)result).Request.Prompt;

        // 1. Tone frame.
        prompt.Should().Contain("إطار النظام",   "tone frame must be present (Arabic)");

        // 2. Grade injection.
        prompt.Should().Contain("الصف 4",        "grade must be injected");

        // 3. Age injection.
        prompt.Should().Contain("10",            "age must be injected");

        // 4. Language instruction.
        prompt.Should().Contain("العربية",       "Arabic-output instruction must be present");

        // 5. Weak-areas section.
        prompt.Should().Contain("Fractions",     "weak-areas section must contain the skill name");

        // 6. Context section (curriculum chunk text).
        prompt.Should().Contain("fraction represents", "curriculum context chunk text must appear");
    }

    // ── PBAS-02: Determinism ──────────────────────────────────────────────────────

    [Fact(DisplayName = "P303-PBAS-02 Same PromptContext → identical prompt string (determinism invariant)")]
    public void Build_SameContext_ProducesIdenticalResult_Twice()
    {
        var ctx = new PromptContext(
            StudentId: 1,
            Intent: HelperIntent.Hint,
            Subject: Subject.Science,
            Grade: 5,
            Age: 11,
            Language: TutorLanguage.En,
            WeakAreas: [new WeakArea(10, "Photosynthesis", 0.45)],
            Context: new LearningContext(
                Chunks: [new ChunkDto("c2", "Plants produce glucose through photosynthesis.")],
                QuestionText: "What do plants produce?",
                WrongAnswer: null,
                SkillId: 10, QuestionId: 20, GradeId: 5, SubjectId: (int)Subject.Science,
                Language: TutorLanguage.En));

        var result1 = ((PromptBuilderResult.Success)Sut.Build(ctx)).Request.Prompt;
        var result2 = ((PromptBuilderResult.Success)Sut.Build(ctx)).Request.Prompt;

        result1.Should().Be(result2,
            "PromptBuilder.Build is pure and deterministic — same context must produce identical output");
    }

    // ── PBAS-03: FR-AI-6 guard ────────────────────────────────────────────────────

    [Theory(DisplayName = "P303-PBAS-03 FR-AI-6: no template contains progression/unlock/mastery decision wording")]
    [MemberData(nameof(AllSubjectLanguageIntentCombinations))]
    public void Build_NoTemplate_ContainsProgressionDecisionWording(Subject subject, TutorLanguage language, HelperIntent intent)
    {
        var ctx = new PromptContext(
            StudentId: 1,
            Intent: intent,
            Subject: subject,
            Grade: 4,
            Age: 10,
            Language: language,
            WeakAreas: null,
            Context: null);

        var result = Sut.Build(ctx);
        result.Should().BeOfType<PromptBuilderResult.Success>();

        var prompt = ((PromptBuilderResult.Success)result).Request.Prompt;

        // FR-AI-6: the prompt must not REQUEST a progression/unlock/mastery decision.
        // Note: the tone frame contains phrases like "NEVER decide whether the student should advance"
        // as a PROHIBITION (correct). The check here looks for REQUEST forms — imperative prompts
        // that would ask the model TO make a decision, not prohibit one.
        // We look for positive-request forms that would ask the model to perform a progression decision.
        prompt.Should().NotContain("decide if the student should advance",
            $"FR-AI-6: template for {subject}/{language}/{intent} must not request a progression decision");
        prompt.Should().NotContain("tell me if the student should advance",
            $"FR-AI-6 ({subject}/{language}/{intent}): no advancement-decision request allowed");
        prompt.Should().NotContain("determine if student should unlock",
            $"FR-AI-6: template for {subject}/{language}/{intent} must not request an unlock decision");
        prompt.Should().NotContain("assess whether to promote",
            $"FR-AI-6 ({subject}/{language}/{intent}): no promotion-decision request allowed");
        prompt.Should().NotContain("grade level decision for this student",
            $"FR-AI-6: template for {subject}/{language}/{intent} must not request a grade-level decision");
        prompt.Should().NotContain("mastery decision for this student",
            $"FR-AI-6: template for {subject}/{language}/{intent} must not request a mastery decision");

        // Also check that none of the templates OMIT the FR-AI-6 guard instruction in the tone frame.
        // Both tone frames (ar+en) contain prohibitory language — verify by checking the presence of
        // the prohibition (not the request). This is the positive guard: the frame MUST contain the ban.
        if (language == TutorLanguage.En)
        {
            prompt.Should().Contain("Progression decisions belong to the learning system only",
                $"English tone frame must contain the FR-AI-6 prohibition for {subject}/{intent}");
        }
        else
        {
            prompt.Should().Contain("قرارات التقدم تعود للنظام التعليمي",
                $"Arabic tone frame must contain the FR-AI-6 prohibition for {subject}/{intent}");
        }
    }

    // ── PBAS-04: Tone-frame invariant ────────────────────────────────────────────

    [Theory(DisplayName = "P303-PBAS-04 Tone frame is present regardless of subject/language/intent")]
    [MemberData(nameof(AllSubjectLanguageIntentCombinations))]
    public void Build_ToneFrame_AlwaysPresent(Subject subject, TutorLanguage language, HelperIntent intent)
    {
        var ctx = new PromptContext(
            StudentId: 1,
            Intent: intent,
            Subject: subject,
            Grade: 4,
            Age: 10,
            Language: language,
            WeakAreas: null,
            Context: null);

        var result = Sut.Build(ctx);
        result.Should().BeOfType<PromptBuilderResult.Success>();

        var prompt = ((PromptBuilderResult.Success)result).Request.Prompt;

        // Both Arabic and English frames have a unique marker.
        if (language == TutorLanguage.Ar)
            prompt.Should().Contain("إطار النظام",
                $"Arabic tone frame must always be present (subject={subject}, intent={intent})");
        else
            prompt.Should().Contain("SYSTEM FRAME",
                $"English tone frame must always be present (subject={subject}, intent={intent})");
    }

    // ── PBAS-05: Unsupported subject → UnsupportedSubjectResult ─────────────────

    [Fact(DisplayName = "P303-PBAS-05 Unsupported subject → PromptBuilderResult.UnsupportedSubjectResult")]
    public void Build_UnsupportedSubject_ReturnsUnsupportedSubjectResult()
    {
        var bogusSubject = (Subject)999;
        var ctx = new PromptContext(
            StudentId: 1,
            Intent: HelperIntent.Explain,
            Subject: bogusSubject,
            Grade: 4,
            Age: 10,
            Language: TutorLanguage.En,
            WeakAreas: null,
            Context: null);

        var result = Sut.Build(ctx);

        result.Should().BeOfType<PromptBuilderResult.UnsupportedSubjectResult>(
            "an unsupported subject must produce a typed UnsupportedSubjectResult — Q3 decision");

        ((PromptBuilderResult.UnsupportedSubjectResult)result).Subject.Should().Be(bogusSubject);
    }

    // ── PBAS-06: AiTaskKind routing ──────────────────────────────────────────────

    [Theory(DisplayName = "P303-PBAS-06 HelperIntent → AiTaskKind routing")]
    [InlineData(HelperIntent.Hint,           AiTaskKind.Hint)]
    [InlineData(HelperIntent.WhyWrong,       AiTaskKind.Hint)]
    [InlineData(HelperIntent.Explain,        AiTaskKind.Explain)]
    [InlineData(HelperIntent.SimilarExample, AiTaskKind.Explain)]
    public void Build_MapsHelperIntentToCorrectAiTaskKind(HelperIntent intent, AiTaskKind expectedTaskKind)
    {
        var ctx = new PromptContext(
            StudentId: 1,
            Intent: intent,
            Subject: Subject.Math,
            Grade: 4,
            Age: 10,
            Language: TutorLanguage.En,
            WeakAreas: null,
            Context: null);

        var result  = Sut.Build(ctx);
        var request = ((PromptBuilderResult.Success)result).Request;

        request.Task.Should().Be(expectedTaskKind,
            $"HelperIntent.{intent} must map to AiTaskKind.{expectedTaskKind}");
    }

    // ── PBAS-07: Scope-constraint wording in all intent templates ────────────────

    [Theory(DisplayName = "P303-PBAS-07 All 4 intent templates contain active-skill/context scope wording")]
    [MemberData(nameof(AllSubjectLanguageIntentCombinations))]
    public void Build_AllIntentTemplates_ContainScopeConstraintWording(Subject subject, TutorLanguage language, HelperIntent intent)
    {
        var ctx = new PromptContext(
            StudentId: 1,
            Intent: intent,
            Subject: subject,
            Grade: 4,
            Age: 10,
            Language: language,
            WeakAreas: null,
            Context: null);

        var result = Sut.Build(ctx);
        result.Should().BeOfType<PromptBuilderResult.Success>();

        var prompt = ((PromptBuilderResult.Success)result).Request.Prompt;

        // Each template must contain scope-constraining language (active skill or context reference).
        // We check for any of a set of markers covering both ar and en variants.
        var hasScopeWording =
            prompt.Contains("active skill")          ||
            prompt.Contains("context only")          ||
            prompt.Contains("context ONLY")          ||
            prompt.Contains("فقط")                   ||  // Arabic "only"
            prompt.Contains("نطاق المهارة")          ||  // Arabic "skill scope"
            prompt.Contains("only about the active") ||
            prompt.Contains("within the active");

        hasScopeWording.Should().BeTrue(
            $"template for {subject}/{language}/{intent} must contain scope-constraint wording " +
            $"(ai-helper-mvp.md §2 Layer 2) — limits responses to the active skill/question context only");
    }

    // ── PBAS-08: StudentId never in assembled prompt (PII minimisation) ──────────

    [Fact(DisplayName = "P303-PBAS-08 StudentId is never injected into the assembled prompt text (PII minimisation)")]
    public void Build_StudentId_NeverAppearsInPromptText()
    {
        const int studentId = 123456789;
        var ctx = new PromptContext(
            StudentId: studentId,
            Intent: HelperIntent.Explain,
            Subject: Subject.English,
            Grade: 6,
            Age: 12,
            Language: TutorLanguage.En,
            WeakAreas: [new WeakArea(1, "Vocabulary", 0.5)],
            Context: new LearningContext(
                Chunks: [new ChunkDto("c1", "Vocabulary is key to language learning.")],
                QuestionText: "What does 'benevolent' mean?",
                WrongAnswer: null,
                SkillId: 1, QuestionId: 2, GradeId: 6,
                SubjectId: (int)Subject.English,
                Language: TutorLanguage.En));

        var result = Sut.Build(ctx);
        var prompt = ((PromptBuilderResult.Success)result).Request.Prompt;

        prompt.Should().NotContain(studentId.ToString(),
            "StudentId must NEVER appear in the assembled prompt text — PII minimisation rule");
    }

    // ── Shared test data ──────────────────────────────────────────────────────────

    public static IEnumerable<object[]> AllSubjectLanguageIntentCombinations()
    {
        foreach (Subject s in Enum.GetValues<Subject>())
        foreach (TutorLanguage l in Enum.GetValues<TutorLanguage>())
        foreach (HelperIntent i in Enum.GetValues<HelperIntent>())
            yield return [s, l, i];
    }
}
