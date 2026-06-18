using FluentAssertions;
using Learnexia.Modules.Parent.Application.Features.Analytics.GetChildWeeklyKpis;
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
/// Unit tests for <see cref="GetChildWeeklyKpisQueryHandler"/> (P5-08-BE-8 / E3).
///
/// Coverage:
///   WK-01  IDOR denied → 403.
///   WK-02  First-week child: all-zero KPIs, all-zero WoW deltas (AC3).
///   WK-03  WoW math: this-week values minus prior-week values.
///   WK-04  childId ≤ 0 → 400.
/// </summary>
public sealed class GetChildWeeklyKpisQueryHandlerTests
{
    private static IStringLocalizer<SharedResources> BuildLocalizer()
    {
        var mock = new Mock<IStringLocalizer<SharedResources>>();
        mock.Setup(l => l[It.IsAny<string>()]).Returns<string>(k => new LocalizedString(k, k));
        return mock.Object;
    }

    private static GetChildWeeklyKpisQueryHandler BuildSut(
        int parentId,
        int childId,
        bool isOwner,
        // This-week data
        IReadOnlyList<DailyXpEntry>? thisWeekXp = null,
        LearningStats? thisWeekStats = null,
        XpProfileSnapshot? profile = null,
        // Prior-week data
        IReadOnlyList<DailyXpEntry>? priorWeekXp = null,
        LearningStats? priorWeekStats = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(u => u.UserId).Returns(parentId);

        var parentChildQuery = new Mock<IParentChildQuery>();
        parentChildQuery
            .Setup(q => q.IsParentOfChildAsync(parentId, childId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(isOwner);

        var xpSeam = new Mock<IStudentXpTimeSeriesQuery>();
        // Set up GetDailySeriesAsync to return this-week data first, then prior-week data.
        // We match on fromDate to distinguish.
        xpSeam.Setup(s => s.GetDailySeriesAsync(childId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((int _, DateOnly from, DateOnly _, CancellationToken _) =>
                  // Heuristic: if from is older than 8 days ago, it's the prior window.
                  from < DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-8))
                      ? (priorWeekXp ?? [])
                      : (thisWeekXp ?? []));

        xpSeam.Setup(s => s.GetProfileSnapshotAsync(childId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(profile ?? new XpProfileSnapshot(0, 1, 0, 0));

        var learningSeam = new Mock<IStudentLearningStatsQuery>();
        learningSeam.Setup(s => s.GetStatsAsync(childId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((int _, DateTime from, DateTime _, CancellationToken _) =>
                        from < DateTime.UtcNow.AddDays(-8)
                            ? (priorWeekStats ?? new LearningStats(0, 0, 0, 0))
                            : (thisWeekStats  ?? new LearningStats(0, 0, 0, 0)));

        return new GetChildWeeklyKpisQueryHandler(
            currentUser.Object,
            parentChildQuery.Object,
            xpSeam.Object,
            learningSeam.Object,
            new Mock<ILoggerManager>().Object,
            BuildLocalizer());
    }

    /// <summary>WK-01 — IDOR: non-owner parent → 403.</summary>
    [Fact]
    public async Task Should_return_403_when_parent_does_not_own_child()
    {
        var handler = BuildSut(parentId: 1, childId: 10, isOwner: false);
        var result  = await handler.Handle(new GetChildWeeklyKpisQuery(10), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.Successed.Should().BeFalse();
    }

    /// <summary>WK-02 — First-week / all-zero state returns 200 OK with zero KPIs and zero deltas.</summary>
    [Fact]
    public async Task Should_return_zero_state_for_first_week_child()
    {
        var handler = BuildSut(parentId: 1, childId: 5, isOwner: true);
        var result  = await handler.Handle(new GetChildWeeklyKpisQuery(5), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Successed.Should().BeTrue();
        var dto = result.Data!;
        dto.TimeLearningMinutes.Should().Be(0);
        dto.XpEarned.Should().Be(0);
        dto.LessonsDone.Should().Be(0);
        dto.TimeLearningDeltaMinutes.Should().Be(0);
        dto.XpDelta.Should().Be(0);
        dto.LessonsDelta.Should().Be(0);
    }

    /// <summary>WK-03 — WoW math: deltas = this week minus prior week.</summary>
    [Fact]
    public async Task Should_compute_wow_deltas_correctly()
    {
        // This week: 200 XP, 1200 seconds (20 min), 4 lessons.
        var thisXp    = new List<DailyXpEntry> { new(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), 200) };
        var thisStats = new LearningStats(LessonsCompleted: 4, TotalAttempts: 10,
                                          TimeLearningSeconds: 1200, AvgAccuracy: 70.0);

        // Prior week: 100 XP, 600 seconds (10 min), 2 lessons.
        var priorXp    = new List<DailyXpEntry> { new(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-9)), 100) };
        var priorStats = new LearningStats(LessonsCompleted: 2, TotalAttempts: 5,
                                           TimeLearningSeconds: 600, AvgAccuracy: 55.0);

        var handler = BuildSut(parentId: 1, childId: 5, isOwner: true,
                               thisWeekXp: thisXp, thisWeekStats: thisStats,
                               priorWeekXp: priorXp, priorWeekStats: priorStats);

        var result = await handler.Handle(new GetChildWeeklyKpisQuery(5), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Successed.Should().BeTrue();
        var dto = result.Data!;
        dto.TimeLearningMinutes.Should().Be(20);           // 1200 / 60
        dto.XpEarned.Should().Be(200);
        dto.LessonsDone.Should().Be(4);
        dto.TimeLearningDeltaMinutes.Should().Be(10);      // 20 - 10
        dto.XpDelta.Should().Be(100);                      // 200 - 100
        dto.LessonsDelta.Should().Be(2);                   // 4 - 2
    }

    /// <summary>WK-04 — childId ≤ 0 → 400 Bad Request.</summary>
    [Fact]
    public async Task Should_return_400_when_child_id_is_negative()
    {
        var handler = BuildSut(parentId: 1, childId: -1, isOwner: false);
        var result  = await handler.Handle(new GetChildWeeklyKpisQuery(-1), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
