using FluentAssertions;
using Learnexia.Modules.Learning.Application.Helpers;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Xunit;

namespace Modules.Learning.UnitTests;

/// <summary>
/// Unit tests for <see cref="SubjectLanguageResolver.Resolve"/> and
/// <see cref="LearningLanguageClaimAccessor.GetLearningLanguage"/>.
///
/// P8-03-BE-2 — required cases (4 codes × 2 media = 8):
///   ARABIC/ar   → Ar (pinned)
///   ARABIC/en   → Ar (pinned)
///   ENGLISH/ar  → En (pinned)
///   ENGLISH/en  → En (pinned)
///   MATH/ar     → Ar (follows learner)
///   MATH/en     → En (follows learner)
///   SCIENCE/ar  → Ar (follows learner)
///   SCIENCE/en  → En (follows learner)
///
/// P8-03-BE-1 — claim accessor fallback:
///   claim = "ar"     → ContentLanguage.Ar
///   claim = "en"     → ContentLanguage.En
///   claim = null     → ContentLanguage.Ar + LogWarn
///   claim = "fr"     → ContentLanguage.Ar + LogWarn
/// </summary>
public sealed class SubjectLanguageResolverTests
{
    // ══════════════════════════════════════════════════════════════════════════════
    // SubjectLanguageResolver — 8 required cases
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Resolve_Arabic_ArLearner_ReturnsAr()
    {
        SubjectLanguageResolver.Resolve(SubjectCode.ARABIC, ContentLanguage.Ar)
            .Should().Be(ContentLanguage.Ar, "ARABIC is always Ar regardless of learner language");
    }

    [Fact]
    public void Resolve_Arabic_EnLearner_ReturnsAr()
    {
        SubjectLanguageResolver.Resolve(SubjectCode.ARABIC, ContentLanguage.En)
            .Should().Be(ContentLanguage.Ar, "ARABIC is pinned to Ar even for En-medium learners");
    }

    [Fact]
    public void Resolve_English_ArLearner_ReturnsEn()
    {
        SubjectLanguageResolver.Resolve(SubjectCode.ENGLISH, ContentLanguage.Ar)
            .Should().Be(ContentLanguage.En, "ENGLISH is always En regardless of learner language");
    }

    [Fact]
    public void Resolve_English_EnLearner_ReturnsEn()
    {
        SubjectLanguageResolver.Resolve(SubjectCode.ENGLISH, ContentLanguage.En)
            .Should().Be(ContentLanguage.En, "ENGLISH is pinned to En even for En-medium learners");
    }

    [Fact]
    public void Resolve_Math_ArLearner_ReturnsAr()
    {
        SubjectLanguageResolver.Resolve(SubjectCode.MATH, ContentLanguage.Ar)
            .Should().Be(ContentLanguage.Ar, "MATH follows the learner's language (ar → Ar)");
    }

    [Fact]
    public void Resolve_Math_EnLearner_ReturnsEn()
    {
        SubjectLanguageResolver.Resolve(SubjectCode.MATH, ContentLanguage.En)
            .Should().Be(ContentLanguage.En, "MATH follows the learner's language (en → En)");
    }

    [Fact]
    public void Resolve_Science_ArLearner_ReturnsAr()
    {
        SubjectLanguageResolver.Resolve(SubjectCode.SCIENCE, ContentLanguage.Ar)
            .Should().Be(ContentLanguage.Ar, "SCIENCE follows the learner's language (ar → Ar)");
    }

    [Fact]
    public void Resolve_Science_EnLearner_ReturnsEn()
    {
        SubjectLanguageResolver.Resolve(SubjectCode.SCIENCE, ContentLanguage.En)
            .Should().Be(ContentLanguage.En, "SCIENCE follows the learner's language (en → En)");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // SubjectLanguageResolver — edge-case matrix (product spec, AC-9)
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void EdgeCase_ArMediumStudent_SeesExpectedLanguages()
    {
        // Arabic-medium: Math(Ar), Science(Ar), Arabic(Ar), English(En)
        SubjectLanguageResolver.Resolve(SubjectCode.MATH,    ContentLanguage.Ar).Should().Be(ContentLanguage.Ar);
        SubjectLanguageResolver.Resolve(SubjectCode.SCIENCE, ContentLanguage.Ar).Should().Be(ContentLanguage.Ar);
        SubjectLanguageResolver.Resolve(SubjectCode.ARABIC,  ContentLanguage.Ar).Should().Be(ContentLanguage.Ar);
        SubjectLanguageResolver.Resolve(SubjectCode.ENGLISH, ContentLanguage.Ar).Should().Be(ContentLanguage.En);
    }

    [Fact]
    public void EdgeCase_EnMediumStudent_SeesExpectedLanguages()
    {
        // English-medium: Math(En), Science(En), Arabic(Ar), English(En)
        SubjectLanguageResolver.Resolve(SubjectCode.MATH,    ContentLanguage.En).Should().Be(ContentLanguage.En);
        SubjectLanguageResolver.Resolve(SubjectCode.SCIENCE, ContentLanguage.En).Should().Be(ContentLanguage.En);
        SubjectLanguageResolver.Resolve(SubjectCode.ARABIC,  ContentLanguage.En).Should().Be(ContentLanguage.Ar);
        SubjectLanguageResolver.Resolve(SubjectCode.ENGLISH, ContentLanguage.En).Should().Be(ContentLanguage.En);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // LearningLanguageClaimAccessor — claim parsing + fallback
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ClaimAccessor_ClaimAr_ReturnsArWithoutWarning()
    {
        var (currentUser, logger) = BuildMocks("ar");

        var result = LearningLanguageClaimAccessor.GetLearningLanguage(currentUser, logger);

        result.Should().Be(ContentLanguage.Ar);
        logger.WarnCallCount.Should().Be(0, "a valid 'ar' claim should not trigger a fallback warning");
    }

    [Fact]
    public void ClaimAccessor_ClaimEn_ReturnsEnWithoutWarning()
    {
        var (currentUser, logger) = BuildMocks("en");

        var result = LearningLanguageClaimAccessor.GetLearningLanguage(currentUser, logger);

        result.Should().Be(ContentLanguage.En);
        logger.WarnCallCount.Should().Be(0, "a valid 'en' claim should not trigger a fallback warning");
    }

    [Fact]
    public void ClaimAccessor_ClaimAbsent_FallsBackToArAndLogsWarn()
    {
        var (currentUser, logger) = BuildMocks(null);

        var result = LearningLanguageClaimAccessor.GetLearningLanguage(currentUser, logger);

        result.Should().Be(ContentLanguage.Ar, "absent claim must fall back to Arabic-first default");
        logger.WarnCallCount.Should().Be(1, "absent claim must emit exactly one fallback warning");
    }

    [Fact]
    public void ClaimAccessor_ClaimUnrecognised_FallsBackToArAndLogsWarn()
    {
        var (currentUser, logger) = BuildMocks("fr");

        var result = LearningLanguageClaimAccessor.GetLearningLanguage(currentUser, logger);

        result.Should().Be(ContentLanguage.Ar, "unrecognised claim value must fall back to Arabic-first default");
        logger.WarnCallCount.Should().Be(1, "unrecognised claim must emit exactly one fallback warning");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════════

    private static (StubCurrentUserService currentUser, SpyLoggerManager logger) BuildMocks(string? claimValue)
    {
        var currentUser = new StubCurrentUserService(claimValue);
        var logger = new SpyLoggerManager();
        return (currentUser, logger);
    }

    // ── Stubs ─────────────────────────────────────────────────────────────────────

    private sealed class StubCurrentUserService : ICurrentUserService
    {
        private readonly string? _claimValue;

        public StubCurrentUserService(string? claimValue) => _claimValue = claimValue;

        public int? UserId => null;
        public string? UserName => null;
        public IList<string> Roles => Array.Empty<string>();

        public string? GetClaimValue(string claimType)
            => claimType == LearningLanguageClaimAccessor.ClaimType ? _claimValue : null;
    }

    private sealed class SpyLoggerManager : ILoggerManager
    {
        public int WarnCallCount { get; private set; }

        public void LogInfo(string message) { }
        public void LogDebug(string message) { }
        public void LogError(Exception? ex, string message) { }

        public void LogWarn(string message) => WarnCallCount++;
    }
}
