using FluentAssertions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Modules.Learning.Infrastructure.Service;
using Learnexia.Shared.Contracts.Learning;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Modules.Learning.UnitTests;

/// <summary>
/// Unit tests for <see cref="WeakAreaDetectorService"/> (P5-02-BE-1 / P5-02-BE-4).
///
/// Uses <c>Microsoft.EntityFrameworkCore.InMemory</c> to avoid a real PostgreSQL dependency —
/// mirrors the pattern used by <c>P4_11_FreezeAndBoost_UnitTests</c> and other in-memory tests.
///
/// Test cases required by P5-02-BE-4:
/// 1.  EmptyStudent_NoMasteryRows_ReturnsEmpty     (first-week safety)
/// 2.  NeedsReview_LowMastery_High                (mastery &lt; 30% → High)
/// 3.  NeedsReview_MidMastery_Medium              (30% ≤ mastery &lt; 50% → Medium)
/// 4.  InProgress_RecentLowAccuracy_Low           (mastery ≥ 50%, recent accuracy &lt; 60% → Low)
/// 5.  Mastered_GoodAccuracy_NotIncluded          (mastery ≥ 50%, recent accuracy ≥ 60% → excluded)
/// 6.  RankingOrder_HighBeforeMediumBeforeLow     (severity ordering)
/// 7.  MaxResults_Respected                        (cap applied correctly)
/// 8.  TieBreaker_HigherDeficitRanksFirst          (within same severity, higher deficit comes first)
/// 9.  NotStarted_ExcludedFromResults              (NotStarted skills are not weak areas)
/// 10. ZeroMaxResults_ReturnsAll                   (maxResults = 0 = unlimited)
/// </summary>
public sealed class WeakAreaDetectorServiceTests : IDisposable
{
    private readonly LearningDbContext _db;
    private readonly WeakAreaDetectorService _sut;

    public WeakAreaDetectorServiceTests()
    {
        var opts = new DbContextOptionsBuilder<LearningDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db  = new LearningDbContext(opts);
        _sut = new WeakAreaDetectorService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ────────────────────────────────────────────────────────────────────

    private static Subject SeedSubject(int id, SubjectCode code)
        => new Subject
        {
            Id          = id,
            Name        = $"Subject-{id}",
            SubjectCode = code,
            Language    = ContentLanguage.Ar,
            GradeId     = 1,
        };

    private static Concept SeedConcept(int id, int subjectId)
        => new Concept
        {
            Id              = id,
            Name            = $"Concept-{id}",
            SubjectId       = subjectId,
            DifficultyLevel = DifficultyLevel.Medium,
        };

    private static Skill SeedSkill(int id, int conceptId, string name = "")
        => new Skill
        {
            Id               = id,
            Name             = string.IsNullOrEmpty(name) ? $"Skill-{id}" : name,
            ConceptId        = conceptId,
            MasteryThreshold = 80,
        };

    private static Grade SeedGrade(int id)
        => new Grade { Id = id, DisplayName = $"Grade-{id}", Number = id };

    private static StudentSkillMastery SeedMastery(
        int studentId, int skillId, int masteryPct, MasteryStatus status)
        => new StudentSkillMastery
        {
            StudentId         = studentId,
            SkillId           = skillId,
            MasteryPercentage = masteryPct,
            Status            = status,
            AttemptsCount     = 3,
            LastPracticedAt   = DateTime.UtcNow.AddDays(-1),
        };

    private static Attempt SeedAttempt(int id, int studentId, int lessonId,
        double accuracy, AttemptStatus status = AttemptStatus.Completed)
        => new Attempt
        {
            Id                = id,
            StudentId         = studentId,
            LessonId          = lessonId,
            AccuracyPercentage = accuracy,
            Status            = status,
            StartedAt         = DateTime.UtcNow.AddDays(-2),
            CompletedAt       = status == AttemptStatus.Completed ? DateTime.UtcNow.AddDays(-1) : null,
        };

    private static QuizQuestion SeedQuestion(int id, int lessonId, int? skillId)
        => new QuizQuestion
        {
            Id           = id,
            LessonId     = lessonId,
            SkillId      = skillId,
            QuestionType = QuestionType.MCQ,
            QuestionText = "?",
            Options      = "[]",
            CorrectAnswer= "A",
        };

    private static StudentAnswer SeedAnswer(int id, int attemptId, int questionId, bool isCorrect)
        => new StudentAnswer
        {
            Id            = id,
            AttemptId     = attemptId,
            QuestionId    = questionId,
            IsCorrect     = isCorrect,
            AnswerPayload = "A",
        };

    /// <summary>Seed the minimum graph: 1 Grade, 1 Subject, 1 Concept, 1 Skill.</summary>
    private void SeedBaseGraph(int subjectId = 1, SubjectCode code = SubjectCode.MATH)
    {
        _db.Set<Grade>().Add(SeedGrade(1));
        var subj = SeedSubject(subjectId, code);
        subj.GradeId = 1;
        _db.Set<Subject>().Add(subj);
        _db.Set<Concept>().Add(SeedConcept(1, subjectId));
        _db.Set<Skill>().Add(SeedSkill(1, 1));
        _db.SaveChanges();
    }

    // ── Case 1: first-week / no mastery rows → empty list ─────────────────────

    [Fact]
    public async Task EmptyStudent_NoMasteryRows_ReturnsEmpty()
    {
        var result = await _sut.DetectAsync(studentId: 99, maxResults: 5);

        result.Should().BeEmpty();
    }

    // ── Case 2: mastery < 30% → High severity ─────────────────────────────────

    [Fact]
    public async Task NeedsReview_LowMastery_ReturnsHighSeverity()
    {
        SeedBaseGraph();
        _db.StudentSkillMasteries.Add(SeedMastery(1, 1, masteryPct: 20, MasteryStatus.NeedsReview));
        await _db.SaveChangesAsync();

        var result = await _sut.DetectAsync(studentId: 1, maxResults: 5);

        result.Should().HaveCount(1);
        result[0].Severity.Should().Be(WeakAreaSeverity.High);
        result[0].SkillId.Should().Be(1);
        result[0].MasteryPercent.Should().Be(20);
    }

    // ── Case 3: 30% ≤ mastery < 50% → Medium severity ─────────────────────────

    [Fact]
    public async Task NeedsReview_MidMastery_ReturnsMediumSeverity()
    {
        SeedBaseGraph();
        _db.StudentSkillMasteries.Add(SeedMastery(1, 1, masteryPct: 40, MasteryStatus.NeedsReview));
        await _db.SaveChangesAsync();

        var result = await _sut.DetectAsync(studentId: 1, maxResults: 5);

        result.Should().HaveCount(1);
        result[0].Severity.Should().Be(WeakAreaSeverity.Medium);
        result[0].MasteryPercent.Should().Be(40);
    }

    // ── Case 4: mastery ≥ 50%, recent accuracy < 60% → Low severity ───────────

    [Fact]
    public async Task InProgress_RecentLowAccuracy_ReturnsLowSeverity()
    {
        SeedBaseGraph();

        // Mastery = 55% (InProgress), not a NeedsReview.
        _db.StudentSkillMasteries.Add(SeedMastery(1, 1, masteryPct: 55, MasteryStatus.InProgress));

        // Seed attempt + question + answer so the detector finds recent accuracy.
        var attempt  = SeedAttempt(id: 1, studentId: 1, lessonId: 1, accuracy: 50.0);
        _db.Attempts.Add(attempt);
        var question = SeedQuestion(id: 1, lessonId: 1, skillId: 1);
        _db.QuizQuestions.Add(question);
        _db.StudentAnswers.Add(SeedAnswer(id: 1, attemptId: 1, questionId: 1, isCorrect: false));
        await _db.SaveChangesAsync();

        var result = await _sut.DetectAsync(studentId: 1, maxResults: 5);

        result.Should().HaveCount(1);
        result[0].Severity.Should().Be(WeakAreaSeverity.Low);
    }

    // ── Case 5: mastery ≥ 50%, recent accuracy ≥ 60% → not included ───────────

    [Fact]
    public async Task Mastered_GoodAccuracy_ExcludedFromResults()
    {
        SeedBaseGraph();
        _db.StudentSkillMasteries.Add(SeedMastery(1, 1, masteryPct: 85, MasteryStatus.Mastered));

        var attempt  = SeedAttempt(id: 1, studentId: 1, lessonId: 1, accuracy: 90.0);
        _db.Attempts.Add(attempt);
        var question = SeedQuestion(id: 1, lessonId: 1, skillId: 1);
        _db.QuizQuestions.Add(question);
        _db.StudentAnswers.Add(SeedAnswer(id: 1, attemptId: 1, questionId: 1, isCorrect: true));
        await _db.SaveChangesAsync();

        var result = await _sut.DetectAsync(studentId: 1, maxResults: 5);

        result.Should().BeEmpty();
    }

    // ── Case 6: ranking order High > Medium > Low ──────────────────────────────

    [Fact]
    public async Task RankingOrder_HighBeforeMediumBeforeLow()
    {
        // Seed graph: 1 Grade, 1 Subject, 3 Concepts, 3 Skills.
        _db.Set<Grade>().Add(SeedGrade(1));
        _db.Set<Subject>().Add(SeedSubject(1, SubjectCode.MATH));
        _db.Set<Concept>().Add(SeedConcept(1, 1));
        _db.Set<Concept>().Add(SeedConcept(2, 1));
        _db.Set<Concept>().Add(SeedConcept(3, 1));
        _db.Set<Skill>().Add(SeedSkill(1, 1, "HighSkill"));
        _db.Set<Skill>().Add(SeedSkill(2, 2, "MedSkill"));
        _db.Set<Skill>().Add(SeedSkill(3, 3, "LowSkill"));

        // High: mastery=20 (<30%)
        _db.StudentSkillMasteries.Add(SeedMastery(1, 1, masteryPct: 20, MasteryStatus.NeedsReview));
        // Medium: mastery=40 (30-50%)
        _db.StudentSkillMasteries.Add(SeedMastery(1, 2, masteryPct: 40, MasteryStatus.NeedsReview));
        // Low: mastery=60, recent accuracy=40%
        _db.StudentSkillMasteries.Add(SeedMastery(1, 3, masteryPct: 60, MasteryStatus.InProgress));

        var attempt  = SeedAttempt(id: 1, studentId: 1, lessonId: 1, accuracy: 40.0);
        _db.Attempts.Add(attempt);
        var question = SeedQuestion(id: 1, lessonId: 1, skillId: 3);
        _db.QuizQuestions.Add(question);
        _db.StudentAnswers.Add(SeedAnswer(id: 1, attemptId: 1, questionId: 1, isCorrect: false));
        await _db.SaveChangesAsync();

        var result = await _sut.DetectAsync(studentId: 1, maxResults: 10);

        result.Should().HaveCount(3);
        result[0].Severity.Should().Be(WeakAreaSeverity.High);
        result[1].Severity.Should().Be(WeakAreaSeverity.Medium);
        result[2].Severity.Should().Be(WeakAreaSeverity.Low);
    }

    // ── Case 7: maxResults cap ─────────────────────────────────────────────────

    [Fact]
    public async Task MaxResults_Respected()
    {
        _db.Set<Grade>().Add(SeedGrade(1));
        _db.Set<Subject>().Add(SeedSubject(1, SubjectCode.MATH));

        for (int i = 1; i <= 5; i++)
        {
            _db.Set<Concept>().Add(SeedConcept(i, 1));
            _db.Set<Skill>().Add(SeedSkill(i, i));
            _db.StudentSkillMasteries.Add(SeedMastery(1, i, masteryPct: 10, MasteryStatus.NeedsReview));
        }
        await _db.SaveChangesAsync();

        var result = await _sut.DetectAsync(studentId: 1, maxResults: 3);

        result.Should().HaveCount(3);
    }

    // ── Case 8: tie-breaker — higher deficit ranks first within same severity ──

    [Fact]
    public async Task TieBreaker_HigherDeficitFirst()
    {
        _db.Set<Grade>().Add(SeedGrade(1));
        _db.Set<Subject>().Add(SeedSubject(1, SubjectCode.MATH));
        _db.Set<Concept>().Add(SeedConcept(1, 1));
        _db.Set<Concept>().Add(SeedConcept(2, 1));
        _db.Set<Skill>().Add(SeedSkill(1, 1, "LowerMastery"));
        _db.Set<Skill>().Add(SeedSkill(2, 2, "HigherMastery"));

        // Both High severity; skill 1 has lower mastery → higher deficit → ranks first.
        _db.StudentSkillMasteries.Add(SeedMastery(1, 1, masteryPct: 10, MasteryStatus.NeedsReview));
        _db.StudentSkillMasteries.Add(SeedMastery(1, 2, masteryPct: 25, MasteryStatus.NeedsReview));
        await _db.SaveChangesAsync();

        var result = await _sut.DetectAsync(studentId: 1, maxResults: 5);

        result.Should().HaveCount(2);
        result[0].SkillId.Should().Be(1, "skill 1 (deficit=90) should rank before skill 2 (deficit=75)");
        result[1].SkillId.Should().Be(2);
    }

    // ── Case 9: NotStarted rows are excluded ──────────────────────────────────

    [Fact]
    public async Task NotStarted_ExcludedFromResults()
    {
        SeedBaseGraph();
        _db.StudentSkillMasteries.Add(SeedMastery(1, 1, masteryPct: 0, MasteryStatus.NotStarted));
        await _db.SaveChangesAsync();

        var result = await _sut.DetectAsync(studentId: 1, maxResults: 5);

        result.Should().BeEmpty();
    }

    // ── Case 10: maxResults = 0 → no cap ──────────────────────────────────────

    [Fact]
    public async Task ZeroMaxResults_ReturnsAllWeakAreas()
    {
        _db.Set<Grade>().Add(SeedGrade(1));
        _db.Set<Subject>().Add(SeedSubject(1, SubjectCode.MATH));

        for (int i = 1; i <= 10; i++)
        {
            _db.Set<Concept>().Add(SeedConcept(i, 1));
            _db.Set<Skill>().Add(SeedSkill(i, i));
            _db.StudentSkillMasteries.Add(SeedMastery(1, i, masteryPct: 20, MasteryStatus.NeedsReview));
        }
        await _db.SaveChangesAsync();

        var result = await _sut.DetectAsync(studentId: 1, maxResults: 0);

        result.Should().HaveCount(10);
    }

    // ── Case 11: SubjectCode correctly propagated ─────────────────────────────

    [Fact]
    public async Task SubjectCode_IsCorrectlyPropagated()
    {
        _db.Set<Grade>().Add(SeedGrade(1));
        _db.Set<Subject>().Add(SeedSubject(1, SubjectCode.ARABIC));
        _db.Set<Concept>().Add(SeedConcept(1, 1));
        _db.Set<Skill>().Add(SeedSkill(1, 1));
        _db.StudentSkillMasteries.Add(SeedMastery(1, 1, masteryPct: 20, MasteryStatus.NeedsReview));
        await _db.SaveChangesAsync();

        var result = await _sut.DetectAsync(studentId: 1, maxResults: 5);

        result.Should().HaveCount(1);
        result[0].SubjectCode.Should().Be((int)SubjectCode.ARABIC);
    }

    // ── Case 12: SuggestedNextAction keys are non-empty strings ──────────────

    [Fact]
    public async Task SuggestedNextAction_IsNonEmpty()
    {
        SeedBaseGraph();
        _db.StudentSkillMasteries.Add(SeedMastery(1, 1, masteryPct: 15, MasteryStatus.NeedsReview));
        await _db.SaveChangesAsync();

        var result = await _sut.DetectAsync(studentId: 1, maxResults: 5);

        result.Should().HaveCount(1);
        result[0].SuggestedNextAction.Should().NotBeNullOrWhiteSpace();
    }
}
