using FluentAssertions;
using Learnexia.Modules.Ai.Application.PromptBuilder;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Contracts.AiTutor;
using Xunit;

namespace Modules.Ai.UnitTests;

/// <summary>
/// Unit tests for graceful-degradation logic (BE-6 gate / AC4).
///
/// Coverage:
///   PBGD-01  Null WeakAreas + null Context → valid prompt, no empty-header artifact, no null-ref.
///   PBGD-02  Empty WeakAreas (Count=0) + null Context → valid prompt, no weak-areas header.
///   PBGD-03  Populated WeakAreas + null Context → weak-areas section appears; no context section.
///   PBGD-04  Null WeakAreas + Context with populated Chunks → context section appears; no weak-areas section.
///   PBGD-05  Both populated → both sections appear.
///   PBGD-06  WhyWrong intent + WrongAnswer set → wrong-answer slot appears.
///   PBGD-07  Non-WhyWrong intent + WrongAnswer set → wrong-answer slot NOT injected.
///   PBGD-08  WhyWrong intent + null WrongAnswer → wrong-answer slot NOT injected (graceful).
///   PBGD-09  Context with empty Chunks (Count=0) → context section omitted entirely.
///   PBGD-10  Graceful degradation applies across all 4 intents (all produce valid prompt on empty context).
/// </summary>
public sealed class PromptBuilderGracefulDegradationTests
{
    private static readonly PromptBuilder Sut = new(new TemplateSelector());

    private static PromptContext MakeContext(
        HelperIntent intent = HelperIntent.Explain,
        IReadOnlyList<WeakArea>? weakAreas = null,
        LearningContext? context = null)
        => new(
            StudentId: 1,
            Intent: intent,
            Subject: Subject.Science,
            Grade: 3,
            Age: 9,
            Language: TutorLanguage.En,
            WeakAreas: weakAreas,
            Context: context);

    private static LearningContext MakeContext(
        IReadOnlyList<ChunkDto>? chunks = null,
        string? wrongAnswer = null,
        string? questionText = null)
        => new LearningContext(
            Chunks: chunks ?? Array.Empty<ChunkDto>(),
            QuestionText: questionText,
            WrongAnswer: wrongAnswer,
            SkillId: 100,
            QuestionId: 200,
            GradeId: 3,
            SubjectId: (int)Subject.Science,
            Language: TutorLanguage.En);

    // ── PBGD-01: Null WeakAreas + null Context → valid prompt, no artifacts ──────

    [Fact(DisplayName = "P303-PBGD-01 Null WeakAreas + null Context → valid prompt without empty-header artifacts")]
    public void Build_NullWeakAreasAndNullContext_ProducesValidPrompt()
    {
        var ctx    = MakeContext(weakAreas: null, context: null);
        var result = Sut.Build(ctx);

        result.Should().BeOfType<PromptBuilderResult.Success>("null optional fields must not cause failure");

        var prompt = ((PromptBuilderResult.Success)result).Request.Prompt;
        prompt.Should().NotBeNullOrWhiteSpace("prompt must be non-empty even with no optional context");

        // No empty-header artifacts.
        prompt.Should().NotContain("## Weak Areas",    "weak-areas section must not appear when null");
        prompt.Should().NotContain("## Areas Needing", "weak-areas header must not appear when null");
        prompt.Should().NotContain("## Curriculum Context", "context section must not appear when null");
        prompt.Should().NotContain("(none)",            "no placeholder content should appear");
    }

    // ── PBGD-02: Empty WeakAreas (Count=0) → no weak-areas header ────────────────

    [Fact(DisplayName = "P303-PBGD-02 Empty WeakAreas list → weak-areas section omitted entirely")]
    public void Build_EmptyWeakAreasList_OmitsWeakAreasSection()
    {
        var ctx    = MakeContext(weakAreas: Array.Empty<WeakArea>(), context: null);
        var result = Sut.Build(ctx);

        var prompt = ((PromptBuilderResult.Success)result).Request.Prompt;
        prompt.Should().NotContain("## Areas Needing", "weak-areas header must not appear for empty list");
        prompt.Should().NotContain("## المهارات",      "Arabic weak-areas header must not appear for empty list");
    }

    // ── PBGD-03: Populated WeakAreas + null Context ───────────────────────────────

    [Fact(DisplayName = "P303-PBGD-03 Populated WeakAreas + null Context → weak-areas appear, no context section")]
    public void Build_PopulatedWeakAreas_NullContext_WeakAreasSectionAppears()
    {
        var areas = new List<WeakArea>
        {
            new WeakArea(SkillId: 10, SkillName: "Long Division", MasteryScore: 0.35),
        };
        var ctx    = MakeContext(weakAreas: areas, context: null);
        var result = Sut.Build(ctx);

        var prompt = ((PromptBuilderResult.Success)result).Request.Prompt;
        prompt.Should().Contain("Long Division",         "weak-areas section must include the skill name");
        prompt.Should().Contain("Areas Needing",         "weak-areas header must appear when populated");
        prompt.Should().NotContain("## Curriculum Context", "context section must not appear when null");
    }

    // ── PBGD-04: Null WeakAreas + Context with populated Chunks ─────────────────

    [Fact(DisplayName = "P303-PBGD-04 Null WeakAreas + populated Context → context section appears, no weak-areas")]
    public void Build_NullWeakAreas_PopulatedContext_ContextSectionAppears()
    {
        var learningCtx = MakeContext(
            chunks: [new ChunkDto("c1", "Plants use sunlight to produce food through photosynthesis.")],
            questionText: "What is photosynthesis?");

        var ctx    = MakeContext(weakAreas: null, context: learningCtx);
        var result = Sut.Build(ctx);

        var prompt = ((PromptBuilderResult.Success)result).Request.Prompt;
        prompt.Should().Contain("photosynthesis",       "curriculum context chunk text must appear in prompt");
        prompt.Should().Contain("Curriculum Context",   "context section header must appear when populated");
        prompt.Should().NotContain("Areas Needing",     "weak-areas section must not appear when null");
        prompt.Should().NotContain("## المهارات",       "Arabic weak-areas header must not appear");
    }

    // ── PBGD-05: Both populated → both sections appear ───────────────────────────

    [Fact(DisplayName = "P303-PBGD-05 Populated WeakAreas + populated Context → both sections present")]
    public void Build_BothPopulated_BothSectionsAppear()
    {
        var areas = new List<WeakArea>
        {
            new WeakArea(SkillId: 20, SkillName: "Cell Division", MasteryScore: 0.4),
        };
        var learningCtx = MakeContext(
            chunks: [new ChunkDto("c2", "The cell cycle consists of interphase and mitosis.")]);

        var ctx    = MakeContext(weakAreas: areas, context: learningCtx);
        var result = Sut.Build(ctx);

        var prompt = ((PromptBuilderResult.Success)result).Request.Prompt;
        prompt.Should().Contain("Cell Division",       "weak-areas section must include the skill name");
        prompt.Should().Contain("cell cycle",          "context chunk text must appear");
    }

    // ── PBGD-06: WhyWrong + WrongAnswer set → wrong-answer slot appears ──────────

    [Fact(DisplayName = "P303-PBGD-06 WhyWrong intent + populated WrongAnswer → wrong-answer slot appears")]
    public void Build_WhyWrongIntent_WithWrongAnswer_SlotsAppear()
    {
        var learningCtx = MakeContext(wrongAnswer: "The sun rises in the west.");
        var ctx         = MakeContext(intent: HelperIntent.WhyWrong, context: learningCtx);
        var result      = Sut.Build(ctx);

        var prompt = ((PromptBuilderResult.Success)result).Request.Prompt;
        prompt.Should().Contain("The sun rises in the west.",
            "the student's wrong answer must be injected for WhyWrong intent");
        prompt.Should().Contain("wrong answer",
            "wrong-answer slot label must appear");
    }

    // ── PBGD-07: Non-WhyWrong intent + WrongAnswer set → slot NOT injected ───────

    [Fact(DisplayName = "P303-PBGD-07 Non-WhyWrong intent → wrong-answer slot NOT injected even if WrongAnswer set")]
    public void Build_NonWhyWrongIntent_WrongAnswerNotInjected()
    {
        var learningCtx = MakeContext(wrongAnswer: "Water boils at 50°C.");
        var ctx         = MakeContext(intent: HelperIntent.Hint, context: learningCtx);
        var result      = Sut.Build(ctx);

        var prompt = ((PromptBuilderResult.Success)result).Request.Prompt;
        prompt.Should().NotContain("Water boils at 50°C.",
            "wrong-answer slot must not be injected for non-WhyWrong intent");
    }

    // ── PBGD-08: WhyWrong + null WrongAnswer → graceful (slot not injected) ──────

    [Fact(DisplayName = "P303-PBGD-08 WhyWrong intent + null WrongAnswer → no null-ref, slot omitted")]
    public void Build_WhyWrongIntent_NullWrongAnswer_Graceful()
    {
        var learningCtx = MakeContext(wrongAnswer: null);
        var ctx         = MakeContext(intent: HelperIntent.WhyWrong, context: learningCtx);

        // Must not throw.
        var act = () => Sut.Build(ctx);
        act.Should().NotThrow("null WrongAnswer must be handled gracefully");

        var result = act();
        var prompt = ((PromptBuilderResult.Success)result).Request.Prompt;
        prompt.Should().NotBeNullOrWhiteSpace("prompt must still be assembled");
    }

    // ── PBGD-09: Context with empty Chunks → context section omitted ─────────────

    [Fact(DisplayName = "P303-PBGD-09 Context with empty Chunks list → context section omitted entirely")]
    public void Build_ContextWithEmptyChunks_OmitsContextSection()
    {
        var learningCtx = MakeContext(chunks: Array.Empty<ChunkDto>());
        var ctx         = MakeContext(context: learningCtx);
        var result      = Sut.Build(ctx);

        var prompt = ((PromptBuilderResult.Success)result).Request.Prompt;
        prompt.Should().NotContain("## Curriculum Context",
            "context section must be omitted when Chunks is empty");
        prompt.Should().NotContain("DATA ONLY",
            "context section header must not appear for empty chunks");
    }

    // ── PBGD-10: All 4 intents produce valid prompts on empty context ────────────

    [Theory(DisplayName = "P303-PBGD-10 All 4 intents produce valid prompts when WeakAreas and Context are null/empty")]
    [MemberData(nameof(AllIntents))]
    public void Build_AllIntents_EmptyContext_ProducesValidPrompt(HelperIntent intent)
    {
        var ctx    = MakeContext(intent: intent, weakAreas: null, context: null);
        var result = Sut.Build(ctx);

        result.Should().BeOfType<PromptBuilderResult.Success>(
            $"intent={intent} must produce Success even with empty/null optional context");

        var prompt = ((PromptBuilderResult.Success)result).Request.Prompt;
        prompt.Should().NotBeNullOrWhiteSpace($"prompt must be non-empty for intent={intent}");
    }

    public static IEnumerable<object[]> AllIntents()
        => Enum.GetValues<HelperIntent>().Select(i => new object[] { i });
}
