using FluentAssertions;
using Learnexia.Modules.Ai.Application.PromptBuilder;
using Learnexia.Shared.Contracts.Ai;
using Xunit;

namespace Modules.Ai.UnitTests;

/// <summary>
/// Unit tests for <see cref="TemplateSelector"/> (BE-4 gate).
///
/// Coverage:
///   TS-01  All 4 subjects × 2 languages × 4 intents → Found result with non-empty template text.
///   TS-02  TemplateSelector has exactly 4 registered subject entries.
///   TS-03  Subject enum has exactly 4 members: {Math, Science, Arabic, English}.
///   TS-04  No Social Studies entry exists in the selector.
///   TS-05  Unsupported (cast) subject → UnsupportedResult (typed result, no silent default — Q3).
///   TS-06  All HelperIntent values are handled per subject (no unhandled-intent throw in production use).
/// </summary>
public sealed class TemplateSelectorTests
{
    private static readonly TemplateSelector Sut = new();

    // ── TS-01: All 4 subjects × 2 languages × 4 intents → Found result ──────────

    [Theory(DisplayName = "P303-TS-01 All 4 subjects × 2 languages × 4 intents return a Found result with text")]
    [MemberData(nameof(AllSubjectLanguageIntentCombinations))]
    public void Select_AllValidCombinations_ReturnFound(Subject subject, TutorLanguage language, HelperIntent intent)
    {
        var result = Sut.Select(subject, intent, language);

        result.Should().BeOfType<TemplateLookupResult.FoundResult>(
            $"Subject={subject}, Language={language}, Intent={intent} must have a registered template");

        ((TemplateLookupResult.FoundResult)result).TemplateText.Should().NotBeNullOrWhiteSpace(
            "template text must not be empty");
    }

    public static IEnumerable<object[]> AllSubjectLanguageIntentCombinations()
    {
        foreach (Subject s in Enum.GetValues<Subject>())
        foreach (TutorLanguage l in Enum.GetValues<TutorLanguage>())
        foreach (HelperIntent i in Enum.GetValues<HelperIntent>())
            yield return [s, l, i];
    }

    // ── TS-02: Exactly 4 registered entries ──────────────────────────────────────

    [Fact(DisplayName = "P303-TS-02 TemplateSelector has exactly 4 registered subject entries")]
    public void TemplateSelector_HasExactlyFourSubjectEntries()
    {
        TemplateSelector.RegisteredSubjectCount.Should().Be(4,
            "exactly 4 subject templates must be registered (Math, Science, Arabic, English)");
    }

    // ── TS-03: Subject enum has exactly 4 members {Math, Science, Arabic, English} ─

    [Fact(DisplayName = "P303-TS-03 Subject enum has exactly 4 members: Math, Science, Arabic, English")]
    public void Subject_EnumHasExactlyFourMembers_And_CorrectNames()
    {
        var members = Enum.GetValues<Subject>();

        members.Length.Should().Be(4,
            "exactly 4 subjects are allowed: Math, Science, Arabic, English (no Social Studies)");

        members.Should().Contain(Subject.Math,    "Math must be a Subject member");
        members.Should().Contain(Subject.Science, "Science must be a Subject member");
        members.Should().Contain(Subject.Arabic,  "Arabic must be a Subject member");
        members.Should().Contain(Subject.English, "English must be a Subject member");
    }

    // ── TS-04: No Social Studies entry ───────────────────────────────────────────

    [Fact(DisplayName = "P303-TS-04 No Social Studies subject member or template exists")]
    public void Subject_DoesNotContainSocialStudies()
    {
        var memberNames = Enum.GetNames<Subject>();

        memberNames.Should().NotContain("SocialStudies",
            "Social Studies is explicitly excluded per product-decision override");
        memberNames.Should().NotContain("Social",
            "no Social Studies variant should be present");

        // Attempting to cast an out-of-range value returns UnsupportedResult — no Social Studies default.
        var bogusSubject = (Subject)99;
        var result = Sut.Select(bogusSubject, HelperIntent.Explain, TutorLanguage.En);
        result.Should().Be(TemplateLookupResult.Unsupported,
            "a value not in the dictionary must return UnsupportedResult, not a default");
    }

    // ── TS-05: Unsupported subject → UnsupportedResult ───────────────────────────

    [Fact(DisplayName = "P303-TS-05 Unsupported subject returns UnsupportedResult (no silent default — Q3)")]
    public void Select_UnsupportedSubject_ReturnsUnsupportedResult()
    {
        var bogusSubject = (Subject)999;

        var result = Sut.Select(bogusSubject, HelperIntent.Hint, TutorLanguage.Ar);

        result.Should().Be(TemplateLookupResult.Unsupported,
            "an unsupported subject must return a typed UnsupportedResult, never silently default");
        result.Should().BeOfType<TemplateLookupResult.UnsupportedResult>();
    }

    // ── TS-06: All 4 HelperIntent values are handled (no throw in selector itself) ─

    [Fact(DisplayName = "P303-TS-06 All HelperIntent enum members have exactly 4 entries")]
    public void HelperIntent_EnumHasExactlyFourMembers()
    {
        var members = Enum.GetValues<HelperIntent>();

        members.Length.Should().Be(4,
            "exactly 4 helper intents are allowed: Explain, Hint, WhyWrong, SimilarExample");

        members.Should().Contain(HelperIntent.Explain,        "Explain must be a HelperIntent member");
        members.Should().Contain(HelperIntent.Hint,           "Hint must be a HelperIntent member");
        members.Should().Contain(HelperIntent.WhyWrong,       "WhyWrong must be a HelperIntent member");
        members.Should().Contain(HelperIntent.SimilarExample, "SimilarExample must be a HelperIntent member");
    }
}
