using FluentAssertions;
using Learnexia.Modules.Ai.Application.Cache;
using Learnexia.Modules.Ai.Application.Features.Explain.Commands;
using Learnexia.Modules.Ai.Application.PromptBuilder;
using Learnexia.Modules.Ai.Application.Services;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Contracts.AiTutor;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Resources;
using Xunit;

namespace Modules.Ai.UnitTests;

/// <summary>
/// Regression tests for AI-DEFECT-1: verify that the <see cref="ExplainConceptCommandHandler"/>
/// correctly differentiates its cache key by the JWT grade claim when students are in
/// different age-bands.
///
/// Before the fix, <c>TryResolveProfile</c> always fell back to grade=4 (band2) because
/// the "Grade" JWT claim was never minted.  After the fix, the claim is emitted at
/// token-mint time and read here via the mocked <see cref="ICurrentUserService"/>.
///
/// <para>These tests prove the handler path end-to-end at the unit level:
/// grade claim → <see cref="AiCacheKeyBuilder.ForExplain"/> → distinct cache keys
/// for different age-bands.</para>
///
/// Tests:
///   GD-01  Grade=2 (band1) vs grade=7 (band3) → different cache keys (cross-cohort isolation).
///   GD-02  Grade=4 (band2) vs grade=5 (band2) → same cache key (within-cohort reuse preserved).
///   GD-03  Handler with missing Grade claim falls back to grade=4 (band2) defensive default.
///   GD-04  Handler with Grade="2" (band1) produces a band1 cache key, not the band2 default.
/// </summary>
public sealed class AiGradeClaimCacheDifferentiationTests
{
    // ── Shared constants ─────────────────────────────────────────────────────────

    private const int StudentId    = 42;
    private const int SubjectId    = (int)Subject.Science;
    private const int ConceptId    = 99;
    private const int SkillId      = 10;

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds an <see cref="ExplainConceptCommandHandler"/> where the JWT mock returns
    /// the supplied <paramref name="grade"/> for the "Grade" claim.
    /// The cache mock CAPTURES the key passed to <see cref="IAiResponseCache.GetApprovedAsync"/>.
    /// </summary>
    private static (ExplainConceptCommandHandler Handler, List<string> CapturedKeys)
        BuildHandlerCapturingCacheKey(string? grade)
    {
        // ── ICurrentUserService ──────────────────────────────────────────────────
        var userMock = new Mock<ICurrentUserService>();
        userMock.Setup(u => u.UserId).Returns(StudentId);
        userMock.Setup(u => u.GetClaimValue("Grade")).Returns(grade);
        userMock.Setup(u => u.GetClaimValue("Age")).Returns("10");
        userMock.Setup(u => u.GetClaimValue("Language")).Returns("ar");

        // ── Learning context: populated chunks so execution reaches cache lookup ─
        var contextProvider = new Mock<ILearningContextProvider>();
        contextProvider
            .Setup(p => p.GetContextAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LearningContext(
                Chunks: new[] { new ChunkDto("c1", "Science chunk content.") },
                QuestionText: null,
                WrongAnswer: null,
                SkillId: SkillId,
                QuestionId: ConceptId,
                GradeId: 5,
                SubjectId: SubjectId,
                Language: TutorLanguage.Ar));

        // ── Prompt builder ───────────────────────────────────────────────────────
        var promptMock = new Mock<IPromptBuilder>();
        promptMock
            .Setup(p => p.Build(It.IsAny<PromptContext>()))
            .Returns(new PromptBuilderResult.Success(
                new AiRequest { Prompt = "Explain photosynthesis", Task = AiTaskKind.Explain }));

        // ── Cache: capture the key; always return MISS so safety layer is reached ─
        var capturedKeys = new List<string>();
        var cacheMock = new Mock<IAiResponseCache>();
        cacheMock
            .Setup(c => c.GetApprovedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => capturedKeys.Add(key))
            .ReturnsAsync((string?)null);
        cacheMock
            .Setup(c => c.WriteAsync(It.IsAny<AiCacheWriteEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // ── Safety layer: always allow so we don't short-circuit before cache key ─
        var safetyMock = new Mock<ISafetyLayer>();
        safetyMock
            .Setup(s => s.GenerateSafeAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SafeAiResult(
                Allowed: true,
                Content: "Test content.",
                Verdict: SafetyVerdict.Allowed,
                Results: Array.Empty<CheckResult>()));

        // ── Remaining stubs ───────────────────────────────────────────────────────
        var lessonCtx = new Mock<ILessonContextContract>();
        lessonCtx
            .Setup(l => l.GetLessonContextAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LessonContextDto?)null);

        var localizer = new Mock<IStringLocalizer<SharedResources>>();
        localizer.Setup(l => l[It.IsAny<string>()]).Returns<string>(k => new LocalizedString(k, k));

        var publisher = new Mock<IPublisher>();
        var logger    = new Mock<ILoggerManager>();
        var redirectBuilder = new RedirectResponseBuilder(localizer.Object);

        var settings = new Mock<IGlobalSettingsProvider>();
        settings.Setup(s => s.GetDecimal(It.IsAny<string>(), It.IsAny<decimal>()))
            .Returns<string, decimal>((_, def) => def);
        settings.Setup(s => s.GetInt(It.IsAny<string>(), It.IsAny<int>()))
            .Returns<string, int>((_, def) => def);
        settings.Setup(s => s.GetString(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((_, def) => def);
        settings.Setup(s => s.GetBool(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns<string, bool>((_, def) => def);

        // No-op scope factory for cache fire-and-forget.
        var innerCacheMock = new Mock<IAiResponseCache>();
        innerCacheMock
            .Setup(c => c.WriteAsync(It.IsAny<AiCacheWriteEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var spMock = new Mock<IServiceProvider>();
        spMock.Setup(sp => sp.GetService(typeof(IAiResponseCache))).Returns(innerCacheMock.Object);
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(spMock.Object);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        var handler = new ExplainConceptCommandHandler(
            userMock.Object,
            lessonCtx.Object,
            contextProvider.Object,
            promptMock.Object,
            safetyMock.Object,
            cacheMock.Object,
            settings.Object,
            redirectBuilder,
            new AiTutorRateLimiter(),
            publisher.Object,
            scopeFactory.Object,
            logger.Object,
            localizer.Object);

        return (handler, capturedKeys);
    }

    // ── GD-01: Different age-bands → different cache keys ────────────────────────

    /// <summary>
    /// GD-01: A student with JWT grade=2 (band1, ages 6-9) and a student with JWT grade=7
    /// (band3, ages 12-15) must receive different cache keys for the same concept —
    /// cross-cohort isolation must be real, not nominal.
    ///
    /// Before AI-DEFECT-1 fix: both students always produced a band2 key (grade fell back to 4).
    /// After fix: the JWT claim is minted at token time and the handler resolves the real grade.
    /// </summary>
    [Fact(DisplayName = "GD-01 Grade=2 (band1) vs grade=7 (band3) → different cache keys (AI-DEFECT-1 regression guard)")]
    public async Task Handle_DifferentAgeBandGrades_ProduceDifferentCacheKeys()
    {
        // Arrange — two handler instances with different JWT grade claims.
        var (handlerBand1, keysBand1) = BuildHandlerCapturingCacheKey(grade: "2"); // band1: grades 1-3
        var (handlerBand3, keysBand3) = BuildHandlerCapturingCacheKey(grade: "7"); // band3: grades 7-9

        var command = new ExplainConceptCommand(LessonId: null, ConceptId: ConceptId, SkillId: SkillId, Question: null);

        // Act — exercise both handlers so they each call GetApprovedAsync with their key.
        await handlerBand1.Handle(command, CancellationToken.None);
        await handlerBand3.Handle(command, CancellationToken.None);

        // Assert — each handler must have captured exactly one cache key.
        keysBand1.Should().HaveCount(1, "handler for band1 student should call cache lookup exactly once");
        keysBand3.Should().HaveCount(1, "handler for band3 student should call cache lookup exactly once");

        var keyBand1 = keysBand1[0];
        var keyBand3 = keysBand3[0];

        keyBand1.Should().NotBe(keyBand3,
            because: "AI-DEFECT-1 fix: a band1 student (grade=2) and a band3 student (grade=7) must " +
                     "receive different cache keys — before the fix both resolved to the default grade=4 " +
                     "(band2) and produced the same key, making cross-cohort isolation nominal only");
    }

    // ── GD-02: Same age-band → same cache key (within-cohort reuse preserved) ────

    /// <summary>
    /// GD-02: Students with grade=4 and grade=5 (both band2) must still share a cache key —
    /// within-cohort reuse must be preserved after the fix.
    /// </summary>
    [Fact(DisplayName = "GD-02 Grade=4 (band2) vs grade=5 (band2) → same cache key (within-cohort reuse preserved)")]
    public async Task Handle_SameAgeBandGrades_ProduceSameCacheKey()
    {
        // Arrange.
        var (handlerGrade4, keysGrade4) = BuildHandlerCapturingCacheKey(grade: "4");
        var (handlerGrade5, keysGrade5) = BuildHandlerCapturingCacheKey(grade: "5");

        var command = new ExplainConceptCommand(LessonId: null, ConceptId: ConceptId, SkillId: SkillId, Question: null);

        // Act.
        await handlerGrade4.Handle(command, CancellationToken.None);
        await handlerGrade5.Handle(command, CancellationToken.None);

        // Assert.
        keysGrade4.Should().HaveCount(1);
        keysGrade5.Should().HaveCount(1);

        keysGrade4[0].Should().Be(keysGrade5[0],
            because: "Grade 4 and Grade 5 are both in band2 — cached content may safely be shared " +
                     "across students in the same developmental cohort (within-cohort reuse is intentional)");
    }

    // ── GD-03: Missing Grade claim → fallback grade=4 (band2) ───────────────────

    /// <summary>
    /// GD-03: When the JWT "Grade" claim is absent (legacy token / non-student account that
    /// somehow reaches the handler), the handler must fall back to grade=4 (band2) — the
    /// existing defensive default — and still produce a valid (deterministic) cache key.
    /// </summary>
    [Fact(DisplayName = "GD-03 Missing Grade claim → fallback grade=4 (band2) — handler still produces a valid key")]
    public async Task Handle_MissingGradeClaim_FallsBackToDefaultGradeAndProducesValidKey()
    {
        // Arrange — grade claim is null (absent from token).
        var (handler, capturedKeys) = BuildHandlerCapturingCacheKey(grade: null);

        var command = new ExplainConceptCommand(LessonId: null, ConceptId: ConceptId, SkillId: SkillId, Question: null);

        // Act.
        await handler.Handle(command, CancellationToken.None);

        // Assert — a key was produced (handler did not throw or return early).
        capturedKeys.Should().HaveCount(1,
            because: "fallback to grade=4 must still produce a valid cache key — handler must not throw");

        // The fallback key should match what grade=4 (band2) produces.
        var (handlerGrade4, keysGrade4) = BuildHandlerCapturingCacheKey(grade: "4");
        await handlerGrade4.Handle(command, CancellationToken.None);

        capturedKeys[0].Should().Be(keysGrade4[0],
            because: "missing Grade claim falls back to grade=4 per TryResolveProfile defensive default; " +
                     "the resulting key must be identical to an explicit grade=4 claim");
    }

    // ── GD-04: Grade=2 (band1) → NOT the band2 default key ──────────────────────

    /// <summary>
    /// GD-04: A student with JWT grade=2 (band1) must receive a band1 cache key.
    /// This is the direct regression guard: before the fix, grade=2 was ignored and
    /// the handler defaulted to grade=4 (band2), making both keys the same.
    /// After the fix, the band1 key differs from the band2 key.
    /// </summary>
    [Fact(DisplayName = "GD-04 Grade=2 (band1) → cache key differs from grade=4 (band2) default (AI-DEFECT-1 direct regression)")]
    public async Task Handle_Grade2_ProducesBand1KeyNotBand2Default()
    {
        // Arrange.
        var (handlerGrade2, keysGrade2) = BuildHandlerCapturingCacheKey(grade: "2");  // band1
        var (handlerDefault, keysDefault) = BuildHandlerCapturingCacheKey(grade: null); // fallback → grade=4, band2

        var command = new ExplainConceptCommand(LessonId: null, ConceptId: ConceptId, SkillId: SkillId, Question: null);

        // Act.
        await handlerGrade2.Handle(command, CancellationToken.None);
        await handlerDefault.Handle(command, CancellationToken.None);

        // Assert.
        keysGrade2.Should().HaveCount(1);
        keysDefault.Should().HaveCount(1);

        keysGrade2[0].Should().NotBe(keysDefault[0],
            because: "AI-DEFECT-1 direct regression: grade=2 (band1) must produce a different cache key than " +
                     "the fallback grade=4 (band2) — if they match, the JWT Grade claim is still being ignored " +
                     "and every grade-1/2/3 student is erroneously served band2 content");
    }
}
