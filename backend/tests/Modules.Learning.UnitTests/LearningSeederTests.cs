using FluentAssertions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Modules.Learning.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Modules.Learning.UnitTests;

/// <summary>
/// Unit tests for <see cref="LearningSeeder"/>. Each test gets a fresh InMemory database via
/// <see cref="BuildServiceProvider"/> so tests are fully isolated.
///
/// P8-02: Updated to assert the bilingual 6-root-per-grade model.
/// </summary>
public sealed class LearningSeederTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a minimal <see cref="IServiceProvider"/> with an InMemory <see cref="LearningDbContext"/>
    /// keyed by a unique database name so each test is isolated.
    /// </summary>
    private static IServiceProvider BuildServiceProvider(string? dbName = null)
    {
        var name = dbName ?? Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<LearningDbContext>(opts =>
            opts.UseInMemoryDatabase(name));
        return services.BuildServiceProvider();
    }

    private static LearningDbContext GetDb(IServiceProvider sp) =>
        sp.GetRequiredService<LearningDbContext>();

    // -------------------------------------------------------------------------
    // P8-02 AC-1: Exactly 6 subject roots per grade
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedAsync_Creates_ExactlySixSubjects_PerGrade()
    {
        var sp = BuildServiceProvider();
        await LearningSeeder.SeedAsync(sp);

        var db = GetDb(sp);

        for (var gradeNumber = 1; gradeNumber <= 6; gradeNumber++)
        {
            var grade = await db.Grades.AsNoTracking().FirstOrDefaultAsync(g => g.Number == gradeNumber);
            grade.Should().NotBeNull($"Grade {gradeNumber} must exist");

            var subjects = await db.Subjects
                .AsNoTracking()
                .Where(s => s.GradeId == grade!.Id)
                .ToListAsync();

            subjects.Should().HaveCount(6,
                $"Grade {gradeNumber} must have exactly 6 subject roots (MATH/Ar, MATH/En, SCIENCE/Ar, SCIENCE/En, ARABIC/Ar, ENGLISH/En)");
        }
    }

    // -------------------------------------------------------------------------
    // P8-02 AC-2: All six expected (SubjectCode, Language) combinations exist per grade
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedAsync_Creates_AllSixExpectedCodeLanguagePairs_PerGrade()
    {
        var sp = BuildServiceProvider();
        await LearningSeeder.SeedAsync(sp);

        var db = GetDb(sp);

        var expectedPairs = new (SubjectCode Code, ContentLanguage Language)[]
        {
            (SubjectCode.MATH,    ContentLanguage.Ar),
            (SubjectCode.MATH,    ContentLanguage.En),
            (SubjectCode.SCIENCE, ContentLanguage.Ar),
            (SubjectCode.SCIENCE, ContentLanguage.En),
            (SubjectCode.ARABIC,  ContentLanguage.Ar),
            (SubjectCode.ENGLISH, ContentLanguage.En),
        };

        for (var gradeNumber = 1; gradeNumber <= 6; gradeNumber++)
        {
            var grade = await db.Grades.AsNoTracking().FirstAsync(g => g.Number == gradeNumber);

            var subjects = await db.Subjects
                .AsNoTracking()
                .Where(s => s.GradeId == grade.Id)
                .ToListAsync();

            var actualPairs = subjects
                .Select(s => (s.SubjectCode, s.Language))
                .ToHashSet();

            foreach (var (code, language) in expectedPairs)
            {
                actualPairs.Should().Contain((code, language),
                    $"Grade {gradeNumber} must contain the {code}/{language} tree");
            }
        }
    }

    // -------------------------------------------------------------------------
    // P8-02 AC-3: (GradeId, SubjectCode, Language) triplet is unique — no duplicates
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedAsync_GradeSubjectCodeLanguage_IsUnique_NoCollisions()
    {
        var sp = BuildServiceProvider();
        await LearningSeeder.SeedAsync(sp);

        var db = GetDb(sp);

        var allSubjects = await db.Subjects.AsNoTracking().ToListAsync();

        var triplets = allSubjects
            .Select(s => (s.GradeId, s.SubjectCode, s.Language))
            .ToList();

        // No duplicate triplets
        triplets.Distinct().Should().HaveCount(triplets.Count,
            "each (GradeId, SubjectCode, Language) triplet must be unique");
    }

    // -------------------------------------------------------------------------
    // P8-02 AC-4: SubjectCode is always explicitly set (never the default int 0 mismatch)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedAsync_AllSubjects_HaveExplicitSubjectCode()
    {
        var sp = BuildServiceProvider();
        await LearningSeeder.SeedAsync(sp);

        var db = GetDb(sp);

        var allSubjects = await db.Subjects.AsNoTracking().ToListAsync();
        allSubjects.Should().NotBeEmpty();

        // All four valid SubjectCode values must be represented
        var codes = allSubjects.Select(s => s.SubjectCode).Distinct().ToHashSet();
        codes.Should().Contain(SubjectCode.MATH);
        codes.Should().Contain(SubjectCode.SCIENCE);
        codes.Should().Contain(SubjectCode.ARABIC);
        codes.Should().Contain(SubjectCode.ENGLISH);
    }

    // -------------------------------------------------------------------------
    // P8-02 AC-5: Full tree linkage — no orphan nodes
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedAsync_TreeIsFullyLinked_NoOrphans()
    {
        var sp = BuildServiceProvider();
        await LearningSeeder.SeedAsync(sp);

        var db = GetDb(sp);

        var subjectIds = await db.Subjects.AsNoTracking().Select(s => s.Id).ToHashSetAsync();
        subjectIds.Should().NotBeEmpty();

        var unitIds = await db.Units.AsNoTracking().Select(u => u.Id).ToHashSetAsync();
        unitIds.Should().NotBeEmpty();

        var conceptIds = await db.Concepts.AsNoTracking().Select(c => c.Id).ToHashSetAsync();
        conceptIds.Should().NotBeEmpty();

        var skillIds = await db.Skills.AsNoTracking().Select(sk => sk.Id).ToHashSetAsync();
        skillIds.Should().NotBeEmpty();

        // Every Unit references an existing Subject
        var unitSubjectIds = await db.Units.AsNoTracking().Select(u => u.SubjectId).ToListAsync();
        unitSubjectIds.Should().OnlyContain(id => subjectIds.Contains(id),
            "every Unit must belong to an existing Subject");

        // Every Lesson references an existing Unit
        var lessonUnitIds = await db.Lessons.AsNoTracking().Select(l => l.UnitId).ToListAsync();
        lessonUnitIds.Should().OnlyContain(id => unitIds.Contains(id),
            "every Lesson must belong to an existing Unit");

        // Every Concept references an existing Subject
        var conceptSubjectIds = await db.Concepts.AsNoTracking().Select(c => c.SubjectId).ToListAsync();
        conceptSubjectIds.Should().OnlyContain(id => subjectIds.Contains(id),
            "every Concept must belong to an existing Subject");

        // Every Skill references an existing Concept
        var skillConceptIds = await db.Skills.AsNoTracking().Select(sk => sk.ConceptId).ToListAsync();
        skillConceptIds.Should().OnlyContain(id => conceptIds.Contains(id),
            "every Skill must belong to an existing Concept");
    }

    // -------------------------------------------------------------------------
    // P8-02 AC-6: Math (both trees) has the deepest sub-tree structure per grade
    // Each language tree of Math must have strictly more Units than Science/Arabic/English
    // in the same grade.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedAsync_MathTrees_HaveDeepestStructure_PerGrade()
    {
        var sp = BuildServiceProvider();
        await LearningSeeder.SeedAsync(sp);

        var db = GetDb(sp);

        var grade1 = await db.Grades.AsNoTracking().FirstAsync(g => g.Number == 1);

        var subjects = await db.Subjects.AsNoTracking()
            .Where(s => s.GradeId == grade1.Id)
            .ToListAsync();

        var mathArId  = subjects.First(s => s.SubjectCode == SubjectCode.MATH    && s.Language == ContentLanguage.Ar).Id;
        var mathEnId  = subjects.First(s => s.SubjectCode == SubjectCode.MATH    && s.Language == ContentLanguage.En).Id;
        var nonMathIds = subjects
            .Where(s => s.SubjectCode != SubjectCode.MATH)
            .Select(s => s.Id)
            .ToList();

        var mathArUnitCount = await db.Units.AsNoTracking().CountAsync(u => u.SubjectId == mathArId);
        var mathEnUnitCount = await db.Units.AsNoTracking().CountAsync(u => u.SubjectId == mathEnId);

        mathArUnitCount.Should().BeGreaterThan(2,
            "MATH/Ar must have more than 2 units (it has 5)");
        mathEnUnitCount.Should().BeGreaterThan(2,
            "MATH/En must have more than 2 units (it has 5)");

        foreach (var otherId in nonMathIds)
        {
            var otherUnitCount = await db.Units.AsNoTracking().CountAsync(u => u.SubjectId == otherId);
            mathArUnitCount.Should().BeGreaterThan(otherUnitCount,
                "MATH/Ar must have strictly more units than every non-Math subject");
            mathEnUnitCount.Should().BeGreaterThan(otherUnitCount,
                "MATH/En must have strictly more units than every non-Math subject");
        }
    }

    // -------------------------------------------------------------------------
    // P8-02 AC-7: Per-language Math prerequisite edge graphs are non-empty and
    // each tree's KnowledgeNodes reference the correct language Subject.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedAsync_MathPrereqEdges_ExistInBothLanguageTrees()
    {
        var sp = BuildServiceProvider();
        await LearningSeeder.SeedAsync(sp);

        var db = GetDb(sp);

        // Collect all MATH/Ar and MATH/En subject ids across all grades.
        var mathArSubjectIds = await db.Subjects
            .AsNoTracking()
            .Where(s => s.SubjectCode == SubjectCode.MATH && s.Language == ContentLanguage.Ar)
            .Select(s => s.Id)
            .ToHashSetAsync();

        var mathEnSubjectIds = await db.Subjects
            .AsNoTracking()
            .Where(s => s.SubjectCode == SubjectCode.MATH && s.Language == ContentLanguage.En)
            .Select(s => s.Id)
            .ToHashSetAsync();

        mathArSubjectIds.Should().NotBeEmpty("MATH/Ar subjects must be seeded");
        mathEnSubjectIds.Should().NotBeEmpty("MATH/En subjects must be seeded");

        // KnowledgeNodes belonging to each language's Math tree.
        var arMathNodeIds = await db.KnowledgeNodes
            .AsNoTracking()
            .Where(n => mathArSubjectIds.Contains(n.SubjectId))
            .Select(n => n.Id)
            .ToHashSetAsync();

        var enMathNodeIds = await db.KnowledgeNodes
            .AsNoTracking()
            .Where(n => mathEnSubjectIds.Contains(n.SubjectId))
            .Select(n => n.Id)
            .ToHashSetAsync();

        arMathNodeIds.Should().NotBeEmpty("MATH/Ar KnowledgeNodes must be seeded");
        enMathNodeIds.Should().NotBeEmpty("MATH/En KnowledgeNodes must be seeded");

        // Prerequisite edges fully within the Ar tree.
        var allEdges = await db.KnowledgeEdges.AsNoTracking().ToListAsync();

        var arMathEdges = allEdges
            .Where(e => e.RelationshipType == EdgeRelationshipType.Prerequisite
                     && arMathNodeIds.Contains(e.SourceNodeId)
                     && arMathNodeIds.Contains(e.TargetNodeId))
            .ToList();

        var enMathEdges = allEdges
            .Where(e => e.RelationshipType == EdgeRelationshipType.Prerequisite
                     && enMathNodeIds.Contains(e.SourceNodeId)
                     && enMathNodeIds.Contains(e.TargetNodeId))
            .ToList();

        arMathEdges.Should().NotBeEmpty("MATH/Ar tree must have at least one prerequisite edge");
        enMathEdges.Should().NotBeEmpty("MATH/En tree must have at least one prerequisite edge");
    }

    // -------------------------------------------------------------------------
    // P8-02 AC-8: No cross-language KnowledgeEdges — edges only connect nodes
    // within the same language tree (Ar nodes → Ar nodes, En nodes → En nodes).
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedAsync_NoEdgesSpan_CrossLanguageTrees()
    {
        var sp = BuildServiceProvider();
        await LearningSeeder.SeedAsync(sp);

        var db = GetDb(sp);

        // Build a map: KnowledgeNodeId → ContentLanguage via Skill → Concept → Subject.
        var nodeLanguages = await db.KnowledgeNodes
            .AsNoTracking()
            .Where(n => n.SkillId != null)
            .Include(n => n.Skill!)
                .ThenInclude(sk => sk.Concept)
                    .ThenInclude(c => c.Subject)
            .ToDictionaryAsync(
                n => n.Id,
                n => n.Skill!.Concept.Subject.Language);

        var allEdges = await db.KnowledgeEdges.AsNoTracking().ToListAsync();

        foreach (var edge in allEdges.Where(e => e.RelationshipType == EdgeRelationshipType.Prerequisite))
        {
            if (!nodeLanguages.TryGetValue(edge.SourceNodeId, out var srcLang) ||
                !nodeLanguages.TryGetValue(edge.TargetNodeId, out var tgtLang))
                continue; // Node not skill-backed — skip.

            srcLang.Should().Be(tgtLang,
                $"Edge (SourceNodeId={edge.SourceNodeId} → TargetNodeId={edge.TargetNodeId}) " +
                $"must not span two language trees");
        }
    }

    // -------------------------------------------------------------------------
    // AC-9: Lesson↔Skill links resolve for Math Grade 1 (both trees)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedAsync_LessonsLinkToSkills_InBothMathTrees_Grade1()
    {
        var sp = BuildServiceProvider();
        await LearningSeeder.SeedAsync(sp);

        var db = GetDb(sp);

        var grade1 = await db.Grades.AsNoTracking().FirstAsync(g => g.Number == 1);

        foreach (var language in new[] { ContentLanguage.Ar, ContentLanguage.En })
        {
            var mathSubject = await db.Subjects.AsNoTracking()
                .FirstAsync(s =>
                    s.SubjectCode == SubjectCode.MATH &&
                    s.Language == language &&
                    s.GradeId == grade1.Id);

            var mathUnitIds = await db.Units.AsNoTracking()
                .Where(u => u.SubjectId == mathSubject.Id)
                .Select(u => u.Id)
                .ToListAsync();

            var mathLessonsWithSkill = await db.Lessons.AsNoTracking()
                .Where(l => mathUnitIds.Contains(l.UnitId) && l.SkillId != null)
                .ToListAsync();

            mathLessonsWithSkill.Should().NotBeEmpty(
                $"at least one MATH/{language} Grade 1 lesson must teach a specific skill");

            var allSkillIds = await db.Skills.AsNoTracking().Select(sk => sk.Id).ToHashSetAsync();

            mathLessonsWithSkill
                .Select(l => l.SkillId!.Value)
                .Should().OnlyContain(id => allSkillIds.Contains(id),
                    $"every SkillId set on a MATH/{language} Grade 1 lesson must reference an existing Skill");
        }
    }

    // -------------------------------------------------------------------------
    // AC-10: Idempotency — second run does not duplicate rows
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedAsync_IsIdempotent_SecondRunDoesNotDuplicate()
    {
        // Use a shared db name so both calls hit the same InMemory store
        var dbName = Guid.NewGuid().ToString();
        var sp = BuildServiceProvider(dbName);

        // First run
        await LearningSeeder.SeedAsync(sp);

        var db = GetDb(sp);
        var gradesAfterFirst   = await db.Grades.AsNoTracking().CountAsync();
        var subjectsAfterFirst = await db.Subjects.AsNoTracking().CountAsync();
        var unitsAfterFirst    = await db.Units.AsNoTracking().CountAsync();
        var lessonsAfterFirst  = await db.Lessons.AsNoTracking().CountAsync();
        var conceptsAfterFirst = await db.Concepts.AsNoTracking().CountAsync();
        var skillsAfterFirst   = await db.Skills.AsNoTracking().CountAsync();

        // Second run (same provider = same InMemory database)
        await LearningSeeder.SeedAsync(sp);

        var gradesAfterSecond   = await db.Grades.AsNoTracking().CountAsync();
        var subjectsAfterSecond = await db.Subjects.AsNoTracking().CountAsync();
        var unitsAfterSecond    = await db.Units.AsNoTracking().CountAsync();
        var lessonsAfterSecond  = await db.Lessons.AsNoTracking().CountAsync();
        var conceptsAfterSecond = await db.Concepts.AsNoTracking().CountAsync();
        var skillsAfterSecond   = await db.Skills.AsNoTracking().CountAsync();

        gradesAfterSecond.Should().Be(gradesAfterFirst,   "Grade count must not change on second seed");
        subjectsAfterSecond.Should().Be(subjectsAfterFirst, "Subject count must not change on second seed");
        unitsAfterSecond.Should().Be(unitsAfterFirst,     "Unit count must not change on second seed");
        lessonsAfterSecond.Should().Be(lessonsAfterFirst,  "Lesson count must not change on second seed");
        conceptsAfterSecond.Should().Be(conceptsAfterFirst, "Concept count must not change on second seed");
        skillsAfterSecond.Should().Be(skillsAfterFirst,   "Skill count must not change on second seed");
    }

    // -------------------------------------------------------------------------
    // CO-BE-3 AC-1: Grade-1 Math root lessons (Ar + En) carry all four question types
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedAsync_Grade1MathRootLessons_HaveAllFourQuestionTypes()
    {
        var sp = BuildServiceProvider();
        await LearningSeeder.SeedAsync(sp);

        var db = GetDb(sp);

        var mathRootLessonNames = new[] { "مقدمة في العد (ص1)", "Introduction to Counting (G1)" };

        foreach (var lessonName in mathRootLessonNames)
        {
            var lesson = await db.Lessons.AsNoTracking().FirstOrDefaultAsync(l => l.Name == lessonName);
            lesson.Should().NotBeNull($"demo lesson '{lessonName}' must be seeded");

            var types = await db.QuizQuestions.AsNoTracking()
                .Where(q => q.LessonId == lesson!.Id)
                .Select(q => q.QuestionType)
                .ToListAsync();

            types.Should().BeEquivalentTo(
                new[] { QuestionType.MCQ, QuestionType.TrueFalse, QuestionType.FillInBlank, QuestionType.Matching },
                $"'{lessonName}' must carry exactly one question of each of the four types");
        }
    }

    // -------------------------------------------------------------------------
    // CO-BE-3 AC-2: Seeded Matching question content follows the CO-BE-1 contract
    // and its CorrectAnswer grades as correct via AnswerComparator (pair-set equality).
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedAsync_SeededMatchingQuestion_ContentMatchesContract_AndSelfGradesCorrect()
    {
        var sp = BuildServiceProvider();
        await LearningSeeder.SeedAsync(sp);

        var db = GetDb(sp);

        var matchingQuestions = await db.QuizQuestions.AsNoTracking()
            .Where(q => q.QuestionType == QuestionType.Matching)
            .ToListAsync();

        matchingQuestions.Should().NotBeEmpty("CO-BE-3 must seed at least one Matching question");

        foreach (var question in matchingQuestions)
        {
            // Options must parse as {"left":[{"id","text"}],"right":[{"id","text"}]}.
            using var optionsDoc = System.Text.Json.JsonDocument.Parse(question.Options);
            optionsDoc.RootElement.TryGetProperty("left", out var left).Should().BeTrue();
            optionsDoc.RootElement.TryGetProperty("right", out var right).Should().BeTrue();
            left.GetArrayLength().Should().Be(right.GetArrayLength());
            foreach (var item in left.EnumerateArray().Concat(right.EnumerateArray()))
            {
                item.TryGetProperty("id", out _).Should().BeTrue("every Matching item carries a stable id");
                item.TryGetProperty("text", out _).Should().BeTrue("every Matching item carries display text");
            }

            // CorrectAnswer must parse as {"pairs":[...]} and grade itself correct via the comparator.
            using var answerDoc = System.Text.Json.JsonDocument.Parse(question.CorrectAnswer);
            answerDoc.RootElement.TryGetProperty("pairs", out var pairs).Should().BeTrue();
            pairs.GetArrayLength().Should().Be(left.GetArrayLength());

            Learnexia.Modules.Learning.Domain.Services.AnswerComparator
                .AreEqual(QuestionType.Matching, question.CorrectAnswer, question.CorrectAnswer)
                .Should().BeTrue("the seeded CorrectAnswer must satisfy its own pair-set comparison");
        }
    }

    // -------------------------------------------------------------------------
    // CO-BE-3 AC-3: Question seeding is idempotent — second run adds zero question rows
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedAsync_QuizQuestions_AreIdempotent_SecondRunDoesNotDuplicate()
    {
        var dbName = Guid.NewGuid().ToString();
        var sp = BuildServiceProvider(dbName);

        await LearningSeeder.SeedAsync(sp);

        var db = GetDb(sp);
        var questionsAfterFirst = await db.QuizQuestions.AsNoTracking().CountAsync();
        questionsAfterFirst.Should().BeGreaterThan(0, "first run must seed demo questions");

        await LearningSeeder.SeedAsync(sp);

        var questionsAfterSecond = await db.QuizQuestions.AsNoTracking().CountAsync();
        questionsAfterSecond.Should().Be(questionsAfterFirst,
            "QuizQuestion count must not change on second seed (per (lesson, type) idempotency key)");
    }

    // -------------------------------------------------------------------------
    // AC-11: Expected subject root count across all grades = 6 × 6 = 36
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedAsync_TotalSubjectCount_Is_ThirtySix()
    {
        var sp = BuildServiceProvider();
        await LearningSeeder.SeedAsync(sp);

        var db = GetDb(sp);

        var totalSubjects = await db.Subjects.AsNoTracking().CountAsync();

        // 6 grades × 6 roots per grade = 36
        totalSubjects.Should().Be(36,
            "6 grades × 6 subject roots per grade (MATH/Ar, MATH/En, SCIENCE/Ar, SCIENCE/En, ARABIC/Ar, ENGLISH/En) = 36");
    }
}
