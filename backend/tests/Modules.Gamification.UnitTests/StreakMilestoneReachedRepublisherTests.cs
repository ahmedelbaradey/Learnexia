using FluentAssertions;
using Learnexia.Modules.Gamification.Application.Features.Reengagement.Republishers;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using MediatR;
using Moq;
using Xunit;
#pragma warning disable CA1707

namespace Modules.Gamification.UnitTests;

/// <summary>
/// Unit tests for <see cref="StreakMilestoneReachedRepublisher"/> (P9-06-BE-1).
///
/// Coverage:
///   MR-01  NewStreak=3  (first milestone) → publishes StreakMilestoneReachedIntegrationEvent
///   MR-02  NewStreak=7  → publishes
///   MR-03  NewStreak=14 → publishes
///   MR-04  NewStreak=30 → publishes
///   MR-05  NewStreak=4  (non-milestone) → does NOT publish
///   MR-06  NewStreak=8  → does NOT publish
///   MR-07  NewStreak=1  (ResetStreakAndStart path) → does NOT publish
///   MR-08  Publish throws → republisher swallows, does NOT rethrow (fail-soft)
///   MR-09  Custom config "5,10" → publishes at 5, not at 3 or 7
///   MR-10  Malformed config "3,abc,7" → ignores the bad entry, still publishes at 3 and 7
///   MR-11  Published event carries correct StudentId and Milestone
/// </summary>
public sealed class StreakMilestoneReachedRepublisherTests
{
    // ── Factories ─────────────────────────────────────────────────────────────────

    private static StreakAdvancedDomainEvent MakeEvent(int studentId, int newStreak)
        => new(studentId, newStreak, LongestStreak: newStreak, ActivityDate: DateOnly.FromDateTime(DateTime.UtcNow));

    private static Mock<IGlobalSettingsProvider> DefaultSettings()
    {
        var mock = new Mock<IGlobalSettingsProvider>();
        mock.Setup(s => s.GetString("Gamification:Streak:MilestoneDays", "3,7,14,30"))
            .Returns("3,7,14,30");
        return mock;
    }

    private static Mock<IGlobalSettingsProvider> CustomSettings(string raw)
    {
        var mock = new Mock<IGlobalSettingsProvider>();
        mock.Setup(s => s.GetString("Gamification:Streak:MilestoneDays", "3,7,14,30"))
            .Returns(raw);
        return mock;
    }

    private static StreakMilestoneReachedRepublisher BuildSut(
        Mock<IPublisher> publisher,
        Mock<IGlobalSettingsProvider>? settings = null)
        => new(publisher.Object, (settings ?? DefaultSettings()).Object, new Mock<ILoggerManager>().Object);

    // ── MR-01 — milestone 3 → publishes ─────────────────────────────────────────

    [Fact(DisplayName = "P906-MR-01 NewStreak=3 (first milestone) publishes integration event")]
    public async Task NewStreak3_IsMilestone_Publishes()
    {
        var publisher = new Mock<IPublisher>();
        var sut = BuildSut(publisher);

        await sut.Handle(MakeEvent(studentId: 1, newStreak: 3), CancellationToken.None);

        publisher.Verify(
            p => p.Publish(It.Is<StreakMilestoneReachedIntegrationEvent>(e => e.Milestone == 3),
                           It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── MR-02 — milestone 7 → publishes ─────────────────────────────────────────

    [Fact(DisplayName = "P906-MR-02 NewStreak=7 (weekly milestone) publishes integration event")]
    public async Task NewStreak7_IsMilestone_Publishes()
    {
        var publisher = new Mock<IPublisher>();
        var sut = BuildSut(publisher);

        await sut.Handle(MakeEvent(studentId: 2, newStreak: 7), CancellationToken.None);

        publisher.Verify(
            p => p.Publish(It.Is<StreakMilestoneReachedIntegrationEvent>(e => e.Milestone == 7),
                           It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── MR-03 — milestone 14 → publishes ────────────────────────────────────────

    [Fact(DisplayName = "P906-MR-03 NewStreak=14 publishes integration event")]
    public async Task NewStreak14_IsMilestone_Publishes()
    {
        var publisher = new Mock<IPublisher>();
        var sut = BuildSut(publisher);

        await sut.Handle(MakeEvent(studentId: 3, newStreak: 14), CancellationToken.None);

        publisher.Verify(
            p => p.Publish(It.Is<StreakMilestoneReachedIntegrationEvent>(e => e.Milestone == 14),
                           It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── MR-04 — milestone 30 → publishes ────────────────────────────────────────

    [Fact(DisplayName = "P906-MR-04 NewStreak=30 publishes integration event")]
    public async Task NewStreak30_IsMilestone_Publishes()
    {
        var publisher = new Mock<IPublisher>();
        var sut = BuildSut(publisher);

        await sut.Handle(MakeEvent(studentId: 4, newStreak: 30), CancellationToken.None);

        publisher.Verify(
            p => p.Publish(It.Is<StreakMilestoneReachedIntegrationEvent>(e => e.Milestone == 30),
                           It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── MR-05 — non-milestone 4 → does NOT publish ───────────────────────────────

    [Fact(DisplayName = "P906-MR-05 NewStreak=4 (non-milestone) does NOT publish")]
    public async Task NewStreak4_NotMilestone_DoesNotPublish()
    {
        var publisher = new Mock<IPublisher>();
        var sut = BuildSut(publisher);

        await sut.Handle(MakeEvent(studentId: 5, newStreak: 4), CancellationToken.None);

        publisher.Verify(
            p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── MR-06 — non-milestone 8 → does NOT publish ───────────────────────────────

    [Fact(DisplayName = "P906-MR-06 NewStreak=8 (non-milestone) does NOT publish")]
    public async Task NewStreak8_NotMilestone_DoesNotPublish()
    {
        var publisher = new Mock<IPublisher>();
        var sut = BuildSut(publisher);

        await sut.Handle(MakeEvent(studentId: 6, newStreak: 8), CancellationToken.None);

        publisher.Verify(
            p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── MR-07 — NewStreak=1 (ResetStreakAndStart) → does NOT publish ─────────────

    [Fact(DisplayName = "P906-MR-07 NewStreak=1 (streak reset day-1) does NOT publish")]
    public async Task NewStreak1_Reset_DoesNotPublish()
    {
        var publisher = new Mock<IPublisher>();
        var sut = BuildSut(publisher);

        await sut.Handle(MakeEvent(studentId: 7, newStreak: 1), CancellationToken.None);

        publisher.Verify(
            p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── MR-08 — publish throws → republisher swallows (fail-soft) ────────────────

    [Fact(DisplayName = "P906-MR-08 Publish throws → republisher swallows, does not rethrow")]
    public async Task PublishThrows_DoesNotRethrow()
    {
        var publisher = new Mock<IPublisher>();
        publisher.Setup(p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new InvalidOperationException("bus failure"));

        var sut = BuildSut(publisher);

        var act = async () => await sut.Handle(MakeEvent(studentId: 8, newStreak: 7), CancellationToken.None);

        await act.Should().NotThrowAsync("republisher must be fail-soft per ADR 0002");
    }

    // ── MR-09 — custom config "5,10" → publishes at 5, not at 3 or 7 ────────────

    [Fact(DisplayName = "P906-MR-09 Custom milestones '5,10' publishes at 5, not at default 3 or 7")]
    public async Task CustomConfig_5_10_PublishesAt5_NotAt3_Or7()
    {
        var publisher = new Mock<IPublisher>();
        var settings = CustomSettings("5,10");
        var sut = BuildSut(publisher, settings);

        await sut.Handle(MakeEvent(studentId: 9, newStreak: 5), CancellationToken.None);
        publisher.Verify(
            p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()),
            Times.Once, "streak=5 is in the custom set");

        publisher.Invocations.Clear();

        await sut.Handle(MakeEvent(studentId: 9, newStreak: 3), CancellationToken.None);
        publisher.Verify(
            p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()),
            Times.Never, "streak=3 is NOT in the custom set");

        await sut.Handle(MakeEvent(studentId: 9, newStreak: 7), CancellationToken.None);
        publisher.Verify(
            p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()),
            Times.Never, "streak=7 is NOT in the custom set");
    }

    // ── MR-10 — malformed config "3,abc,7" → ignores bad token ──────────────────

    [Fact(DisplayName = "P906-MR-10 Malformed config '3,abc,7' skips bad token and publishes at 3 and 7")]
    public async Task MalformedConfig_SkipsBadToken_PublishesValidThresholds()
    {
        var publisher7 = new Mock<IPublisher>();
        var settings = CustomSettings("3,abc,7");
        var sut = BuildSut(publisher7, settings);

        await sut.Handle(MakeEvent(studentId: 10, newStreak: 7), CancellationToken.None);

        publisher7.Verify(
            p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()),
            Times.Once, "streak=7 is a valid milestone in '3,abc,7'");

        var publisher3 = new Mock<IPublisher>();
        var sut3 = BuildSut(publisher3, CustomSettings("3,abc,7"));

        await sut3.Handle(MakeEvent(studentId: 10, newStreak: 3), CancellationToken.None);
        publisher3.Verify(
            p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()),
            Times.Once, "streak=3 is also a valid milestone");
    }

    // ── MR-11 — published event carries correct StudentId and Milestone ───────────

    [Fact(DisplayName = "P906-MR-11 Published event carries correct StudentId and Milestone")]
    public async Task PublishedEvent_CarriesCorrectStudentIdAndMilestone()
    {
        StreakMilestoneReachedIntegrationEvent? captured = null;
        var publisher = new Mock<IPublisher>();
        publisher.Setup(p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
                 .Callback<INotification, CancellationToken>((n, _) =>
                     captured = n as StreakMilestoneReachedIntegrationEvent)
                 .Returns(Task.CompletedTask);

        var sut = BuildSut(publisher);

        await sut.Handle(MakeEvent(studentId: 42, newStreak: 14), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.StudentId.Should().Be(42);
        captured.Milestone.Should().Be(14);
        captured.EventId.Should().NotBeEmpty();
    }
}
