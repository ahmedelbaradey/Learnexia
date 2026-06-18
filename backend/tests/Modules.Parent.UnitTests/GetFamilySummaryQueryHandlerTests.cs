using FluentAssertions;
using Learnexia.Modules.Parent.Application.Features.Analytics.GetFamilySummary;
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
/// Unit tests for <see cref="GetFamilySummaryQueryHandler"/> (P5-08-BE-7 / E2).
///
/// Coverage:
///   FS-01  Parent with no children → all-zero summary, 200 OK.
///   FS-02  Parent with one active-today child → activeLearners = 1.
///   FS-03  Badge count sourced from IStudentBadgesQuery.GetByStudentIdAsync TotalCount.
///   FS-04  Family aggregation: two children, totals are summed.
/// </summary>
public sealed class GetFamilySummaryQueryHandlerTests
{
    private static IStringLocalizer<SharedResources> BuildLocalizer()
    {
        var mock = new Mock<IStringLocalizer<SharedResources>>();
        mock.Setup(l => l[It.IsAny<string>()]).Returns<string>(k => new LocalizedString(k, k));
        return mock.Object;
    }

    private static GetFamilySummaryQueryHandler BuildSut(
        int parentId,
        IReadOnlyList<int> childIds,
        // Per-child overrides: index maps to childIds.
        XpProfileSnapshot[]? profiles    = null,
        DailyXpEntry[][]? dailySeries    = null,
        LearningStats[]? learningStats   = null,
        DateTime?[]? lastActivities      = null,
        int[]? badgeCounts               = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(u => u.UserId).Returns(parentId);

        var parentChildQuery = new Mock<IParentChildQuery>();
        parentChildQuery
            .Setup(q => q.GetChildIdsForParentAsync(parentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(childIds);

        var xpSeam = new Mock<IStudentXpTimeSeriesQuery>();
        var learningSeam = new Mock<IStudentLearningStatsQuery>();
        var badgesSeam = new Mock<IStudentBadgesQuery>();

        for (int i = 0; i < childIds.Count; i++)
        {
            var idx = i;
            var childId = childIds[idx];

            var profile = profiles?[idx] ?? new XpProfileSnapshot(0, 1, 0, 0);
            xpSeam.Setup(s => s.GetProfileSnapshotAsync(childId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(profile);

            var series = dailySeries?[idx] ?? [];
            xpSeam.Setup(s => s.GetDailySeriesAsync(childId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(series);

            var stats = learningStats?[idx] ?? new LearningStats(0, 0, 0, 0);
            learningSeam.Setup(s => s.GetStatsAsync(childId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(stats);

            var lastActivity = lastActivities?[idx];
            learningSeam.Setup(s => s.GetLastActivityUtcAsync(childId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(lastActivity);

            var totalBadges = badgeCounts?[idx] ?? 0;
            badgesSeam.Setup(s => s.GetByStudentIdAsync(childId, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new StudentBadgesSnapshot(totalBadges, []));
        }

        return new GetFamilySummaryQueryHandler(
            currentUser.Object,
            parentChildQuery.Object,
            xpSeam.Object,
            learningSeam.Object,
            badgesSeam.Object,
            new Mock<ILoggerManager>().Object,
            BuildLocalizer());
    }

    /// <summary>FS-01 — Parent with no children → empty summary.</summary>
    [Fact]
    public async Task Should_return_zero_summary_when_parent_has_no_children()
    {
        var handler = BuildSut(parentId: 1, childIds: []);
        var result  = await handler.Handle(new GetFamilySummaryQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Successed.Should().BeTrue();
        result.Data!.ActiveLearners.Should().Be(0);
        result.Data.TotalXp.Should().Be(0);
        result.Data.BadgesEarned.Should().Be(0);
    }

    /// <summary>FS-02 — One child active today → activeLearners = 1.</summary>
    [Fact]
    public async Task Should_count_active_learner_when_child_active_today()
    {
        var handler = BuildSut(
            parentId: 1,
            childIds: [10],
            lastActivities: [DateTime.UtcNow]);

        var result = await handler.Handle(new GetFamilySummaryQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Data!.ActiveLearners.Should().Be(1);
    }

    /// <summary>FS-03 — Badge count sourced from IStudentBadgesQuery.TotalCount.</summary>
    [Fact]
    public async Task Should_sum_badge_counts_from_badges_seam()
    {
        var handler = BuildSut(
            parentId: 1,
            childIds: [10, 11],
            badgeCounts: [3, 5]);

        var result = await handler.Handle(new GetFamilySummaryQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Data!.BadgesEarned.Should().Be(8); // 3 + 5
    }

    /// <summary>FS-04 — Two children: lessons and XP are summed across the family.</summary>
    [Fact]
    public async Task Should_aggregate_lessons_and_xp_across_all_children()
    {
        var xpChild1 = new[] { new DailyXpEntry(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), 150) };
        var xpChild2 = new[] { new DailyXpEntry(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)), 200) };
        var stats1   = new LearningStats(LessonsCompleted: 3, TotalAttempts: 5, TimeLearningSeconds: 600, AvgAccuracy: 70);
        var stats2   = new LearningStats(LessonsCompleted: 2, TotalAttempts: 4, TimeLearningSeconds: 400, AvgAccuracy: 60);

        var handler = BuildSut(
            parentId: 1,
            childIds: [10, 11],
            dailySeries: [xpChild1, xpChild2],
            learningStats: [stats1, stats2]);

        var result = await handler.Handle(new GetFamilySummaryQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Data!.TotalXp.Should().Be(350);          // 150 + 200
        result.Data.LessonsCompleted.Should().Be(5);    // 3 + 2
    }
}
