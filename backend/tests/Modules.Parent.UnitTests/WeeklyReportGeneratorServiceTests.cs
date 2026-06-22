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

namespace Modules.Parent.UnitTests;

/// <summary>
/// Unit tests for <see cref="WeeklyReportGeneratorService"/> (P5-01-BE-2).
///
/// Uses EF Core InMemory to test the find-or-create upsert without hitting a real DB.
///
/// Coverage:
///   GEN-01  First generation for a child+week → new WeeklyReport row created.
///   GEN-02  Idempotent re-run for same child+week → row updated, not duplicated.
///   GEN-03  No-activity week (all seams return zero) → valid zeroed report, no error.
///   GEN-04  XP seam failure → graceful degradation: xpEarned=0, rest unaffected.
/// </summary>
public sealed class WeeklyReportGeneratorServiceTests : IDisposable
{
    private static readonly DateTime PriorMonday =
        new(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc); // a Monday

    private readonly ParentDbContext _db;

    public WeeklyReportGeneratorServiceTests()
    {
        var options = new DbContextOptionsBuilder<ParentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ParentDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private static Mock<IStudentXpTimeSeriesQuery> XpSeamWithXp(int childId, int totalXp)
    {
        var mock = new Mock<IStudentXpTimeSeriesQuery>();
        mock.Setup(s => s.GetDailySeriesAsync(childId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                                               It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DailyXpEntry(DateOnly.FromDateTime(PriorMonday), totalXp)]);
        return mock;
    }

    private static Mock<IStudentXpTimeSeriesQuery> XpSeamEmpty(int childId) =>
        XpSeamWithXp(childId, 0);

    private static Mock<IStudentXpTimeSeriesQuery> XpSeamThrowing()
    {
        var mock = new Mock<IStudentXpTimeSeriesQuery>();
        mock.Setup(s => s.GetDailySeriesAsync(It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                                               It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("seam failure"));
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

    private WeeklyReportGeneratorService BuildSut(
        Mock<IStudentXpTimeSeriesQuery> xp,
        Mock<IStudentMasterySummaryQuery> mastery,
        Mock<IStudentAllSubjectsWeakAreasQuery> weakAreas,
        Mock<IPublisher>? publisher = null)
        => new(_db, xp.Object, mastery.Object, weakAreas.Object,
               (publisher ?? new Mock<IPublisher>()).Object,
               new Mock<ILoggerManager>().Object);

    /// <summary>GEN-01 — First run for a child+week → new row created.</summary>
    [Fact]
    public async Task Should_create_new_report_for_first_run()
    {
        const int childId = 10;

        var sut = BuildSut(
            XpSeamWithXp(childId, 200),
            MasterySeam(childId, 2),
            WeakAreasEmpty());

        await sut.GenerateAsync(childId, PriorMonday);

        var rows = await _db.WeeklyReports.ToListAsync();
        rows.Should().HaveCount(1);
        var row = rows[0];
        row.ChildId.Should().Be(childId);
        row.WeekStartUtc.Should().Be(PriorMonday);
        row.XpEarned.Should().Be(200);
        row.SkillsImproved.Should().Be(2);
    }

    /// <summary>GEN-02 — Idempotent re-run → row updated, not duplicated.</summary>
    [Fact]
    public async Task Should_update_existing_report_on_idempotent_rerun()
    {
        const int childId = 11;

        // First run: 100 XP.
        var sut = BuildSut(
            XpSeamWithXp(childId, 100),
            MasterySeam(childId, 1),
            WeakAreasEmpty());
        await sut.GenerateAsync(childId, PriorMonday);

        // Second run: 300 XP (re-aggregate).
        var sut2 = BuildSut(
            XpSeamWithXp(childId, 300),
            MasterySeam(childId, 3),
            WeakAreasEmpty());
        await sut2.GenerateAsync(childId, PriorMonday);

        // Still exactly one row.
        var rows = await _db.WeeklyReports.ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].XpEarned.Should().Be(300);         // updated
        rows[0].SkillsImproved.Should().Be(3);     // updated
    }

    /// <summary>GEN-03 — No-activity week: all seams return zero → valid zeroed report, no exception.</summary>
    [Fact]
    public async Task Should_produce_valid_zero_report_for_no_activity_week()
    {
        const int childId = 12;

        var sut = BuildSut(
            XpSeamEmpty(childId),
            MasterySeam(childId, 0),
            WeakAreasEmpty());

        // Must not throw.
        var act = async () => await sut.GenerateAsync(childId, PriorMonday);
        await act.Should().NotThrowAsync();

        var rows = await _db.WeeklyReports.ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].XpEarned.Should().Be(0);
        rows[0].SkillsImproved.Should().Be(0);
        rows[0].WeakAreasJson.Should().Be("[]");
    }

    /// <summary>GEN-04 — XP seam throws → graceful degradation: xpEarned=0, row still upserted.</summary>
    [Fact]
    public async Task Should_gracefully_degrade_when_xp_seam_throws()
    {
        const int childId = 13;

        var sut = BuildSut(
            XpSeamThrowing(),              // XP seam throws
            MasterySeam(childId, 2),       // mastery seam succeeds
            WeakAreasEmpty());

        // Must not propagate the seam exception.
        var act = async () => await sut.GenerateAsync(childId, PriorMonday);
        await act.Should().NotThrowAsync();

        var rows = await _db.WeeklyReports.ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].XpEarned.Should().Be(0);           // degraded to 0
        rows[0].SkillsImproved.Should().Be(2);     // mastery data still captured
    }

    /// <summary>
    /// GEN-05 — Recommendations are persisted as STRUCTURED {code, skillName} (P6 cleanup: localize
    /// persisted reports), NOT as localized English prose. High severity → REVIEW_CONCEPT, else
    /// PRACTICE_SKILL. The read handler localizes these at request time.
    /// </summary>
    [Fact]
    public async Task Should_persist_structured_recommendation_codes_not_prose()
    {
        const int childId = 14;

        var weak = new Mock<IStudentAllSubjectsWeakAreasQuery>();
        weak.Setup(q => q.GetWeakAreasAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WeakAreaEntry>
            {
                new(1, "Fractions", 0, 20, WeakAreaSeverity.High, "review"),
                new(2, "Addition", 0, 40, WeakAreaSeverity.Medium, "practice"),
            });

        var sut = BuildSut(XpSeamWithXp(childId, 10), MasterySeam(childId, 1), weak);
        await sut.GenerateAsync(childId, PriorMonday);

        var row = (await _db.WeeklyReports.ToListAsync()).Single();
        row.RecommendationsJson.Should()
            .Contain("REVIEW_CONCEPT").And.Contain("PRACTICE_SKILL")     // stable codes, severity-mapped
            .And.Contain("skillName").And.Contain("Fractions").And.Contain("Addition");
        row.RecommendationsJson.Should()
            .NotContain("Review concept for").And.NotContain("Practice skill:"); // no localized prose persisted
    }
}
