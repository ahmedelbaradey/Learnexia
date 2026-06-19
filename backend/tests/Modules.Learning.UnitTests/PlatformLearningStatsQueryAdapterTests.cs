using FluentAssertions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Contracts;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
#pragma warning disable CA1707

namespace Modules.Learning.UnitTests;

/// <summary>
/// Unit tests for <see cref="PlatformLearningStatsQueryAdapter"/> (P7-10).
/// Uses EF InMemory provider — mirrors the StudentLearningStatsQueryAdapterTests pattern.
///
/// Coverage:
///   PL-01  EmptyDb_ReturnsAllZeroes_NoBreakdowns
///   PL-02  CompletedAttempts_CountsLessonsAndAttempts
///   PL-03  NonCompletedAttempts_Excluded
///   PL-04  WindowFilter_ExcludesOutsideAttempts
///   PL-05  DistinctActiveStudents_CountsDistinct
///   PL-06  DistinctLessonsCompleted_CountsDistinct
///   PL-07  SubjectBreakdown_GroupsBySubjectCodeAndLanguage
///   PL-08  GradeBreakdown_GroupsByGradeId
/// </summary>
public sealed class PlatformLearningStatsQueryAdapterTests : IDisposable
{
    private readonly LearningDbContext _db;
    private readonly PlatformLearningStatsQueryAdapter _sut;

    public PlatformLearningStatsQueryAdapterTests()
    {
        var opts = new DbContextOptionsBuilder<LearningDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db  = new LearningDbContext(opts);
        _sut = new PlatformLearningStatsQueryAdapter(_db);
    }

    public void Dispose() => _db.Dispose();

    private static readonly DateTime Base = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime From = Base;
    private static readonly DateTime To   = Base.AddDays(30);

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private async Task<(Grade grade, Subject subject, Unit unit, Lesson lesson)> SeedCurriculumAsync(
        int gradeId, SubjectCode subjectCode, ContentLanguage language, int lessonId)
    {
        // Re-use an already-tracked Grade rather than inserting a duplicate
        var grade = await _db.Grades.FindAsync(gradeId);
        if (grade is null)
        {
            grade = new Grade
            {
                Id          = gradeId,
                Number      = gradeId,
                DisplayName = $"Grade {gradeId}",
            };
            _db.Grades.Add(grade);
        }

        var subject = new Subject
        {
            Id              = gradeId * 100 + (int)subjectCode,
            Name            = subjectCode.ToString(),
            SubjectCode     = subjectCode,
            Language        = language,
            GradeId         = gradeId,
            LifecycleState  = LifecycleState.Published,
        };
        _db.Subjects.Add(subject);

        var unit = new Unit
        {
            Id             = gradeId * 1000 + (int)subjectCode,
            Name           = "Unit 1",
            SubjectId      = subject.Id,
            SequenceOrder  = 1,
            LifecycleState = LifecycleState.Published,
        };
        _db.Units.Add(unit);

        var lesson = new Lesson
        {
            Id             = lessonId,
            Name           = $"Lesson {lessonId}",
            UnitId         = unit.Id,
            SequenceOrder  = 1,
            Difficulty     = DifficultyLevel.Medium,
            LifecycleState = LifecycleState.Published,
        };
        _db.Lessons.Add(lesson);

        await _db.SaveChangesAsync();
        return (grade, subject, unit, lesson);
    }

    private async Task SeedAttemptAsync(
        int id, int studentId, int lessonId, AttemptStatus status, DateTime? completedAt)
    {
        _db.Attempts.Add(new Attempt
        {
            Id           = id,
            StudentId    = studentId,
            LessonId     = lessonId,
            Status       = status,
            CompletedAt  = completedAt,
            StartedAt    = completedAt?.AddSeconds(-30) ?? DateTime.UtcNow.AddMinutes(-1),
        });
        await _db.SaveChangesAsync();
    }

    // ── PL-01: empty database → all zeroes, no breakdowns ─────────────────────

    [Fact]
    public async Task PL_01_EmptyDb_ReturnsAllZeroes_NoBreakdowns()
    {
        var result = await _sut.GetPlatformAsync(From, To);

        result.LessonsCompleted.Should().Be(0);
        result.TotalAttempts.Should().Be(0);
        result.DistinctActiveStudents.Should().Be(0);
        result.BySubject.Should().BeEmpty();
        result.ByGrade.Should().BeEmpty();
    }

    // ── PL-02: completed attempts → counts correctly ───────────────────────────

    [Fact]
    public async Task PL_02_CompletedAttempts_CountsLessonsAndAttempts()
    {
        await SeedCurriculumAsync(gradeId: 1, subjectCode: SubjectCode.MATH,
            language: ContentLanguage.Ar, lessonId: 1);

        await SeedAttemptAsync(1, studentId: 10, lessonId: 1, AttemptStatus.Completed, Base.AddDays(1));
        await SeedAttemptAsync(2, studentId: 10, lessonId: 1, AttemptStatus.Completed, Base.AddDays(2));

        var result = await _sut.GetPlatformAsync(From, To);

        result.TotalAttempts.Should().Be(2);
        result.LessonsCompleted.Should().Be(1); // 1 distinct lesson
        result.DistinctActiveStudents.Should().Be(1);
    }

    // ── PL-03: non-completed attempts excluded ─────────────────────────────────

    [Fact]
    public async Task PL_03_NonCompletedAttempts_Excluded()
    {
        await SeedCurriculumAsync(gradeId: 1, subjectCode: SubjectCode.MATH,
            language: ContentLanguage.Ar, lessonId: 1);

        await SeedAttemptAsync(1, 10, 1, AttemptStatus.InProgress, completedAt: null);
        await SeedAttemptAsync(2, 10, 1, AttemptStatus.Abandoned,  completedAt: null);

        var result = await _sut.GetPlatformAsync(From, To);

        result.TotalAttempts.Should().Be(0);
    }

    // ── PL-04: window filter excludes outside-range attempts ──────────────────

    [Fact]
    public async Task PL_04_WindowFilter_ExcludesOutsideAttempts()
    {
        await SeedCurriculumAsync(gradeId: 1, subjectCode: SubjectCode.MATH,
            language: ContentLanguage.Ar, lessonId: 1);

        await SeedAttemptAsync(1, 10, 1, AttemptStatus.Completed, From.AddDays(-1));   // before
        await SeedAttemptAsync(2, 10, 1, AttemptStatus.Completed, Base.AddDays(5));    // inside
        await SeedAttemptAsync(3, 10, 1, AttemptStatus.Completed, To.AddDays(1));      // after

        var result = await _sut.GetPlatformAsync(From, To);

        result.TotalAttempts.Should().Be(1);
    }

    // ── PL-05: distinct active students count ─────────────────────────────────

    [Fact]
    public async Task PL_05_DistinctActiveStudents_CountsDistinct()
    {
        await SeedCurriculumAsync(gradeId: 1, subjectCode: SubjectCode.MATH,
            language: ContentLanguage.Ar, lessonId: 1);

        // Students 10, 11, 12 — student 10 has 2 attempts
        await SeedAttemptAsync(1, 10, 1, AttemptStatus.Completed, Base.AddDays(1));
        await SeedAttemptAsync(2, 10, 1, AttemptStatus.Completed, Base.AddDays(2));
        await SeedAttemptAsync(3, 11, 1, AttemptStatus.Completed, Base.AddDays(3));
        await SeedAttemptAsync(4, 12, 1, AttemptStatus.Completed, Base.AddDays(4));

        var result = await _sut.GetPlatformAsync(From, To);

        result.DistinctActiveStudents.Should().Be(3);
    }

    // ── PL-06: distinct lessons completed (not attempts) ──────────────────────

    [Fact]
    public async Task PL_06_DistinctLessonsCompleted_CountsDistinct()
    {
        await SeedCurriculumAsync(gradeId: 1, subjectCode: SubjectCode.MATH,
            language: ContentLanguage.Ar, lessonId: 1);
        await SeedCurriculumAsync(gradeId: 1, subjectCode: SubjectCode.SCIENCE,
            language: ContentLanguage.Ar, lessonId: 2);

        // 3 total attempts but only 2 distinct lessons
        await SeedAttemptAsync(1, 10, 1, AttemptStatus.Completed, Base.AddDays(1));
        await SeedAttemptAsync(2, 10, 2, AttemptStatus.Completed, Base.AddDays(2));
        await SeedAttemptAsync(3, 11, 1, AttemptStatus.Completed, Base.AddDays(3)); // lesson 1 again

        var result = await _sut.GetPlatformAsync(From, To);

        result.LessonsCompleted.Should().Be(2);
        result.TotalAttempts.Should().Be(3);
    }

    // ── PL-07: subject breakdown groups correctly ──────────────────────────────

    [Fact]
    public async Task PL_07_SubjectBreakdown_GroupsBySubjectCodeAndLanguage()
    {
        // Two different subjects in different grades
        await SeedCurriculumAsync(gradeId: 1, subjectCode: SubjectCode.MATH,
            language: ContentLanguage.Ar, lessonId: 1);
        await SeedCurriculumAsync(gradeId: 2, subjectCode: SubjectCode.SCIENCE,
            language: ContentLanguage.Ar, lessonId: 2);

        await SeedAttemptAsync(1, 10, 1, AttemptStatus.Completed, Base.AddDays(1));  // Math/Ar
        await SeedAttemptAsync(2, 11, 1, AttemptStatus.Completed, Base.AddDays(2));  // Math/Ar
        await SeedAttemptAsync(3, 10, 2, AttemptStatus.Completed, Base.AddDays(3));  // Science/Ar

        var result = await _sut.GetPlatformAsync(From, To);

        result.BySubject.Should().HaveCount(2);

        var mathRow = result.BySubject.Single(b => b.SubjectCode == (int)SubjectCode.MATH);
        mathRow.TotalAttempts.Should().Be(2);
        mathRow.LessonsCompleted.Should().Be(1);
        mathRow.DistinctActiveStudents.Should().Be(2);

        var scienceRow = result.BySubject.Single(b => b.SubjectCode == (int)SubjectCode.SCIENCE);
        scienceRow.TotalAttempts.Should().Be(1);
    }

    // ── PL-08: grade breakdown groups correctly ────────────────────────────────

    [Fact]
    public async Task PL_08_GradeBreakdown_GroupsByGradeId()
    {
        await SeedCurriculumAsync(gradeId: 1, subjectCode: SubjectCode.MATH,
            language: ContentLanguage.Ar, lessonId: 1);
        await SeedCurriculumAsync(gradeId: 2, subjectCode: SubjectCode.MATH,
            language: ContentLanguage.Ar, lessonId: 2);

        await SeedAttemptAsync(1, 10, 1, AttemptStatus.Completed, Base.AddDays(1)); // grade 1
        await SeedAttemptAsync(2, 10, 1, AttemptStatus.Completed, Base.AddDays(2)); // grade 1
        await SeedAttemptAsync(3, 11, 2, AttemptStatus.Completed, Base.AddDays(3)); // grade 2

        var result = await _sut.GetPlatformAsync(From, To);

        result.ByGrade.Should().HaveCount(2);

        var grade1Row = result.ByGrade.Single(b => b.GradeId == 1);
        grade1Row.TotalAttempts.Should().Be(2);

        var grade2Row = result.ByGrade.Single(b => b.GradeId == 2);
        grade2Row.TotalAttempts.Should().Be(1);
    }
}
