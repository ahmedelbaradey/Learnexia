using FluentAssertions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Contracts;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Learning;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Modules.Learning.UnitTests;

/// <summary>
/// Unit tests for <see cref="StudentMasterySummaryQueryAdapter"/> (P5-08-BE-3).
///
/// Cases:
/// 1.  BrandNewStudent_AllSubjectsAtZero          (sentinel: all four subjects returned at 0%)
/// 2.  SingleSubject_AverageMasteryCorrect
/// 3.  AllFourSubjects_Present_WithData
/// 4.  NotStartedRows_Excluded_FromAverage
/// 5.  OverallPercent_AverageAcrossAllSkills
/// 6.  SubjectWithNoData_ReturnsZeroNotMissing     (sentinel: subject always present)
/// </summary>
public sealed class StudentMasterySummaryQueryAdapterTests : IDisposable
{
    private readonly LearningDbContext _db;
    private readonly StudentMasterySummaryQueryAdapter _sut;

    public StudentMasterySummaryQueryAdapterTests()
    {
        var opts = new DbContextOptionsBuilder<LearningDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db  = new LearningDbContext(opts);
        _sut = new StudentMasterySummaryQueryAdapter(_db);
    }

    public void Dispose() => _db.Dispose();

    private bool _gradeSeeded;

    private void SeedSubjectTree(int subjectId, SubjectCode code,
        IEnumerable<(int skillId, int masteryPct, MasteryStatus status)> skillMasteries)
    {
        if (!_gradeSeeded)
        {
            _db.Set<Grade>().Add(new Grade { Id = 1, DisplayName = "Grade 1", Number = 1 });
            _gradeSeeded = true;
        }

        _db.Set<Subject>().Add(new Subject
        {
            Id          = subjectId,
            Name        = $"Subject-{subjectId}",
            SubjectCode = code,
            Language    = ContentLanguage.Ar,
            GradeId     = 1,
        });

        int conceptId = subjectId * 100;
        _db.Set<Concept>().Add(new Concept
        {
            Id              = conceptId,
            Name            = $"Concept-{conceptId}",
            SubjectId       = subjectId,
            DifficultyLevel = DifficultyLevel.Medium,
        });

        foreach (var (skillId, masteryPct, status) in skillMasteries)
        {
            _db.Set<Skill>().Add(new Skill
            {
                Id               = skillId,
                Name             = $"Skill-{skillId}",
                ConceptId        = conceptId,
                MasteryThreshold = 80,
            });

            _db.StudentSkillMasteries.Add(new StudentSkillMastery
            {
                StudentId         = 1,
                SkillId           = skillId,
                MasteryPercentage = masteryPct,
                Status            = status,
                AttemptsCount     = status == MasteryStatus.NotStarted ? 0 : 2,
                LastPracticedAt   = DateTime.UtcNow.AddDays(-1),
            });
        }
    }

    // ── Case 1: brand-new student → all four subjects at 0% ─────────────────

    [Fact]
    public async Task BrandNewStudent_AllSubjectsAtZero()
    {
        var result = await _sut.GetByStudentAsync(studentId: 99);

        result.BySubject.Should().HaveCount(4);
        result.BySubject.All(s => s.Percent == 0).Should().BeTrue();
        result.OverallPercent.Should().Be(0);
    }

    // ── Case 2: single subject, average mastery correct ──────────────────────

    [Fact]
    public async Task SingleSubject_AverageMasteryCorrect()
    {
        SeedSubjectTree(subjectId: 1, SubjectCode.MATH, new[]
        {
            (skillId: 1, masteryPct: 60, MasteryStatus.InProgress),
            (skillId: 2, masteryPct: 80, MasteryStatus.Mastered),
        });
        await _db.SaveChangesAsync();

        var result = await _sut.GetByStudentAsync(studentId: 1);

        var mathEntry = result.BySubject.Single(s => s.SubjectCode == (int)SubjectCode.MATH);
        mathEntry.Percent.Should().Be(70, "average of 60 and 80 = 70");
        result.OverallPercent.Should().Be(70);
    }

    // ── Case 3: all four subjects present with data ───────────────────────────

    [Fact]
    public async Task AllFourSubjects_Present_WithData()
    {
        SeedSubjectTree(1, SubjectCode.MATH,    new[] { (101, 80, MasteryStatus.Mastered) });
        SeedSubjectTree(2, SubjectCode.SCIENCE, new[] { (201, 60, MasteryStatus.InProgress) });
        SeedSubjectTree(3, SubjectCode.ARABIC,  new[] { (301, 40, MasteryStatus.NeedsReview) });
        SeedSubjectTree(4, SubjectCode.ENGLISH, new[] { (401, 20, MasteryStatus.NeedsReview) });
        await _db.SaveChangesAsync();

        var result = await _sut.GetByStudentAsync(studentId: 1);

        result.BySubject.Should().HaveCount(4);
        result.BySubject.Single(s => s.SubjectCode == (int)SubjectCode.MATH).Percent.Should().Be(80);
        result.BySubject.Single(s => s.SubjectCode == (int)SubjectCode.SCIENCE).Percent.Should().Be(60);
        result.BySubject.Single(s => s.SubjectCode == (int)SubjectCode.ARABIC).Percent.Should().Be(40);
        result.BySubject.Single(s => s.SubjectCode == (int)SubjectCode.ENGLISH).Percent.Should().Be(20);
    }

    // ── Case 4: NotStarted rows excluded from average ────────────────────────

    [Fact]
    public async Task NotStartedRows_Excluded_FromAverage()
    {
        SeedSubjectTree(1, SubjectCode.MATH, new[]
        {
            (1, 70, MasteryStatus.InProgress),
            (2, 0,  MasteryStatus.NotStarted),  // should be excluded
        });
        await _db.SaveChangesAsync();

        var result = await _sut.GetByStudentAsync(studentId: 1);

        var mathEntry = result.BySubject.Single(s => s.SubjectCode == (int)SubjectCode.MATH);
        mathEntry.Percent.Should().Be(70, "NotStarted (0%) row should be excluded from the average");
    }

    // ── Case 5: overall = average across all non-NotStarted skills ────────────

    [Fact]
    public async Task OverallPercent_AverageAcrossAllSkills()
    {
        SeedSubjectTree(1, SubjectCode.MATH,    new[] { (1, 80, MasteryStatus.Mastered) });
        SeedSubjectTree(2, SubjectCode.SCIENCE, new[] { (2, 60, MasteryStatus.InProgress) });
        await _db.SaveChangesAsync();

        var result = await _sut.GetByStudentAsync(studentId: 1);

        result.OverallPercent.Should().Be(70, "average of 80 and 60 = 70");
    }

    // ── Case 6: subject with no data → 0% present (not missing) ──────────────

    [Fact]
    public async Task SubjectWithNoData_ReturnsZeroNotMissing()
    {
        // Only Math has data; the other three subjects should be 0% (not absent from the list).
        SeedSubjectTree(1, SubjectCode.MATH, new[] { (1, 75, MasteryStatus.InProgress) });
        await _db.SaveChangesAsync();

        var result = await _sut.GetByStudentAsync(studentId: 1);

        result.BySubject.Should().HaveCount(4);
        result.BySubject.Single(s => s.SubjectCode == (int)SubjectCode.SCIENCE).Percent.Should().Be(0);
        result.BySubject.Single(s => s.SubjectCode == (int)SubjectCode.ARABIC).Percent.Should().Be(0);
        result.BySubject.Single(s => s.SubjectCode == (int)SubjectCode.ENGLISH).Percent.Should().Be(0);
    }
}
