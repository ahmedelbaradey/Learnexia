using FluentAssertions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Modules.Learning.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Modules.Learning.UnitTests;

/// <summary>
/// Unit tests for <see cref="LearningSeeder"/>. Each test gets a fresh InMemory database via
/// <see cref="BuildServiceProvider"/> so tests are fully isolated.
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
    // AC-1: Exactly 4 subjects per grade (Math, Science, Arabic, English)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedAsync_Creates_ExactlyFourSubjects_PerGrade()
    {
        var sp = BuildServiceProvider();
        await LearningSeeder.SeedAsync(sp);

        var db = GetDb(sp);

        // Grade 1 must exist
        var grade1 = await db.Grades.AsNoTracking().FirstOrDefaultAsync(g => g.Number == 1);
        grade1.Should().NotBeNull();

        // Exactly 4 subjects in grade 1
        var subjectsInGrade1 = await db.Subjects
            .AsNoTracking()
            .Where(s => s.GradeId == grade1!.Id)
            .ToListAsync();

        subjectsInGrade1.Should().HaveCount(4);
        subjectsInGrade1.Select(s => s.Name).Should().BeEquivalentTo(
            new[] { "Math", "Science", "Arabic", "English" });
    }

    // -------------------------------------------------------------------------
    // AC-2: Full tree linkage — no orphan nodes
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedAsync_TreeIsFullyLinked_NoOrphans()
    {
        var sp = BuildServiceProvider();
        await LearningSeeder.SeedAsync(sp);

        var db = GetDb(sp);

        // All subject IDs that exist
        var subjectIds = await db.Subjects.AsNoTracking().Select(s => s.Id).ToHashSetAsync();
        subjectIds.Should().NotBeEmpty();

        // All unit IDs that exist
        var unitIds = await db.Units.AsNoTracking().Select(u => u.Id).ToHashSetAsync();
        unitIds.Should().NotBeEmpty();

        // All concept IDs that exist
        var conceptIds = await db.Concepts.AsNoTracking().Select(c => c.Id).ToHashSetAsync();
        conceptIds.Should().NotBeEmpty();

        // All skill IDs that exist
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
    // AC-3: Lesson↔Skill links resolve for Math Grade 1
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedAsync_LessonsLinkToSkills_WhereSet()
    {
        var sp = BuildServiceProvider();
        await LearningSeeder.SeedAsync(sp);

        var db = GetDb(sp);

        var grade1 = await db.Grades.AsNoTracking().FirstAsync(g => g.Number == 1);
        var mathSubject = await db.Subjects.AsNoTracking()
            .FirstAsync(s => s.Name == "Math" && s.GradeId == grade1.Id);

        // At least one lesson in Math Grade 1 has SkillId set
        var mathUnitIds = await db.Units.AsNoTracking()
            .Where(u => u.SubjectId == mathSubject.Id)
            .Select(u => u.Id)
            .ToListAsync();

        var mathLessonsWithSkill = await db.Lessons.AsNoTracking()
            .Where(l => mathUnitIds.Contains(l.UnitId) && l.SkillId != null)
            .ToListAsync();

        mathLessonsWithSkill.Should().NotBeEmpty(
            "at least one Math Grade 1 lesson must teach a specific skill");

        // The SkillIds on those lessons must exist in Skills
        var allSkillIds = await db.Skills.AsNoTracking().Select(sk => sk.Id).ToHashSetAsync();

        mathLessonsWithSkill
            .Select(l => l.SkillId!.Value)
            .Should().OnlyContain(id => allSkillIds.Contains(id),
                "every SkillId set on a lesson must reference an existing Skill");
    }

    // -------------------------------------------------------------------------
    // AC-5: Math has the deepest tree per grade
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedAsync_Math_HasDeepestTree()
    {
        var sp = BuildServiceProvider();
        await LearningSeeder.SeedAsync(sp);

        var db = GetDb(sp);

        var grade1 = await db.Grades.AsNoTracking().FirstAsync(g => g.Number == 1);

        var subjects = await db.Subjects.AsNoTracking()
            .Where(s => s.GradeId == grade1.Id)
            .ToListAsync();

        var mathId = subjects.First(s => s.Name == "Math").Id;
        var othersIds = subjects.Where(s => s.Name != "Math").Select(s => s.Id).ToList();

        // Unit counts
        var mathUnitCount = await db.Units.AsNoTracking().CountAsync(u => u.SubjectId == mathId);
        foreach (var otherId in othersIds)
        {
            var otherUnitCount = await db.Units.AsNoTracking().CountAsync(u => u.SubjectId == otherId);
            mathUnitCount.Should().BeGreaterThan(otherUnitCount,
                "Math must have strictly more units than every other subject");
        }

        // Concept counts
        var mathConceptCount = await db.Concepts.AsNoTracking().CountAsync(c => c.SubjectId == mathId);
        foreach (var otherId in othersIds)
        {
            var otherConceptCount = await db.Concepts.AsNoTracking().CountAsync(c => c.SubjectId == otherId);
            mathConceptCount.Should().BeGreaterThan(otherConceptCount,
                "Math must have strictly more concepts than every other subject");
        }

        // Skill counts (via concepts belonging to the subject's concepts)
        var mathConceptIds = await db.Concepts.AsNoTracking()
            .Where(c => c.SubjectId == mathId).Select(c => c.Id).ToListAsync();
        var mathSkillCount = await db.Skills.AsNoTracking()
            .CountAsync(sk => mathConceptIds.Contains(sk.ConceptId));

        foreach (var otherId in othersIds)
        {
            var otherConceptIds = await db.Concepts.AsNoTracking()
                .Where(c => c.SubjectId == otherId).Select(c => c.Id).ToListAsync();
            var otherSkillCount = await db.Skills.AsNoTracking()
                .CountAsync(sk => otherConceptIds.Contains(sk.ConceptId));

            mathSkillCount.Should().BeGreaterThan(otherSkillCount,
                "Math must have strictly more skills than every other subject");
        }
    }

    // -------------------------------------------------------------------------
    // AC-4: Idempotency — second run does not duplicate rows
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
        var gradesAfterFirst = await db.Grades.AsNoTracking().CountAsync();
        var subjectsAfterFirst = await db.Subjects.AsNoTracking().CountAsync();
        var unitsAfterFirst = await db.Units.AsNoTracking().CountAsync();
        var lessonsAfterFirst = await db.Lessons.AsNoTracking().CountAsync();
        var conceptsAfterFirst = await db.Concepts.AsNoTracking().CountAsync();
        var skillsAfterFirst = await db.Skills.AsNoTracking().CountAsync();

        // Second run (same provider = same InMemory database)
        await LearningSeeder.SeedAsync(sp);

        var gradesAfterSecond = await db.Grades.AsNoTracking().CountAsync();
        var subjectsAfterSecond = await db.Subjects.AsNoTracking().CountAsync();
        var unitsAfterSecond = await db.Units.AsNoTracking().CountAsync();
        var lessonsAfterSecond = await db.Lessons.AsNoTracking().CountAsync();
        var conceptsAfterSecond = await db.Concepts.AsNoTracking().CountAsync();
        var skillsAfterSecond = await db.Skills.AsNoTracking().CountAsync();

        gradesAfterSecond.Should().Be(gradesAfterFirst, "Grade count must not change on second seed");
        subjectsAfterSecond.Should().Be(subjectsAfterFirst, "Subject count must not change on second seed");
        unitsAfterSecond.Should().Be(unitsAfterFirst, "Unit count must not change on second seed");
        lessonsAfterSecond.Should().Be(lessonsAfterFirst, "Lesson count must not change on second seed");
        conceptsAfterSecond.Should().Be(conceptsAfterFirst, "Concept count must not change on second seed");
        skillsAfterSecond.Should().Be(skillsAfterFirst, "Skill count must not change on second seed");
    }
}
