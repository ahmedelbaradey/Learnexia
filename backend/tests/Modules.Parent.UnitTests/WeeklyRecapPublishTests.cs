using FluentAssertions;
using Learnexia.Modules.Parent.Infrastructure.Persistence;
using Learnexia.Modules.Parent.Infrastructure.Service;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
#pragma warning disable CA1707

namespace Modules.Parent.UnitTests;

/// <summary>
/// Unit tests for the P9-06 weekly-recap publish behaviour in
/// <see cref="WeeklyReportGeneratorService"/> (P9-06-BE-3).
///
/// Coverage:
///   RC-01  Active week (xp > 0) → publishes WeeklyRecapReadyIntegrationEvent
///   RC-02  Skills > 0, xp = 0 → publishes (only pure zero is suppressed)
///   RC-03  Zero-activity week (xp=0, skills=0) → does NOT publish (never-shaming)
///   RC-04  Published event carries correct StudentId, XpEarned, SkillsImproved, WeekStartUtc
///   RC-05  Publish throws → service swallows, does not propagate (fail-soft)
///   RC-06  Publish failure does not prevent the WeeklyReport row from being written
/// </summary>
public sealed class WeeklyRecapPublishTests : IDisposable
{
    private static readonly DateTime PriorMonday =
        new(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc);

    private readonly ParentDbContext _db;

    public WeeklyRecapPublishTests()
    {
        var options = new DbContextOptionsBuilder<ParentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ParentDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static Mock<IStudentXpTimeSeriesQuery> XpSeam(int childId, int totalXp)
    {
        var mock = new Mock<IStudentXpTimeSeriesQuery>();
        mock.Setup(s => s.GetDailySeriesAsync(childId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                                               It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DailyXpEntry(DateOnly.FromDateTime(PriorMonday), totalXp)]);
        return mock;
    }

    private static Mock<IStudentMasterySummaryQuery> MasterySeam(int childId, int improvingSubjects)
    {
        var bySubject = Enumerable.Range(0, 4)
            .Select(i => new SubjectMasteryEntry(i, i < improvingSubjects ? 60 : 0))
            .ToList()
            .AsReadOnly();
        var summary = new MasterySummary(improvingSubjects > 0 ? 50 : 0, bySubject);
        var mock = new Mock<IStudentMasterySummaryQuery>();
        mock.Setup(s => s.GetByStudentAsync(childId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);
        return mock;
    }

    private static Mock<IStudentAllSubjectsWeakAreasQuery> WeakAreasEmpty()
    {
        var mock = new Mock<IStudentAllSubjectsWeakAreasQuery>();
        mock.Setup(q => q.GetWeakAreasAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        return mock;
    }

    private WeeklyReportGeneratorService BuildSut(int childId, int xp, int skills, Mock<IPublisher> publisher)
        => new(_db, XpSeam(childId, xp).Object, MasterySeam(childId, skills).Object,
               WeakAreasEmpty().Object, publisher.Object, new Mock<ILoggerManager>().Object);

    // ── RC-01 — active week (xp > 0) → publishes ────────────────────────────────

    [Fact(DisplayName = "P906-RC-01 Active week (xp>0) publishes WeeklyRecapReadyIntegrationEvent")]
    public async Task ActiveWeek_Publishes()
    {
        const int childId = 100;
        var publisher = new Mock<IPublisher>();
        var sut = BuildSut(childId, xp: 150, skills: 2, publisher);

        await sut.GenerateAsync(childId, PriorMonday);

        publisher.Verify(
            p => p.Publish(It.Is<WeeklyRecapReadyIntegrationEvent>(e => e.XpEarned == 150),
                           It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── RC-02 — skills > 0, xp = 0 → publishes ──────────────────────────────────

    [Fact(DisplayName = "P906-RC-02 skills>0 but xp=0 still publishes (not pure-zero activity)")]
    public async Task SkillsPositive_XpZero_Publishes()
    {
        const int childId = 101;
        var publisher = new Mock<IPublisher>();
        var sut = BuildSut(childId, xp: 0, skills: 2, publisher);

        await sut.GenerateAsync(childId, PriorMonday);

        publisher.Verify(
            p => p.Publish(It.IsAny<WeeklyRecapReadyIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Once, "skills=2 means it is not a zero-activity week");
    }

    // ── RC-03 — zero-activity week → does NOT publish ────────────────────────────

    [Fact(DisplayName = "P906-RC-03 Zero-activity week (xp=0, skills=0) suppresses the recap nudge")]
    public async Task ZeroActivityWeek_DoesNotPublish()
    {
        const int childId = 102;
        var publisher = new Mock<IPublisher>();
        var sut = BuildSut(childId, xp: 0, skills: 0, publisher);

        await sut.GenerateAsync(childId, PriorMonday);

        publisher.Verify(
            p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()),
            Times.Never, "zero-activity recap must be suppressed (never-shaming)");
    }

    // ── RC-04 — published event carries correct fields ───────────────────────────

    [Fact(DisplayName = "P906-RC-04 Published event carries correct StudentId, XpEarned, SkillsImproved, WeekStartUtc")]
    public async Task PublishedEvent_CarriesCorrectFields()
    {
        const int childId = 103;
        WeeklyRecapReadyIntegrationEvent? captured = null;
        var publisher = new Mock<IPublisher>();
        publisher.Setup(p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
                 .Callback<INotification, CancellationToken>((n, _) =>
                     captured = n as WeeklyRecapReadyIntegrationEvent)
                 .Returns(Task.CompletedTask);

        var sut = BuildSut(childId, xp: 250, skills: 3, publisher);

        await sut.GenerateAsync(childId, PriorMonday);

        captured.Should().NotBeNull();
        captured!.StudentId.Should().Be(childId);
        captured.XpEarned.Should().Be(250);
        captured.SkillsImproved.Should().Be(3);
        captured.WeekStartUtc.Should().Be(PriorMonday);
        captured.EventId.Should().NotBeEmpty();
    }

    // ── RC-05 — publish throws → service swallows (fail-soft) ───────────────────

    [Fact(DisplayName = "P906-RC-05 Publish throws → service swallows, does not propagate")]
    public async Task PublishThrows_DoesNotPropagate()
    {
        const int childId = 104;
        var publisher = new Mock<IPublisher>();
        publisher.Setup(p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new InvalidOperationException("bus down"));

        var sut = BuildSut(childId, xp: 100, skills: 1, publisher);

        var act = async () => await sut.GenerateAsync(childId, PriorMonday);

        await act.Should().NotThrowAsync("publish failure must be fail-soft");
    }

    // ── RC-06 — publish failure does not prevent the DB row ──────────────────────

    [Fact(DisplayName = "P906-RC-06 Publish failure does not prevent the WeeklyReport row from being saved")]
    public async Task PublishFailure_RowStillPersisted()
    {
        const int childId = 105;
        var publisher = new Mock<IPublisher>();
        publisher.Setup(p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new InvalidOperationException("bus down"));

        var sut = BuildSut(childId, xp: 200, skills: 2, publisher);

        await sut.GenerateAsync(childId, PriorMonday);

        var rows = await _db.WeeklyReports.ToListAsync();
        rows.Should().HaveCount(1, "the DB row must be written even if the publish fails");
        rows[0].XpEarned.Should().Be(200);
    }
}
