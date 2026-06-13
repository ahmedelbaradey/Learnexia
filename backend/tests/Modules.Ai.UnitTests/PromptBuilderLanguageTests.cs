using FluentAssertions;
using Learnexia.Modules.Ai.Application.PromptBuilder;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Contracts.AiTutor;
using Xunit;

namespace Modules.Ai.UnitTests;

/// <summary>
/// Unit tests for language-variant selection (BE-5 gate).
///
/// Coverage:
///   PBL-01  Arabic context → assembled prompt contains explicit Arabic-output instruction.
///   PBL-02  English context → assembled prompt contains explicit English-output instruction.
///   PBL-03  Arabic context → Arabic tone frame is used (not English frame).
///   PBL-04  English context → English tone frame is used (not Arabic frame).
///   PBL-05  Both language paths produce non-empty prompts for all 4 intents.
/// </summary>
public sealed class PromptBuilderLanguageTests
{
    private static readonly PromptBuilder Sut = new(new TemplateSelector());

    private static PromptContext MakeContext(TutorLanguage language, HelperIntent intent = HelperIntent.Explain)
        => new(
            StudentId: 42,
            Intent: intent,
            Subject: Subject.Math,
            Grade: 4,
            Age: 10,
            Language: language,
            WeakAreas: null,
            Context: null);

    // ── PBL-01: Arabic path → Arabic-output instruction ──────────────────────────

    [Fact(DisplayName = "P303-PBL-01 Arabic language context → assembled prompt contains Arabic-output instruction")]
    public void Build_ArabicLanguage_ContainsArabicInstruction()
    {
        var ctx    = MakeContext(TutorLanguage.Ar);
        var result = Sut.Build(ctx);

        var request = ((PromptBuilderResult.Success)result).Request;
        request.Prompt.Should().Contain("العربية",
            "Arabic-language prompt must contain explicit Arabic-output instruction");
    }

    // ── PBL-02: English path → English-output instruction ────────────────────────

    [Fact(DisplayName = "P303-PBL-02 English language context → assembled prompt contains English-output instruction")]
    public void Build_EnglishLanguage_ContainsEnglishInstruction()
    {
        var ctx    = MakeContext(TutorLanguage.En);
        var result = Sut.Build(ctx);

        var request = ((PromptBuilderResult.Success)result).Request;
        request.Prompt.Should().Contain("respond entirely in English",
            "English-language prompt must contain explicit English-output instruction");
    }

    // ── PBL-03: Arabic → Arabic tone frame used ───────────────────────────────────

    [Fact(DisplayName = "P303-PBL-03 Arabic language → Arabic tone frame is used")]
    public void Build_ArabicLanguage_UsesArabicToneFrame()
    {
        var ctx    = MakeContext(TutorLanguage.Ar);
        var result = Sut.Build(ctx);

        var request = ((PromptBuilderResult.Success)result).Request;
        // The Arabic tone frame contains "إطار النظام" — a unique marker.
        request.Prompt.Should().Contain("إطار النظام",
            "the Arabic tone frame must be prepended for Arabic-language requests");
    }

    // ── PBL-04: English → English tone frame used ────────────────────────────────

    [Fact(DisplayName = "P303-PBL-04 English language → English tone frame is used")]
    public void Build_EnglishLanguage_UsesEnglishToneFrame()
    {
        var ctx    = MakeContext(TutorLanguage.En);
        var result = Sut.Build(ctx);

        var request = ((PromptBuilderResult.Success)result).Request;
        // The English tone frame contains "SYSTEM FRAME" — a unique marker.
        request.Prompt.Should().Contain("SYSTEM FRAME",
            "the English tone frame must be prepended for English-language requests");
    }

    // ── PBL-05: Both language paths produce non-empty prompts for all 4 intents ──

    [Theory(DisplayName = "P303-PBL-05 Both language paths produce non-empty prompts for all 4 intents")]
    [MemberData(nameof(AllLanguagesAndIntents))]
    public void Build_BothLanguages_AllIntents_ProducesNonEmptyPrompt(TutorLanguage language, HelperIntent intent)
    {
        var ctx    = MakeContext(language, intent);
        var result = Sut.Build(ctx);

        result.Should().BeOfType<PromptBuilderResult.Success>(
            $"Language={language}, Intent={intent} must produce a successful result");

        var request = ((PromptBuilderResult.Success)result).Request;
        request.Prompt.Should().NotBeNullOrWhiteSpace(
            "assembled prompt must be non-empty");
    }

    public static IEnumerable<object[]> AllLanguagesAndIntents()
    {
        foreach (TutorLanguage l in Enum.GetValues<TutorLanguage>())
        foreach (HelperIntent i in Enum.GetValues<HelperIntent>())
            yield return [l, i];
    }
}
