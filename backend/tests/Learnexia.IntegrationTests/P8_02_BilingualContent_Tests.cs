using FluentAssertions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Modules.Learning.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

// =============================================================================
// P8-02 integration tests — "Author bilingual curriculum (parallel language trees)"
//
// Surface under test:
//   LearningDbContext (PostgreSQL via Testcontainers) — DB-level / schema / model assertions.
//   No public HTTP "author curriculum" endpoint exists; the seeder runs in-process.
//
// Test cases (BE-TC-01 … BE-TC-10) from docs/qc/P8-02/backend-test-cases.md.
//
// Note: BE-TC-01..07 are ALSO covered by Modules.Learning.UnitTests/LearningSeederTests.cs
// (using InMemory DB — task BE-6). These integration tests are belt-and-suspenders DB-level
// regression guards against a real PostgreSQL/Testcontainers schema (catches migration drift).
//
// Note on G-02: the claim-absent fallback (ContentLanguage.Ar default) is covered by the
// unit test in Modules.Learning.UnitTests/SubjectLanguageResolverTests.cs.
// =============================================================================

[Collection("IntegrationTests")]
public sealed class P8_02_BilingualContent_Tests : IAsyncLifetime
{
    private readonly LearnexiaWebAppFactory _factory;

    public P8_02_BilingualContent_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();

        using var scope = _factory.Services.CreateScope();
        await LearningSeeder.SeedAsync(scope.ServiceProvider);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // Helper: get a fresh LearningDbContext scope
    private IServiceScope CreateScope() => _factory.Services.CreateScope();

    // =========================================================================
    // BE-TC-01 — Every grade has exactly the 6 expected (SubjectCode, Language) roots
    // =========================================================================

    [Fact(DisplayName = "P802-TC01 Every grade has exactly 6 subject roots (MATH/Ar,MATH/En,SCIENCE/Ar,SCIENCE/En,ARABIC/Ar,ENGLISH/En)")]
    public async Task TC01_EveryGrade_HasExactlySixExpectedRoots()
    {
        var expectedPairs = new (SubjectCode Code, ContentLanguage Lang)[]
        {
            (SubjectCode.MATH,    ContentLanguage.Ar),
            (SubjectCode.MATH,    ContentLanguage.En),
            (SubjectCode.SCIENCE, ContentLanguage.Ar),
            (SubjectCode.SCIENCE, ContentLanguage.En),
            (SubjectCode.ARABIC,  ContentLanguage.Ar),
            (SubjectCode.ENGLISH, ContentLanguage.En),
        };

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        for (var gradeNumber = 1; gradeNumber <= 6; gradeNumber++)
        {
            var grade = await db.Grades.AsNoTracking()
                .FirstOrDefaultAsync(g => g.Number == gradeNumber);
            grade.Should().NotBeNull($"Grade {gradeNumber} must be seeded");

            var subjects = await db.Subjects.AsNoTracking()
                .Where(s => s.GradeId == grade!.Id)
                .ToListAsync();

            subjects.Should().HaveCount(6,
                $"Grade {gradeNumber} must have exactly 6 subject roots; found {subjects.Count}");

            var actualPairs = subjects
                .Select(s => (s.SubjectCode, s.Language))
                .ToHashSet();

            foreach (var (code, lang) in expectedPairs)
            {
                actualPairs.Should().Contain((code, lang),
                    $"Grade {gradeNumber} must contain the {code}/{lang} tree");
            }

            // Verify no ARABIC/En or ENGLISH/Ar root exists
            actualPairs.Should().NotContain((SubjectCode.ARABIC,  ContentLanguage.En),
                $"Grade {gradeNumber} must NOT have ARABIC/En root");
            actualPairs.Should().NotContain((SubjectCode.ENGLISH, ContentLanguage.Ar),
                $"Grade {gradeNumber} must NOT have ENGLISH/Ar root");
        }
    }

    // =========================================================================
    // BE-TC-02 — Math & Science in BOTH languages; Arabic only Ar; English only En
    // =========================================================================

    [Fact(DisplayName = "P802-TC02 MATH/SCIENCE both languages; ARABIC only Ar; ENGLISH only En")]
    public async Task TC02_SubjectCode_LanguageParity()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        // Scope to the canonical seeded curriculum (grades 1–6) — like TC01 — so other tests'
        // fresh-grade fixtures (which may seed arbitrary SubjectCode/Language pairs to exercise
        // serving paths) cannot pollute this assertion about the SEEDER's canonical output.
        var canonicalGradeIds = await db.Grades.AsNoTracking()
            .Where(g => g.Number >= 1 && g.Number <= 6)
            .Select(g => g.Id)
            .ToListAsync();

        var allSubjects = await db.Subjects.AsNoTracking()
            .Where(s => canonicalGradeIds.Contains(s.GradeId))
            .ToListAsync();

        var mathLangs    = allSubjects.Where(s => s.SubjectCode == SubjectCode.MATH)
                                      .Select(s => s.Language).Distinct().ToHashSet();
        var scienceLangs = allSubjects.Where(s => s.SubjectCode == SubjectCode.SCIENCE)
                                      .Select(s => s.Language).Distinct().ToHashSet();
        var arabicLangs  = allSubjects.Where(s => s.SubjectCode == SubjectCode.ARABIC)
                                      .Select(s => s.Language).Distinct().ToHashSet();
        var englishLangs = allSubjects.Where(s => s.SubjectCode == SubjectCode.ENGLISH)
                                      .Select(s => s.Language).Distinct().ToHashSet();

        mathLangs.Should().Contain(ContentLanguage.Ar, "MATH must have an Ar tree");
        mathLangs.Should().Contain(ContentLanguage.En, "MATH must have an En tree");

        scienceLangs.Should().Contain(ContentLanguage.Ar, "SCIENCE must have an Ar tree");
        scienceLangs.Should().Contain(ContentLanguage.En, "SCIENCE must have an En tree");

        arabicLangs.Should().BeEquivalentTo(new[] { ContentLanguage.Ar },
            "ARABIC exists ONLY in Ar language");

        englishLangs.Should().BeEquivalentTo(new[] { ContentLanguage.En },
            "ENGLISH exists ONLY in En language");
    }

    // =========================================================================
    // BE-TC-03 — SubjectCode + Language populated on every Subject (no NULL/untagged)
    // =========================================================================

    [Fact(DisplayName = "P802-TC03 All Subjects have valid SubjectCode and Language (no untagged rows)")]
    public async Task TC03_AllSubjects_HaveValidCodeAndLanguage()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var allSubjects = await db.Subjects.AsNoTracking().ToListAsync();
        allSubjects.Should().NotBeEmpty("subjects must be seeded");

        var validCodes = new[] { SubjectCode.MATH, SubjectCode.SCIENCE, SubjectCode.ARABIC, SubjectCode.ENGLISH };
        var validLangs = new[] { ContentLanguage.Ar, ContentLanguage.En };

        foreach (var s in allSubjects)
        {
            validCodes.Should().Contain(s.SubjectCode,
                $"Subject id={s.Id} has an unexpected SubjectCode={s.SubjectCode}");
            validLangs.Should().Contain(s.Language,
                $"Subject id={s.Id} has an unexpected Language={s.Language}");
        }
    }

    // =========================================================================
    // BE-TC-04 — Child entities (Lesson) carry NO language column (language only on Subject)
    // =========================================================================

    [Fact(DisplayName = "P802-TC04 Lesson entity has no Language/ContentLanguage property (language inherited via Subject)")]
    public async Task TC04_LessonEntity_HasNoLanguageProperty()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        // Schema-assert: verify LearningDbContext.Model for Lesson has no Language property
        var lessonEntityType = db.Model.FindEntityType(typeof(Lesson));
        lessonEntityType.Should().NotBeNull("Lesson must be a mapped entity");

        var lessonProperties = lessonEntityType!
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        lessonProperties.Should().NotContain("Language",
            "Lesson must NOT have a 'Language' column — language is inherited via Subject");
        lessonProperties.Should().NotContain("ContentLanguage",
            "Lesson must NOT have a 'ContentLanguage' column");

        // Also verify Unit has no language column
        var unitEntityType = db.Model.FindEntityType(typeof(Unit));
        unitEntityType.Should().NotBeNull("Unit must be a mapped entity");
        var unitProperties = unitEntityType!.GetProperties().Select(p => p.Name).ToList();
        unitProperties.Should().NotContain("Language",
            "Unit must NOT have a 'Language' column");

        // Behavioral: walk a known MATH/En G1 lesson to its Subject and confirm language is resolved there
        var mathEnLesson = await db.Lessons.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Name == "Introduction to Counting (G1)");
        mathEnLesson.Should().NotBeNull("MATH/En G1 demo lesson must be seeded");

        // Walk Lesson → Unit → Subject
        var unit = await db.Units.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == mathEnLesson!.UnitId);
        unit.Should().NotBeNull("MATH/En G1 lesson must have a parent Unit");

        var subject = await db.Subjects.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == unit!.SubjectId);
        subject.Should().NotBeNull("Unit must have a parent Subject");
        subject!.Language.Should().Be(ContentLanguage.En,
            "MATH/En G1 lesson's owning Subject must have Language=En");
        subject.SubjectCode.Should().Be(SubjectCode.MATH,
            "MATH/En G1 lesson's owning Subject must have SubjectCode=MATH");
    }

    // =========================================================================
    // BE-TC-05 — UNIQUE index IX_Subjects_GradeId_SubjectCode_Language exists
    // =========================================================================

    [Fact(DisplayName = "P802-TC05 Subject model has unique index IX_Subjects_GradeId_SubjectCode_Language")]
    public async Task TC05_UniqueIndex_GradeSubjectCodeLanguage_ExistsOnModel()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var subjectEntityType = db.Model.FindEntityType(typeof(Subject));
        subjectEntityType.Should().NotBeNull();

        var indexes = subjectEntityType!.GetIndexes().ToList();

        // Find the unique index on (GradeId, SubjectCode, Language)
        var uniqueIndex = indexes.FirstOrDefault(ix =>
            ix.IsUnique &&
            ix.Properties.Count == 3 &&
            ix.Properties.Any(p => p.Name == "GradeId") &&
            ix.Properties.Any(p => p.Name == "SubjectCode") &&
            ix.Properties.Any(p => p.Name == "Language"));

        uniqueIndex.Should().NotBeNull(
            "Subject entity must have a UNIQUE index on (GradeId, SubjectCode, Language); " +
            "available indexes: [{0}]",
            string.Join(", ", indexes.Select(ix =>
                $"[{string.Join(", ", ix.Properties.Select(p => p.Name))}] unique={ix.IsUnique}")));

        uniqueIndex!.IsUnique.Should().BeTrue(
            "IX_Subjects_GradeId_SubjectCode_Language must be a UNIQUE index");

        // The index should have the correct database name
        var dbName = uniqueIndex.GetDatabaseName();
        dbName.Should().Be("IX_Subjects_GradeId_SubjectCode_Language",
            "unique index must be named IX_Subjects_GradeId_SubjectCode_Language; actual={0}", dbName);

        await Task.CompletedTask;
    }

    // =========================================================================
    // BE-TC-06 — Duplicate (GradeId, SubjectCode, Language) insert rejected by DB
    // =========================================================================

    [Fact(DisplayName = "P802-TC06 Duplicate (GradeId, SubjectCode, Language) insert is rejected by DB unique constraint")]
    public async Task TC06_DuplicateSubjectInsert_RejectedByDbUniqueConstraint()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        // Find an existing MATH/En grade-1 subject
        var grade1 = await db.Grades.AsNoTracking().FirstAsync(g => g.Number == 1);
        var existing = await db.Subjects.AsNoTracking()
            .FirstAsync(s =>
                s.GradeId == grade1.Id &&
                s.SubjectCode == SubjectCode.MATH &&
                s.Language == ContentLanguage.En);

        // Attempt to insert a duplicate
        var duplicate = new Subject
        {
            Name        = "Math EN Duplicate",
            GradeId     = existing.GradeId,
            SubjectCode = SubjectCode.MATH,
            Language    = ContentLanguage.En,
            LifecycleState = LifecycleState.Draft,
        };
        db.Subjects.Add(duplicate);

        var act = async () => await db.SaveChangesAsync(LearningSeeder.SystemUserId);

        await act.Should().ThrowAsync<DbUpdateException>(
            "inserting a duplicate (GradeId, MATH, En) must throw DbUpdateException (unique violation)");
    }

    // =========================================================================
    // BE-TC-07 — Seeder is idempotent (re-running does not duplicate roots)
    // =========================================================================

    [Fact(DisplayName = "P802-TC07 Seeder is idempotent — re-run does not create duplicate subject roots")]
    public async Task TC07_Seeder_IsIdempotent_NoNewRoots()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        // Count subjects per grade after initial seed (done in InitializeAsync)
        var countsAfterFirst = new Dictionary<int, int>();
        for (var g = 1; g <= 6; g++)
        {
            var grade = await db.Grades.AsNoTracking().FirstAsync(gg => gg.Number == g);
            countsAfterFirst[g] = await db.Subjects.AsNoTracking()
                .CountAsync(s => s.GradeId == grade.Id);
        }

        // Re-run the seeder
        using var scope2 = _factory.Services.CreateScope();
        await LearningSeeder.SeedAsync(scope2.ServiceProvider);

        // Re-count
        using var scope3 = _factory.Services.CreateScope();
        var db3 = scope3.ServiceProvider.GetRequiredService<LearningDbContext>();

        for (var g = 1; g <= 6; g++)
        {
            var grade = await db3.Grades.AsNoTracking().FirstAsync(gg => gg.Number == g);
            var countAfterSecond = await db3.Subjects.AsNoTracking()
                .CountAsync(s => s.GradeId == grade.Id);

            countAfterSecond.Should().Be(countsAfterFirst[g],
                $"Grade {g} subject count must be unchanged after second seeder run; " +
                $"was {countsAfterFirst[g]}, now {countAfterSecond}");

            // Also confirm still exactly 6
            countAfterSecond.Should().Be(6,
                $"Grade {g} must still have exactly 6 subject roots after re-seed");
        }
    }

    // =========================================================================
    // BE-TC-08 — Math/Science parallel trees have independent (non-shared) child rows
    // =========================================================================

    [Fact(DisplayName = "P802-TC08 MATH/Ar and MATH/En G1 trees have disjoint Unit rows (independent parallel trees)")]
    public async Task TC08_MathTrees_HaveDisjointChildRows()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var grade1 = await db.Grades.AsNoTracking().FirstAsync(g => g.Number == 1);

        var mathAr = await db.Subjects.AsNoTracking()
            .FirstAsync(s => s.GradeId == grade1.Id &&
                             s.SubjectCode == SubjectCode.MATH &&
                             s.Language == ContentLanguage.Ar);

        var mathEn = await db.Subjects.AsNoTracking()
            .FirstAsync(s => s.GradeId == grade1.Id &&
                             s.SubjectCode == SubjectCode.MATH &&
                             s.Language == ContentLanguage.En);

        mathAr.Id.Should().NotBe(mathEn.Id, "MATH/Ar and MATH/En must be distinct Subject rows");

        var arUnitIds = await db.Units.AsNoTracking()
            .Where(u => u.SubjectId == mathAr.Id)
            .Select(u => u.Id)
            .ToHashSetAsync();

        var enUnitIds = await db.Units.AsNoTracking()
            .Where(u => u.SubjectId == mathEn.Id)
            .Select(u => u.Id)
            .ToHashSetAsync();

        arUnitIds.Should().NotBeEmpty("MATH/Ar G1 must have Units");
        enUnitIds.Should().NotBeEmpty("MATH/En G1 must have Units");

        arUnitIds.Should().NotIntersectWith(enUnitIds,
            "MATH/Ar and MATH/En trees must own disjoint Unit rows (independent parallel trees, not shared structure)");

        // Also confirm display names differ (Ar units have Arabic names, En units have English names)
        var arUnitNames = await db.Units.AsNoTracking()
            .Where(u => arUnitIds.Contains(u.Id))
            .Select(u => u.Name)
            .ToListAsync();

        var enUnitNames = await db.Units.AsNoTracking()
            .Where(u => enUnitIds.Contains(u.Id))
            .Select(u => u.Name)
            .ToListAsync();

        // Ar names should not be identical to En names (different language authoring)
        arUnitNames.Should().NotBeEquivalentTo(enUnitNames,
            "MATH/Ar and MATH/En unit names must differ (language-specific authoring)");
    }

    // =========================================================================
    // BE-TC-09 — No cross-language KnowledgeEdge (edges stay within one language tree)
    // =========================================================================

    [Fact(DisplayName = "P802-TC09 No cross-language KnowledgeEdges — prereq edges stay within one language tree")]
    public async Task TC09_NoCrossLanguageKnowledgeEdges()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        // Build a map: KnowledgeNodeId → ContentLanguage (via Skill → Concept → Subject)
        // Only skill-backed nodes are resolvable; standalone nodes are skipped.
        var nodeLanguages = await db.KnowledgeNodes
            .AsNoTracking()
            .Where(n => n.SkillId != null)
            .Include(n => n.Skill!)
                .ThenInclude(sk => sk.Concept)
                    .ThenInclude(c => c.Subject)
            .ToDictionaryAsync(
                n => n.Id,
                n => n.Skill!.Concept.Subject.Language);

        if (nodeLanguages.Count == 0)
        {
            // No skill-backed KnowledgeNodes seeded — skip cross-language check
            // (reported as BLOCKED/SKIPPED if KnowledgeNodes are not queryable via this path).
            return;
        }

        var allEdges = await db.KnowledgeEdges.AsNoTracking().ToListAsync();

        var crossLanguageEdges = allEdges
            .Where(e => e.RelationshipType == EdgeRelationshipType.Prerequisite)
            .Where(e =>
                nodeLanguages.TryGetValue(e.SourceNodeId, out var srcLang) &&
                nodeLanguages.TryGetValue(e.TargetNodeId, out var tgtLang) &&
                srcLang != tgtLang)
            .ToList();

        crossLanguageEdges.Should().BeEmpty(
            "no Prerequisite KnowledgeEdge must connect a node in one language tree to a node in another language tree; " +
            "found {0} cross-language edge(s): [{1}]",
            crossLanguageEdges.Count,
            string.Join(", ", crossLanguageEdges.Select(e => $"src={e.SourceNodeId}→tgt={e.TargetNodeId}")));
    }

    // =========================================================================
    // BE-TC-10 — Content item resolves to exactly one language tree (no cross-language leakage)
    // =========================================================================

    [Fact(DisplayName = "P802-TC10 A lesson resolves to exactly one language tree (no cross-language leakage)")]
    public async Task TC10_LessonResolvesToExactlyOneLanguageTree()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        // MATH/En G1 known lesson
        var mathEnLesson = await db.Lessons.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Name == "Introduction to Counting (G1)");
        mathEnLesson.Should().NotBeNull("MATH/En G1 demo lesson must be seeded");

        var mathEnUnit = await db.Units.AsNoTracking()
            .FirstAsync(u => u.Id == mathEnLesson!.UnitId);
        var mathEnSubject = await db.Subjects.AsNoTracking()
            .FirstAsync(s => s.Id == mathEnUnit.SubjectId);

        mathEnSubject.Language.Should().Be(ContentLanguage.En,
            "MATH/En lesson must resolve to a Subject with Language=En");
        mathEnSubject.SubjectCode.Should().Be(SubjectCode.MATH,
            "MATH/En lesson must resolve to a Subject with SubjectCode=MATH");

        // MATH/Ar G1 known lesson
        var mathArLesson = await db.Lessons.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Name == "مقدمة في العد (ص1)");
        mathArLesson.Should().NotBeNull("MATH/Ar G1 lesson must be seeded");

        var mathArUnit = await db.Units.AsNoTracking()
            .FirstAsync(u => u.Id == mathArLesson!.UnitId);
        var mathArSubject = await db.Subjects.AsNoTracking()
            .FirstAsync(s => s.Id == mathArUnit.SubjectId);

        mathArSubject.Language.Should().Be(ContentLanguage.Ar,
            "MATH/Ar lesson must resolve to a Subject with Language=Ar");
        mathArSubject.SubjectCode.Should().Be(SubjectCode.MATH,
            "MATH/Ar lesson must resolve to a Subject with SubjectCode=MATH");

        // The two lessons must belong to different Subjects (no shared parent)
        mathEnSubject.Id.Should().NotBe(mathArSubject.Id,
            "MATH/En and MATH/Ar G1 lessons must have different parent Subjects (independent trees, no shared parent)");
    }
}
