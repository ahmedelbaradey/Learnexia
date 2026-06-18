using FluentAssertions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Contracts;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Modules.Learning.UnitTests;

/// <summary>
/// Unit tests for <see cref="StudentLearningStatsQueryAdapter"/> (P5-08-BE-2).
///
/// Cases:
/// 1.  BrandNewStudent_ReturnsZeroedStats
/// 2.  CompletedAttempts_SumsDuration_CountsDistinctLessons
/// 3.  WindowFilter_ExcludesOutOfWindowAttempts
/// 4.  NonCompletedAttempts_ExcludedFromStats
/// 5.  GetLastActivityUtc_ReturnsLatestCompleted
/// 6.  GetLastActivityUtc_NoCompletedAttempts_ReturnsNull
/// </summary>
public sealed class StudentLearningStatsQueryAdapterTests : IDisposable
{
    private readonly LearningDbContext _db;
    private readonly StudentLearningStatsQueryAdapter _sut;

    public StudentLearningStatsQueryAdapterTests()
    {
        var opts = new DbContextOptionsBuilder<LearningDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db  = new LearningDbContext(opts);
        _sut = new StudentLearningStatsQueryAdapter(_db);
    }

    public void Dispose() => _db.Dispose();

    private static Attempt MakeAttempt(int id, int studentId, int lessonId,
        AttemptStatus status, DateTime? completedAt, int durationSeconds, double accuracy)
        => new Attempt
        {
            Id                 = id,
            StudentId          = studentId,
            LessonId           = lessonId,
            Status             = status,
            CompletedAt        = completedAt,
            DurationSeconds    = durationSeconds,
            AccuracyPercentage = accuracy,
            StartedAt          = completedAt?.AddSeconds(-durationSeconds) ?? DateTime.UtcNow.AddMinutes(-5),
        };

    // ── Case 1: no data → zeroed stats ──────────────────────────────────────

    [Fact]
    public async Task BrandNewStudent_ReturnsZeroedStats()
    {
        var from = DateTime.UtcNow.AddDays(-7);
        var to   = DateTime.UtcNow;

        var result = await _sut.GetStatsAsync(studentId: 99, from, to);

        result.LessonsCompleted.Should().Be(0);
        result.TotalAttempts.Should().Be(0);
        result.TimeLearningSeconds.Should().Be(0);
        result.AvgAccuracy.Should().Be(0.0);
    }

    // ── Case 2: completed attempts within window ─────────────────────────────

    [Fact]
    public async Task CompletedAttempts_SumsDuration_CountsDistinctLessons()
    {
        var baseTime = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
        // Two attempts for the same lesson → only 1 distinct lesson.
        _db.Attempts.AddRange(
            MakeAttempt(1, 1, lessonId: 10, AttemptStatus.Completed, baseTime, durationSeconds: 120, accuracy: 80),
            MakeAttempt(2, 1, lessonId: 10, AttemptStatus.Completed, baseTime.AddHours(1), durationSeconds: 60, accuracy: 90),
            MakeAttempt(3, 1, lessonId: 20, AttemptStatus.Completed, baseTime.AddHours(2), durationSeconds: 180, accuracy: 70)
        );
        await _db.SaveChangesAsync();

        var from   = baseTime.AddHours(-1);
        var to     = baseTime.AddHours(3);
        var result = await _sut.GetStatsAsync(studentId: 1, from, to);

        result.LessonsCompleted.Should().Be(2, "lessons 10 and 20 are distinct");
        result.TotalAttempts.Should().Be(3);
        result.TimeLearningSeconds.Should().Be(360, "120+60+180=360");
        result.AvgAccuracy.Should().BeApproximately(80.0, 0.1, "average of 80, 90, 70");
    }

    // ── Case 3: attempts outside the window are excluded ────────────────────

    [Fact]
    public async Task WindowFilter_ExcludesOutOfWindowAttempts()
    {
        var baseTime = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
        _db.Attempts.AddRange(
            MakeAttempt(1, 1, lessonId: 10, AttemptStatus.Completed, baseTime, durationSeconds: 100, accuracy: 80),
            MakeAttempt(2, 1, lessonId: 20, AttemptStatus.Completed, baseTime.AddDays(-10), durationSeconds: 200, accuracy: 90)
        );
        await _db.SaveChangesAsync();

        // Window covers only baseTime.
        var from   = baseTime.AddDays(-1);
        var to     = baseTime.AddDays(1);
        var result = await _sut.GetStatsAsync(studentId: 1, from, to);

        result.LessonsCompleted.Should().Be(1);
        result.TotalAttempts.Should().Be(1);
        result.TimeLearningSeconds.Should().Be(100);
    }

    // ── Case 4: non-completed attempts excluded ──────────────────────────────

    [Fact]
    public async Task NonCompletedAttempts_ExcludedFromStats()
    {
        var baseTime = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
        _db.Attempts.AddRange(
            MakeAttempt(1, 1, lessonId: 10, AttemptStatus.Completed, baseTime, durationSeconds: 100, accuracy: 75),
            MakeAttempt(2, 1, lessonId: 20, AttemptStatus.InProgress, completedAt: null, durationSeconds: 200, accuracy: 0)
        );
        await _db.SaveChangesAsync();

        var from   = baseTime.AddHours(-1);
        var to     = baseTime.AddHours(1);
        var result = await _sut.GetStatsAsync(studentId: 1, from, to);

        result.LessonsCompleted.Should().Be(1, "only the completed attempt qualifies");
        result.TotalAttempts.Should().Be(1);
        result.TimeLearningSeconds.Should().Be(100);
    }

    // ── Case 5: GetLastActivityUtc returns latest completed ──────────────────

    [Fact]
    public async Task GetLastActivityUtc_ReturnsLatestCompleted()
    {
        var t1 = new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 1, 8, 10, 0, 0, DateTimeKind.Utc);
        _db.Attempts.AddRange(
            MakeAttempt(1, 1, 10, AttemptStatus.Completed, t1, 60, 80),
            MakeAttempt(2, 1, 20, AttemptStatus.Completed, t2, 60, 90)
        );
        await _db.SaveChangesAsync();

        var result = await _sut.GetLastActivityUtcAsync(studentId: 1);

        result.Should().Be(t2);
    }

    // ── Case 6: GetLastActivityUtc returns null when no completed attempts ────

    [Fact]
    public async Task GetLastActivityUtc_NoCompletedAttempts_ReturnsNull()
    {
        var result = await _sut.GetLastActivityUtcAsync(studentId: 42);
        result.Should().BeNull();
    }
}
