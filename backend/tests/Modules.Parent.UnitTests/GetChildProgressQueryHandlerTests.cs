using FluentAssertions;
using Learnexia.Modules.Parent.Application.Features.Analytics.GetChildProgress;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Localization;
using Moq;
using Resources;
using System.Net;
using Xunit;

namespace Modules.Parent.UnitTests;

/// <summary>
/// Unit tests for <see cref="GetChildProgressQueryHandler"/> (P5-08-BE-6 / E1).
///
/// Coverage:
///   CP-01  IDOR denied: authenticated parent is NOT the owner of the requested child → 403.
///   CP-02  Brand-new child with no seam data → all-zero state, 200 OK (AC3 graceful zeroes).
///   CP-03  Child with XP + mastery + weak area → fields correctly mapped to DTO.
///   CP-04  Seam throws → graceful zero/null in affected field; other fields still populated (AC3).
///   CP-05  childId ≤ 0 → 400 Bad Request.
/// </summary>
public sealed class GetChildProgressQueryHandlerTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static IStringLocalizer<SharedResources> BuildLocalizer()
    {
        var mock = new Mock<IStringLocalizer<SharedResources>>();
        mock.Setup(l => l[It.IsAny<string>()]).Returns<string>(k => new LocalizedString(k, k));
        return mock.Object;
    }

    private static ILoggerManager BuildLogger() => new Mock<ILoggerManager>().Object;

    private static GetChildProgressQueryHandler BuildSut(
        int parentId,
        int childId,
        bool isOwner,
        XpProfileSnapshot? profile = null,
        MasterySummary? mastery = null,
        IReadOnlyList<WeakAreaEntry>? weakAreas = null,
        DateTime? lastActivity = null,
        ChildEnergySnapshot? energySnapshot = null,
        bool xpThrows = false,
        bool masteryThrows = false)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(u => u.UserId).Returns(parentId);

        var parentChildQuery = new Mock<IParentChildQuery>();
        parentChildQuery
            .Setup(q => q.IsParentOfChildAsync(parentId, childId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(isOwner);

        var xpSeam = new Mock<IStudentXpTimeSeriesQuery>();
        if (xpThrows)
            xpSeam.Setup(s => s.GetProfileSnapshotAsync(childId, It.IsAny<CancellationToken>()))
                  .ThrowsAsync(new InvalidOperationException("seam unavailable"));
        else
            xpSeam.Setup(s => s.GetProfileSnapshotAsync(childId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(profile ?? new XpProfileSnapshot(0, 1, 0, 0));

        var masterySeam = new Mock<IStudentMasterySummaryQuery>();
        if (masteryThrows)
            masterySeam.Setup(s => s.GetByStudentAsync(childId, It.IsAny<CancellationToken>()))
                       .ThrowsAsync(new InvalidOperationException("seam unavailable"));
        else
            masterySeam.Setup(s => s.GetByStudentAsync(childId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(mastery ?? new MasterySummary(0, []));

        var weakSeam = new Mock<IStudentAllSubjectsWeakAreasQuery>();
        weakSeam.Setup(s => s.GetWeakAreasAsync(childId, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(weakAreas ?? []);

        var learningSeam = new Mock<IStudentLearningStatsQuery>();
        learningSeam.Setup(s => s.GetLastActivityUtcAsync(childId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(lastActivity);

        var energySeam = new Mock<IChildEnergyUsageQuery>();
        energySeam.Setup(s => s.GetSnapshotAsync(childId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(energySnapshot ?? new ChildEnergySnapshot(0, 0, 0, 0, null));

        return new GetChildProgressQueryHandler(
            currentUser.Object,
            parentChildQuery.Object,
            xpSeam.Object,
            masterySeam.Object,
            weakSeam.Object,
            learningSeam.Object,
            energySeam.Object,
            BuildLogger(),
            BuildLocalizer());
    }

    // ── Tests ─────────────────────────────────────────────────────────────────────

    /// <summary>CP-01 — IDOR: non-owner parent gets 403.</summary>
    [Fact]
    public async Task Should_return_403_when_parent_does_not_own_child()
    {
        var handler = BuildSut(parentId: 1, childId: 99, isOwner: false);
        var result  = await handler.Handle(new GetChildProgressQuery(99), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.Successed.Should().BeFalse();
    }

    /// <summary>CP-02 — Empty/first-week state returns zeroed DTO with 200 OK.</summary>
    [Fact]
    public async Task Should_return_zeroed_state_for_brand_new_child()
    {
        var handler = BuildSut(parentId: 1, childId: 5, isOwner: true);
        var result  = await handler.Handle(new GetChildProgressQuery(5), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Successed.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Level.Should().Be(1);
        result.Data.TotalXp.Should().Be(0);
        result.Data.MasteryPercent.Should().Be(0);
        result.Data.WeakestSkill.Should().BeNull();
        result.Data.ActiveToday.Should().BeFalse();
        result.Data.Energy.Remaining.Should().Be(0);
    }

    /// <summary>CP-03 — Full data maps correctly to DTO fields.</summary>
    [Fact]
    public async Task Should_map_seam_data_to_dto_correctly()
    {
        var profile  = new XpProfileSnapshot(TotalXp: 500, CurrentLevel: 3, CurrentStreak: 7, BestStreak: 10);
        var mastery  = new MasterySummary(OverallPercent: 65, BySubject: []);
        var weakArea = new WeakAreaEntry(SkillId: 42, SkillName: "Fractions", SubjectCode: 0,
                                        MasteryPercent: 25, Severity: WeakAreaSeverity.High,
                                        SuggestedNextAction: "review");
        var energy = new ChildEnergySnapshot(Remaining: 30, Allocated: 50, Spent: 20,
                                             PurchasedBalance: 0, CycleEndsAtUtc: null);
        // Set lastActivity to today so activeToday = true.
        var lastActivity = DateTime.UtcNow;

        var handler = BuildSut(
            parentId: 1, childId: 5, isOwner: true,
            profile: profile, mastery: mastery,
            weakAreas: [weakArea], lastActivity: lastActivity,
            energySnapshot: energy);

        var result = await handler.Handle(new GetChildProgressQuery(5), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Successed.Should().BeTrue();
        var dto = result.Data!;
        dto.Level.Should().Be(3);
        dto.TotalXp.Should().Be(500);
        dto.CurrentStreak.Should().Be(7);
        dto.BestStreak.Should().Be(10);
        dto.MasteryPercent.Should().Be(65);
        dto.ActiveToday.Should().BeTrue();
        dto.WeakestSkill.Should().NotBeNull();
        dto.WeakestSkill!.SkillId.Should().Be(42);
        dto.WeakestSkill.Severity.Should().Be((int)WeakAreaSeverity.High);
        dto.Energy.Remaining.Should().Be(30);
        dto.Energy.Spent.Should().Be(20);
    }

    /// <summary>CP-04 — XP seam throws → mastery still populated (graceful degradation).</summary>
    [Fact]
    public async Task Should_degrade_gracefully_when_xp_seam_throws()
    {
        var mastery = new MasterySummary(OverallPercent: 45, BySubject: []);
        var handler = BuildSut(parentId: 1, childId: 5, isOwner: true, mastery: mastery, xpThrows: true);

        var result = await handler.Handle(new GetChildProgressQuery(5), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Successed.Should().BeTrue();
        // XP fields zeroed out.
        result.Data!.Level.Should().Be(1);
        result.Data.TotalXp.Should().Be(0);
        // Mastery still populated.
        result.Data.MasteryPercent.Should().Be(45);
    }

    /// <summary>CP-05 — childId ≤ 0 returns 400 Bad Request.</summary>
    [Fact]
    public async Task Should_return_400_when_child_id_is_zero()
    {
        var handler = BuildSut(parentId: 1, childId: 0, isOwner: false);
        var result  = await handler.Handle(new GetChildProgressQuery(0), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.Successed.Should().BeFalse();
    }
}
