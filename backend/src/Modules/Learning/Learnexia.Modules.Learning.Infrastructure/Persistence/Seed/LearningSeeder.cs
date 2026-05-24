using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Learning.Infrastructure.Persistence.Seed;

/// <summary>
/// Idempotent seed of the four MVP subjects (Math, Science, Arabic, English) for all six
/// school grades (1–6). Runs outside MediatR / UnitOfWorkBehavior, so it stamps audit fields
/// itself via <see cref="LearningDbContext.SaveChangesAsync(int)"/>.
///
/// System user id convention: <see cref="SystemUserId"/> = 0.  This is the agreed constant for
/// rows that are authored by an automated seed process with no real user identity.  If a future
/// convention establishes a dedicated seed-user record, update this constant.
///
/// Invoked only in Development via <c>LearningModule.InitializeAsync</c>.  Keep the seeder
/// environment-neutral (no <c>IHostEnvironment</c> dependency) so unit tests can call it directly.
/// </summary>
public static class LearningSeeder
{
    /// <summary>System user id used to stamp <c>CreatedBy</c> / <c>UpdatedBy</c> on seeded rows.</summary>
    public const int SystemUserId = 0;

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var db = serviceProvider.GetRequiredService<LearningDbContext>();

        for (var gradeNumber = 1; gradeNumber <= 6; gradeNumber++)
        {
            var gradeId = await EnsureGradeAsync(db, gradeNumber);
            await SeedMathAsync(db, gradeId, gradeNumber);
            await SeedScienceAsync(db, gradeId, gradeNumber);
            await SeedArabicAsync(db, gradeId, gradeNumber);
            await SeedEnglishAsync(db, gradeId, gradeNumber);
        }
    }

    // -------------------------------------------------------------------------
    // Grade
    // -------------------------------------------------------------------------

    private static async Task<int> EnsureGradeAsync(LearningDbContext db, int gradeNumber)
    {
        var existing = await db.Grades
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Number == gradeNumber);

        if (existing is not null)
            return existing.Id;

        var grade = new Grade
        {
            Number = gradeNumber,
            DisplayName = $"Grade {gradeNumber}",
        };
        db.Grades.Add(grade);
        await db.SaveChangesAsync(SystemUserId);
        return grade.Id;
    }

    // -------------------------------------------------------------------------
    // Math — deepest tree: 5 Units × 3 Lessons, 5 Concepts × 3 Skills (per grade)
    // -------------------------------------------------------------------------

    private static async Task SeedMathAsync(LearningDbContext db, int gradeId, int gradeNumber)
    {
        var subjectId = await EnsureSubjectAsync(db, "Math", gradeId);

        // Units (5)
        var unitIds = new int[5];
        unitIds[0] = await EnsureUnitAsync(db, $"Numbers and Place Value (G{gradeNumber})", 1, subjectId);
        unitIds[1] = await EnsureUnitAsync(db, $"Addition and Subtraction (G{gradeNumber})", 2, subjectId);
        unitIds[2] = await EnsureUnitAsync(db, $"Multiplication and Division (G{gradeNumber})", 3, subjectId);
        unitIds[3] = await EnsureUnitAsync(db, $"Fractions and Decimals (G{gradeNumber})", 4, subjectId);
        unitIds[4] = await EnsureUnitAsync(db, $"Geometry and Measurement (G{gradeNumber})", 5, subjectId);

        // Concepts (5) — seeded before lessons so SkillId can be set on lessons
        var conceptIds = new int[5];
        conceptIds[0] = await EnsureConceptAsync(db, $"Counting and Comparing (G{gradeNumber})", "Understanding quantity and order", DifficultyLevel.Easy, subjectId);
        conceptIds[1] = await EnsureConceptAsync(db, $"Basic Operations (G{gradeNumber})", "Addition, subtraction, multiplication, division", DifficultyLevel.Medium, subjectId);
        conceptIds[2] = await EnsureConceptAsync(db, $"Rational Numbers (G{gradeNumber})", "Fractions, decimals, and percentages", DifficultyLevel.Medium, subjectId);
        conceptIds[3] = await EnsureConceptAsync(db, $"Shapes and Space (G{gradeNumber})", "2D/3D shapes, angles, and symmetry", DifficultyLevel.Hard, subjectId);
        conceptIds[4] = await EnsureConceptAsync(db, $"Data and Probability (G{gradeNumber})", "Reading charts, tables, and chance", DifficultyLevel.Hard, subjectId);

        // Skills (3 per concept = 15 total)
        var skillId_C0_S0 = await EnsureSkillAsync(db, $"Count to 1000 (G{gradeNumber})", 70, 10, conceptIds[0]);
        var skillId_C0_S1 = await EnsureSkillAsync(db, $"Compare and Order Numbers (G{gradeNumber})", 75, 15, conceptIds[0]);
        var skillId_C0_S2 = await EnsureSkillAsync(db, $"Identify Even and Odd Numbers (G{gradeNumber})", 70, 10, conceptIds[0]);

        var skillId_C1_S0 = await EnsureSkillAsync(db, $"Add Single-Digit Numbers (G{gradeNumber})", 80, 15, conceptIds[1]);
        var skillId_C1_S1 = await EnsureSkillAsync(db, $"Subtract Within 100 (G{gradeNumber})", 80, 20, conceptIds[1]);
        var skillId_C1_S2 = await EnsureSkillAsync(db, $"Multiply Single-Digit Factors (G{gradeNumber})", 80, 20, conceptIds[1]);

        var skillId_C2_S0 = await EnsureSkillAsync(db, $"Identify Unit Fractions (G{gradeNumber})", 75, 20, conceptIds[2]);
        var skillId_C2_S1 = await EnsureSkillAsync(db, $"Compare Fractions with Same Denominator (G{gradeNumber})", 80, 25, conceptIds[2]);
        var skillId_C2_S2 = await EnsureSkillAsync(db, $"Convert Fractions to Decimals (G{gradeNumber})", 85, 30, conceptIds[2]);

        var skillId_C3_S0 = await EnsureSkillAsync(db, $"Classify 2D Shapes (G{gradeNumber})", 75, 15, conceptIds[3]);
        var skillId_C3_S1 = await EnsureSkillAsync(db, $"Measure Area and Perimeter (G{gradeNumber})", 80, 25, conceptIds[3]);
        var skillId_C3_S2 = await EnsureSkillAsync(db, $"Identify Lines of Symmetry (G{gradeNumber})", 75, 20, conceptIds[3]);

        var skillId_C4_S0 = await EnsureSkillAsync(db, $"Read Bar Graphs (G{gradeNumber})", 70, 15, conceptIds[4]);
        var skillId_C4_S1 = await EnsureSkillAsync(db, $"Interpret Pictographs (G{gradeNumber})", 70, 15, conceptIds[4]);
        var skillId_C4_S2 = await EnsureSkillAsync(db, $"Describe Likelihood of Events (G{gradeNumber})", 75, 20, conceptIds[4]);

        // Lessons (3 per unit = 15 total); first lesson in each unit unlocked, rest locked
        await EnsureLessonAsync(db, $"Introduction to Counting (G{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[0], skillId_C0_S0);
        await EnsureLessonAsync(db, $"Place Value: Tens and Hundreds (G{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[0], skillId_C0_S1);
        await EnsureLessonAsync(db, $"Rounding Numbers (G{gradeNumber})", DifficultyLevel.Hard, 3, true, unitIds[0], skillId_C0_S2);

        await EnsureLessonAsync(db, $"Adding Two-Digit Numbers (G{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[1], skillId_C1_S0);
        await EnsureLessonAsync(db, $"Subtracting with Regrouping (G{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[1], skillId_C1_S1);
        await EnsureLessonAsync(db, $"Word Problems: Add and Subtract (G{gradeNumber})", DifficultyLevel.Hard, 3, true, unitIds[1], null);

        await EnsureLessonAsync(db, $"Multiplication Tables (G{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[2], skillId_C1_S2);
        await EnsureLessonAsync(db, $"Division as Equal Groups (G{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[2], null);
        await EnsureLessonAsync(db, $"Word Problems: Multiply and Divide (G{gradeNumber})", DifficultyLevel.Hard, 3, true, unitIds[2], null);

        await EnsureLessonAsync(db, $"What is a Fraction? (G{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[3], skillId_C2_S0);
        await EnsureLessonAsync(db, $"Equivalent Fractions (G{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[3], skillId_C2_S1);
        await EnsureLessonAsync(db, $"Fractions and Decimals (G{gradeNumber})", DifficultyLevel.Hard, 3, true, unitIds[3], skillId_C2_S2);

        await EnsureLessonAsync(db, $"2D Shapes and Properties (G{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[4], skillId_C3_S0);
        await EnsureLessonAsync(db, $"Perimeter and Area (G{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[4], skillId_C3_S1);
        await EnsureLessonAsync(db, $"Reading Charts and Graphs (G{gradeNumber})", DifficultyLevel.Hard, 3, true, unitIds[4], skillId_C4_S0);
    }

    // -------------------------------------------------------------------------
    // Science — 2 Units × 2 Lessons, 2 Concepts × 2 Skills (per grade)
    // -------------------------------------------------------------------------

    private static async Task SeedScienceAsync(LearningDbContext db, int gradeId, int gradeNumber)
    {
        var subjectId = await EnsureSubjectAsync(db, "Science", gradeId);

        var unitIds = new int[2];
        unitIds[0] = await EnsureUnitAsync(db, $"Living Things (G{gradeNumber})", 1, subjectId);
        unitIds[1] = await EnsureUnitAsync(db, $"Matter and Energy (G{gradeNumber})", 2, subjectId);

        var conceptIds = new int[2];
        conceptIds[0] = await EnsureConceptAsync(db, $"Plants and Animals (G{gradeNumber})", "Characteristics of living organisms", DifficultyLevel.Easy, subjectId);
        conceptIds[1] = await EnsureConceptAsync(db, $"States of Matter (G{gradeNumber})", "Solid, liquid, gas and changes", DifficultyLevel.Medium, subjectId);

        var skillId_C0_S0 = await EnsureSkillAsync(db, $"Identify Living vs Non-Living (G{gradeNumber})", 70, 15, conceptIds[0]);
        var skillId_C0_S1 = await EnsureSkillAsync(db, $"Classify Animals by Habitat (G{gradeNumber})", 75, 20, conceptIds[0]);
        var skillId_C1_S0 = await EnsureSkillAsync(db, $"Describe Properties of Solids (G{gradeNumber})", 75, 15, conceptIds[1]);
        var skillId_C1_S1 = await EnsureSkillAsync(db, $"Explain Changes of State (G{gradeNumber})", 80, 25, conceptIds[1]);

        await EnsureLessonAsync(db, $"What Are Living Things? (G{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[0], skillId_C0_S0);
        await EnsureLessonAsync(db, $"Animal Habitats (G{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[0], skillId_C0_S1);

        await EnsureLessonAsync(db, $"Solids, Liquids, and Gases (G{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[1], skillId_C1_S0);
        await EnsureLessonAsync(db, $"Melting and Freezing (G{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[1], skillId_C1_S1);
    }

    // -------------------------------------------------------------------------
    // Arabic — 2 Units × 2 Lessons, 2 Concepts × 2 Skills (per grade)
    // -------------------------------------------------------------------------

    private static async Task SeedArabicAsync(LearningDbContext db, int gradeId, int gradeNumber)
    {
        var subjectId = await EnsureSubjectAsync(db, "Arabic", gradeId);

        var unitIds = new int[2];
        unitIds[0] = await EnsureUnitAsync(db, $"Reading and Comprehension (G{gradeNumber})", 1, subjectId);
        unitIds[1] = await EnsureUnitAsync(db, $"Grammar and Writing (G{gradeNumber})", 2, subjectId);

        var conceptIds = new int[2];
        conceptIds[0] = await EnsureConceptAsync(db, $"Phonics and Decoding (G{gradeNumber})", "Letter sounds and word recognition", DifficultyLevel.Easy, subjectId);
        conceptIds[1] = await EnsureConceptAsync(db, $"Sentence Structure (G{gradeNumber})", "Noun, verb, and sentence construction", DifficultyLevel.Medium, subjectId);

        var skillId_C0_S0 = await EnsureSkillAsync(db, $"Recognize Arabic Letters (G{gradeNumber})", 70, 15, conceptIds[0]);
        var skillId_C0_S1 = await EnsureSkillAsync(db, $"Read Short Vowel Words (G{gradeNumber})", 75, 20, conceptIds[0]);
        var skillId_C1_S0 = await EnsureSkillAsync(db, $"Identify Nouns and Verbs (G{gradeNumber})", 75, 15, conceptIds[1]);
        var skillId_C1_S1 = await EnsureSkillAsync(db, $"Write Simple Arabic Sentences (G{gradeNumber})", 80, 25, conceptIds[1]);

        await EnsureLessonAsync(db, $"Arabic Alphabet Review (G{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[0], skillId_C0_S0);
        await EnsureLessonAsync(db, $"Reading Short Texts (G{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[0], skillId_C0_S1);

        await EnsureLessonAsync(db, $"Parts of a Sentence (G{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[1], skillId_C1_S0);
        await EnsureLessonAsync(db, $"Writing Sentences (G{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[1], skillId_C1_S1);
    }

    // -------------------------------------------------------------------------
    // English — 2 Units × 2 Lessons, 2 Concepts × 2 Skills (per grade)
    // -------------------------------------------------------------------------

    private static async Task SeedEnglishAsync(LearningDbContext db, int gradeId, int gradeNumber)
    {
        var subjectId = await EnsureSubjectAsync(db, "English", gradeId);

        var unitIds = new int[2];
        unitIds[0] = await EnsureUnitAsync(db, $"Vocabulary and Reading (G{gradeNumber})", 1, subjectId);
        unitIds[1] = await EnsureUnitAsync(db, $"Grammar and Composition (G{gradeNumber})", 2, subjectId);

        var conceptIds = new int[2];
        conceptIds[0] = await EnsureConceptAsync(db, $"Word Recognition (G{gradeNumber})", "Sight words and vocabulary building", DifficultyLevel.Easy, subjectId);
        conceptIds[1] = await EnsureConceptAsync(db, $"English Grammar Basics (G{gradeNumber})", "Nouns, verbs, adjectives, and tenses", DifficultyLevel.Medium, subjectId);

        var skillId_C0_S0 = await EnsureSkillAsync(db, $"Identify Sight Words (G{gradeNumber})", 70, 15, conceptIds[0]);
        var skillId_C0_S1 = await EnsureSkillAsync(db, $"Use Context Clues (G{gradeNumber})", 75, 20, conceptIds[0]);
        var skillId_C1_S0 = await EnsureSkillAsync(db, $"Identify Parts of Speech (G{gradeNumber})", 75, 15, conceptIds[1]);
        var skillId_C1_S1 = await EnsureSkillAsync(db, $"Write in Simple Past Tense (G{gradeNumber})", 80, 25, conceptIds[1]);

        await EnsureLessonAsync(db, $"Sight Words and Fluency (G{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[0], skillId_C0_S0);
        await EnsureLessonAsync(db, $"Reading Comprehension (G{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[0], skillId_C0_S1);

        await EnsureLessonAsync(db, $"Nouns and Verbs (G{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[1], skillId_C1_S0);
        await EnsureLessonAsync(db, $"Simple Sentences (G{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[1], skillId_C1_S1);
    }

    // -------------------------------------------------------------------------
    // Per-node idempotency helpers
    // Each helper checks by stable natural key before inserting; returns the id.
    // -------------------------------------------------------------------------

    private static async Task<int> EnsureSubjectAsync(LearningDbContext db, string name, int gradeId)
    {
        var existing = await db.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name == name && s.GradeId == gradeId);

        if (existing is not null)
            return existing.Id;

        var subject = new Subject { Name = name, GradeId = gradeId };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync(SystemUserId);
        return subject.Id;
    }

    private static async Task<int> EnsureUnitAsync(
        LearningDbContext db, string name, int sequenceOrder, int subjectId)
    {
        var existing = await db.Units
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Name == name && u.SubjectId == subjectId);

        if (existing is not null)
            return existing.Id;

        var unit = new Unit { Name = name, SequenceOrder = sequenceOrder, SubjectId = subjectId };
        db.Units.Add(unit);
        await db.SaveChangesAsync(SystemUserId);
        return unit.Id;
    }

    private static async Task<int> EnsureConceptAsync(
        LearningDbContext db, string name, string description, DifficultyLevel difficulty, int subjectId)
    {
        var existing = await db.Concepts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Name == name && c.SubjectId == subjectId);

        if (existing is not null)
            return existing.Id;

        var concept = new Concept
        {
            Name = name,
            Description = description,
            DifficultyLevel = difficulty,
            SubjectId = subjectId,
        };
        db.Concepts.Add(concept);
        await db.SaveChangesAsync(SystemUserId);
        return concept.Id;
    }

    private static async Task<int> EnsureSkillAsync(
        LearningDbContext db, string name, int masteryThreshold, int estimatedTimeMinutes, int conceptId)
    {
        var existing = await db.Skills
            .AsNoTracking()
            .FirstOrDefaultAsync(sk => sk.Name == name && sk.ConceptId == conceptId);

        if (existing is not null)
            return existing.Id;

        var skill = new Skill
        {
            Name = name,
            MasteryThreshold = masteryThreshold,
            EstimatedTimeMinutes = estimatedTimeMinutes,
            ConceptId = conceptId,
        };
        db.Skills.Add(skill);
        await db.SaveChangesAsync(SystemUserId);
        return skill.Id;
    }

    private static async Task EnsureLessonAsync(
        LearningDbContext db,
        string name,
        DifficultyLevel difficulty,
        int sequenceOrder,
        bool isLocked,
        int unitId,
        int? skillId)
    {
        var exists = await db.Lessons
            .AsNoTracking()
            .AnyAsync(l => l.Name == name && l.UnitId == unitId);

        if (exists)
            return;

        var lesson = new Lesson
        {
            Name = name,
            Difficulty = difficulty,
            SequenceOrder = sequenceOrder,
            IsLocked = isLocked,
            UnitId = unitId,
            SkillId = skillId,
        };
        db.Lessons.Add(lesson);
        await db.SaveChangesAsync(SystemUserId);
    }
}
