using System.Text.Json;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Learning.Infrastructure.Persistence.Seed;

/// <summary>
/// Idempotent seed of the six MVP subject trees per grade (Math/Ar, Math/En, Science/Ar,
/// Science/En, Arabic/Ar, English/En) for all six school grades (1–6).
///
/// P8-02: Parallel bilingual tree model. Each grade has 6 Subject roots keyed on the
/// UNIQUE triplet (GradeId, SubjectCode, Language). <see cref="EnsureSubjectAsync"/> uses
/// this triplet as the idempotency key; SubjectCode and Language are always assigned
/// explicitly — never derived from Name.
///
/// Math and Science trees exist in both Ar and En. Arabic exists only as Ar.
/// English exists only as En. KnowledgeNode/KnowledgeEdge prereq graphs are authored
/// separately within each language tree; no cross-language edges.
///
/// Runs outside MediatR / UnitOfWorkBehavior, so it stamps audit fields
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

            // P8-02: 6 subject roots per grade — 2 languages for Math/Science, 1 each for Arabic/English.
            await SeedMathArAsync(db, gradeId, gradeNumber);
            await SeedMathEnAsync(db, gradeId, gradeNumber);
            await SeedScienceArAsync(db, gradeId, gradeNumber);
            await SeedScienceEnAsync(db, gradeId, gradeNumber);
            await SeedArabicArAsync(db, gradeId, gradeNumber);
            await SeedEnglishEnAsync(db, gradeId, gradeNumber);
        }

        // ILoggerManager may be absent in minimal unit-test providers; fall back to no-op.
        var logger = serviceProvider.GetService<ILoggerManager>();
        await SeedSkillGraphAsync(db, logger);
        await SeedDemoLessonContentAsync(db, logger);
        await MarkBossLessonsAsync(db, logger);
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

    // =========================================================================
    // Math — Arabic tree: 5 Units × 3 Lessons, 5 Concepts × 3 Skills (per grade)
    // Display names: Arabic
    // =========================================================================

    private static async Task SeedMathArAsync(LearningDbContext db, int gradeId, int gradeNumber)
    {
        var subjectId = await EnsureSubjectAsync(db, gradeId, SubjectCode.MATH, ContentLanguage.Ar,
            $"الرياضيات (الصف {gradeNumber})");

        // Units (5)
        var unitIds = new int[5];
        unitIds[0] = await EnsureUnitAsync(db, $"الأعداد والقيمة المكانية (ص{gradeNumber})", 1, subjectId);
        unitIds[1] = await EnsureUnitAsync(db, $"الجمع والطرح (ص{gradeNumber})", 2, subjectId);
        unitIds[2] = await EnsureUnitAsync(db, $"الضرب والقسمة (ص{gradeNumber})", 3, subjectId);
        unitIds[3] = await EnsureUnitAsync(db, $"الكسور والأعداد العشرية (ص{gradeNumber})", 4, subjectId);
        unitIds[4] = await EnsureUnitAsync(db, $"الهندسة والقياس (ص{gradeNumber})", 5, subjectId);

        // Concepts (5)
        var conceptIds = new int[5];
        conceptIds[0] = await EnsureConceptAsync(db, $"العد والمقارنة (ص{gradeNumber})", "فهم الكميات والترتيب", DifficultyLevel.Easy, subjectId);
        conceptIds[1] = await EnsureConceptAsync(db, $"العمليات الأساسية (ص{gradeNumber})", "الجمع والطرح والضرب والقسمة", DifficultyLevel.Medium, subjectId);
        conceptIds[2] = await EnsureConceptAsync(db, $"الأعداد النسبية (ص{gradeNumber})", "الكسور والأعداد العشرية والنسب المئوية", DifficultyLevel.Medium, subjectId);
        conceptIds[3] = await EnsureConceptAsync(db, $"الأشكال والفضاء (ص{gradeNumber})", "الأشكال ثنائية وثلاثية الأبعاد والزوايا", DifficultyLevel.Hard, subjectId);
        conceptIds[4] = await EnsureConceptAsync(db, $"البيانات والاحتمالات (ص{gradeNumber})", "قراءة الرسوم البيانية والجداول والاحتمال", DifficultyLevel.Hard, subjectId);

        // Skills (3 per concept = 15 total) — Arabic tree
        var skillId_C0_S0_Ar = await EnsureSkillAsync(db, $"العد حتى 1000 (ص{gradeNumber})", 70, 10, conceptIds[0]);
        var skillId_C0_S1_Ar = await EnsureSkillAsync(db, $"المقارنة وترتيب الأعداد (ص{gradeNumber})", 75, 15, conceptIds[0]);
        var skillId_C0_S2_Ar = await EnsureSkillAsync(db, $"تمييز الأعداد الزوجية والفردية (ص{gradeNumber})", 70, 10, conceptIds[0]);

        var skillId_C1_S0_Ar = await EnsureSkillAsync(db, $"جمع الأرقام الأحادية (ص{gradeNumber})", 80, 15, conceptIds[1]);
        var skillId_C1_S1_Ar = await EnsureSkillAsync(db, $"الطرح حتى 100 (ص{gradeNumber})", 80, 20, conceptIds[1]);
        var skillId_C1_S2_Ar = await EnsureSkillAsync(db, $"ضرب الأرقام الأحادية (ص{gradeNumber})", 80, 20, conceptIds[1]);

        var skillId_C2_S0_Ar = await EnsureSkillAsync(db, $"التعرف على الكسور الوحدية (ص{gradeNumber})", 75, 20, conceptIds[2]);
        var skillId_C2_S1_Ar = await EnsureSkillAsync(db, $"مقارنة الكسور بنفس المقام (ص{gradeNumber})", 80, 25, conceptIds[2]);
        var skillId_C2_S2_Ar = await EnsureSkillAsync(db, $"تحويل الكسور إلى أعداد عشرية (ص{gradeNumber})", 85, 30, conceptIds[2]);

        var skillId_C3_S0_Ar = await EnsureSkillAsync(db, $"تصنيف الأشكال ثنائية الأبعاد (ص{gradeNumber})", 75, 15, conceptIds[3]);
        var skillId_C3_S1_Ar = await EnsureSkillAsync(db, $"قياس المساحة والمحيط (ص{gradeNumber})", 80, 25, conceptIds[3]);
        var skillId_C3_S2_Ar = await EnsureSkillAsync(db, $"تحديد محاور التماثل (ص{gradeNumber})", 75, 20, conceptIds[3]);

        var skillId_C4_S0_Ar = await EnsureSkillAsync(db, $"قراءة الرسوم البيانية الشريطية (ص{gradeNumber})", 70, 15, conceptIds[4]);
        var skillId_C4_S1_Ar = await EnsureSkillAsync(db, $"تفسير الرسوم البيانية الصورية (ص{gradeNumber})", 70, 15, conceptIds[4]);
        var skillId_C4_S2_Ar = await EnsureSkillAsync(db, $"وصف احتمال الأحداث (ص{gradeNumber})", 75, 20, conceptIds[4]);

        // Lessons (3 per unit = 15 total)
        await EnsureLessonAsync(db, $"مقدمة في العد (ص{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[0], skillId_C0_S0_Ar);
        await EnsureLessonAsync(db, $"القيمة المكانية: العشرات والمئات (ص{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[0], skillId_C0_S1_Ar);
        await EnsureLessonAsync(db, $"تقريب الأعداد (ص{gradeNumber})", DifficultyLevel.Hard, 3, true, unitIds[0], skillId_C0_S2_Ar);

        await EnsureLessonAsync(db, $"جمع الأعداد ذات الرقمين (ص{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[1], skillId_C1_S0_Ar);
        await EnsureLessonAsync(db, $"الطرح مع الاستلاف (ص{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[1], skillId_C1_S1_Ar);
        await EnsureLessonAsync(db, $"مسائل الجمع والطرح (ص{gradeNumber})", DifficultyLevel.Hard, 3, true, unitIds[1], null);

        await EnsureLessonAsync(db, $"جداول الضرب (ص{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[2], skillId_C1_S2_Ar);
        await EnsureLessonAsync(db, $"القسمة كمجموعات متساوية (ص{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[2], null);
        await EnsureLessonAsync(db, $"مسائل الضرب والقسمة (ص{gradeNumber})", DifficultyLevel.Hard, 3, true, unitIds[2], null);

        await EnsureLessonAsync(db, $"ما هو الكسر؟ (ص{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[3], skillId_C2_S0_Ar);
        await EnsureLessonAsync(db, $"الكسور المتكافئة (ص{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[3], skillId_C2_S1_Ar);
        await EnsureLessonAsync(db, $"الكسور والأعداد العشرية (ص{gradeNumber})", DifficultyLevel.Hard, 3, true, unitIds[3], skillId_C2_S2_Ar);

        await EnsureLessonAsync(db, $"الأشكال ثنائية الأبعاد وخصائصها (ص{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[4], skillId_C3_S0_Ar);
        await EnsureLessonAsync(db, $"المحيط والمساحة (ص{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[4], skillId_C3_S1_Ar);
        await EnsureLessonAsync(db, $"قراءة الرسوم البيانية والمخططات (ص{gradeNumber})", DifficultyLevel.Hard, 3, true, unitIds[4], skillId_C4_S0_Ar);
    }

    // =========================================================================
    // Math — English tree: 5 Units × 3 Lessons, 5 Concepts × 3 Skills (per grade)
    // Display names: English
    // =========================================================================

    private static async Task SeedMathEnAsync(LearningDbContext db, int gradeId, int gradeNumber)
    {
        var subjectId = await EnsureSubjectAsync(db, gradeId, SubjectCode.MATH, ContentLanguage.En,
            $"Math (G{gradeNumber})");

        // Units (5)
        var unitIds = new int[5];
        unitIds[0] = await EnsureUnitAsync(db, $"Numbers and Place Value (G{gradeNumber})", 1, subjectId);
        unitIds[1] = await EnsureUnitAsync(db, $"Addition and Subtraction (G{gradeNumber})", 2, subjectId);
        unitIds[2] = await EnsureUnitAsync(db, $"Multiplication and Division (G{gradeNumber})", 3, subjectId);
        unitIds[3] = await EnsureUnitAsync(db, $"Fractions and Decimals (G{gradeNumber})", 4, subjectId);
        unitIds[4] = await EnsureUnitAsync(db, $"Geometry and Measurement (G{gradeNumber})", 5, subjectId);

        // Concepts (5)
        var conceptIds = new int[5];
        conceptIds[0] = await EnsureConceptAsync(db, $"Counting and Comparing (G{gradeNumber})", "Understanding quantity and order", DifficultyLevel.Easy, subjectId);
        conceptIds[1] = await EnsureConceptAsync(db, $"Basic Operations (G{gradeNumber})", "Addition, subtraction, multiplication, division", DifficultyLevel.Medium, subjectId);
        conceptIds[2] = await EnsureConceptAsync(db, $"Rational Numbers (G{gradeNumber})", "Fractions, decimals, and percentages", DifficultyLevel.Medium, subjectId);
        conceptIds[3] = await EnsureConceptAsync(db, $"Shapes and Space (G{gradeNumber})", "2D/3D shapes, angles, and symmetry", DifficultyLevel.Hard, subjectId);
        conceptIds[4] = await EnsureConceptAsync(db, $"Data and Probability (G{gradeNumber})", "Reading charts, tables, and chance", DifficultyLevel.Hard, subjectId);

        // Skills (3 per concept = 15 total) — English tree
        var skillId_C0_S0_En = await EnsureSkillAsync(db, $"Count to 1000 (G{gradeNumber})", 70, 10, conceptIds[0]);
        var skillId_C0_S1_En = await EnsureSkillAsync(db, $"Compare and Order Numbers (G{gradeNumber})", 75, 15, conceptIds[0]);
        var skillId_C0_S2_En = await EnsureSkillAsync(db, $"Identify Even and Odd Numbers (G{gradeNumber})", 70, 10, conceptIds[0]);

        var skillId_C1_S0_En = await EnsureSkillAsync(db, $"Add Single-Digit Numbers (G{gradeNumber})", 80, 15, conceptIds[1]);
        var skillId_C1_S1_En = await EnsureSkillAsync(db, $"Subtract Within 100 (G{gradeNumber})", 80, 20, conceptIds[1]);
        var skillId_C1_S2_En = await EnsureSkillAsync(db, $"Multiply Single-Digit Factors (G{gradeNumber})", 80, 20, conceptIds[1]);

        var skillId_C2_S0_En = await EnsureSkillAsync(db, $"Identify Unit Fractions (G{gradeNumber})", 75, 20, conceptIds[2]);
        var skillId_C2_S1_En = await EnsureSkillAsync(db, $"Compare Fractions with Same Denominator (G{gradeNumber})", 80, 25, conceptIds[2]);
        var skillId_C2_S2_En = await EnsureSkillAsync(db, $"Convert Fractions to Decimals (G{gradeNumber})", 85, 30, conceptIds[2]);

        var skillId_C3_S0_En = await EnsureSkillAsync(db, $"Classify 2D Shapes (G{gradeNumber})", 75, 15, conceptIds[3]);
        var skillId_C3_S1_En = await EnsureSkillAsync(db, $"Measure Area and Perimeter (G{gradeNumber})", 80, 25, conceptIds[3]);
        var skillId_C3_S2_En = await EnsureSkillAsync(db, $"Identify Lines of Symmetry (G{gradeNumber})", 75, 20, conceptIds[3]);

        var skillId_C4_S0_En = await EnsureSkillAsync(db, $"Read Bar Graphs (G{gradeNumber})", 70, 15, conceptIds[4]);
        var skillId_C4_S1_En = await EnsureSkillAsync(db, $"Interpret Pictographs (G{gradeNumber})", 70, 15, conceptIds[4]);
        var skillId_C4_S2_En = await EnsureSkillAsync(db, $"Describe Likelihood of Events (G{gradeNumber})", 75, 20, conceptIds[4]);

        // Lessons (3 per unit = 15 total)
        await EnsureLessonAsync(db, $"Introduction to Counting (G{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[0], skillId_C0_S0_En);
        await EnsureLessonAsync(db, $"Place Value: Tens and Hundreds (G{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[0], skillId_C0_S1_En);
        await EnsureLessonAsync(db, $"Rounding Numbers (G{gradeNumber})", DifficultyLevel.Hard, 3, true, unitIds[0], skillId_C0_S2_En);

        await EnsureLessonAsync(db, $"Adding Two-Digit Numbers (G{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[1], skillId_C1_S0_En);
        await EnsureLessonAsync(db, $"Subtracting with Regrouping (G{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[1], skillId_C1_S1_En);
        await EnsureLessonAsync(db, $"Word Problems: Add and Subtract (G{gradeNumber})", DifficultyLevel.Hard, 3, true, unitIds[1], null);

        await EnsureLessonAsync(db, $"Multiplication Tables (G{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[2], skillId_C1_S2_En);
        await EnsureLessonAsync(db, $"Division as Equal Groups (G{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[2], null);
        await EnsureLessonAsync(db, $"Word Problems: Multiply and Divide (G{gradeNumber})", DifficultyLevel.Hard, 3, true, unitIds[2], null);

        await EnsureLessonAsync(db, $"What is a Fraction? (G{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[3], skillId_C2_S0_En);
        await EnsureLessonAsync(db, $"Equivalent Fractions (G{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[3], skillId_C2_S1_En);
        await EnsureLessonAsync(db, $"Fractions and Decimals (G{gradeNumber})", DifficultyLevel.Hard, 3, true, unitIds[3], skillId_C2_S2_En);

        await EnsureLessonAsync(db, $"2D Shapes and Properties (G{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[4], skillId_C3_S0_En);
        await EnsureLessonAsync(db, $"Perimeter and Area (G{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[4], skillId_C3_S1_En);
        await EnsureLessonAsync(db, $"Reading Charts and Graphs (G{gradeNumber})", DifficultyLevel.Hard, 3, true, unitIds[4], skillId_C4_S0_En);
    }

    // =========================================================================
    // Science — Arabic tree: 2 Units × 2 Lessons, 2 Concepts × 2 Skills (per grade)
    // =========================================================================

    private static async Task SeedScienceArAsync(LearningDbContext db, int gradeId, int gradeNumber)
    {
        var subjectId = await EnsureSubjectAsync(db, gradeId, SubjectCode.SCIENCE, ContentLanguage.Ar,
            $"العلوم (الصف {gradeNumber})");

        var unitIds = new int[2];
        unitIds[0] = await EnsureUnitAsync(db, $"الكائنات الحية (ص{gradeNumber})", 1, subjectId);
        unitIds[1] = await EnsureUnitAsync(db, $"المادة والطاقة (ص{gradeNumber})", 2, subjectId);

        var conceptIds = new int[2];
        conceptIds[0] = await EnsureConceptAsync(db, $"النباتات والحيوانات (ص{gradeNumber})", "خصائص الكائنات الحية", DifficultyLevel.Easy, subjectId);
        conceptIds[1] = await EnsureConceptAsync(db, $"حالات المادة (ص{gradeNumber})", "الصلب والسائل والغاز والتحولات", DifficultyLevel.Medium, subjectId);

        var skillId_C0_S0_Ar = await EnsureSkillAsync(db, $"تمييز الأحياء وغير الأحياء (ص{gradeNumber})", 70, 15, conceptIds[0]);
        var skillId_C0_S1_Ar = await EnsureSkillAsync(db, $"تصنيف الحيوانات حسب بيئتها (ص{gradeNumber})", 75, 20, conceptIds[0]);
        var skillId_C1_S0_Ar = await EnsureSkillAsync(db, $"وصف خصائص المواد الصلبة (ص{gradeNumber})", 75, 15, conceptIds[1]);
        var skillId_C1_S1_Ar = await EnsureSkillAsync(db, $"شرح تحولات الحالة (ص{gradeNumber})", 80, 25, conceptIds[1]);

        await EnsureLessonAsync(db, $"ما هي الكائنات الحية؟ (ص{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[0], skillId_C0_S0_Ar);
        await EnsureLessonAsync(db, $"بيئات الحيوانات (ص{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[0], skillId_C0_S1_Ar);

        await EnsureLessonAsync(db, $"الصلب والسائل والغاز (ص{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[1], skillId_C1_S0_Ar);
        await EnsureLessonAsync(db, $"الذوبان والتجمد (ص{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[1], skillId_C1_S1_Ar);
    }

    // =========================================================================
    // Science — English tree: 2 Units × 2 Lessons, 2 Concepts × 2 Skills (per grade)
    // =========================================================================

    private static async Task SeedScienceEnAsync(LearningDbContext db, int gradeId, int gradeNumber)
    {
        var subjectId = await EnsureSubjectAsync(db, gradeId, SubjectCode.SCIENCE, ContentLanguage.En,
            $"Science (G{gradeNumber})");

        var unitIds = new int[2];
        unitIds[0] = await EnsureUnitAsync(db, $"Living Things (G{gradeNumber})", 1, subjectId);
        unitIds[1] = await EnsureUnitAsync(db, $"Matter and Energy (G{gradeNumber})", 2, subjectId);

        var conceptIds = new int[2];
        conceptIds[0] = await EnsureConceptAsync(db, $"Plants and Animals (G{gradeNumber})", "Characteristics of living organisms", DifficultyLevel.Easy, subjectId);
        conceptIds[1] = await EnsureConceptAsync(db, $"States of Matter (G{gradeNumber})", "Solid, liquid, gas and changes", DifficultyLevel.Medium, subjectId);

        var skillId_C0_S0_En = await EnsureSkillAsync(db, $"Identify Living vs Non-Living (G{gradeNumber})", 70, 15, conceptIds[0]);
        var skillId_C0_S1_En = await EnsureSkillAsync(db, $"Classify Animals by Habitat (G{gradeNumber})", 75, 20, conceptIds[0]);
        var skillId_C1_S0_En = await EnsureSkillAsync(db, $"Describe Properties of Solids (G{gradeNumber})", 75, 15, conceptIds[1]);
        var skillId_C1_S1_En = await EnsureSkillAsync(db, $"Explain Changes of State (G{gradeNumber})", 80, 25, conceptIds[1]);

        await EnsureLessonAsync(db, $"What Are Living Things? (G{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[0], skillId_C0_S0_En);
        await EnsureLessonAsync(db, $"Animal Habitats (G{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[0], skillId_C0_S1_En);

        await EnsureLessonAsync(db, $"Solids, Liquids, and Gases (G{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[1], skillId_C1_S0_En);
        await EnsureLessonAsync(db, $"Melting and Freezing (G{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[1], skillId_C1_S1_En);
    }

    // =========================================================================
    // Arabic — Arabic tree only (ARABIC is always Ar, pinned by resolution rule)
    // 2 Units × 2 Lessons, 2 Concepts × 2 Skills (per grade)
    // =========================================================================

    private static async Task SeedArabicArAsync(LearningDbContext db, int gradeId, int gradeNumber)
    {
        var subjectId = await EnsureSubjectAsync(db, gradeId, SubjectCode.ARABIC, ContentLanguage.Ar,
            $"اللغة العربية (الصف {gradeNumber})");

        var unitIds = new int[2];
        unitIds[0] = await EnsureUnitAsync(db, $"القراءة والفهم (ص{gradeNumber})", 1, subjectId);
        unitIds[1] = await EnsureUnitAsync(db, $"القواعد والكتابة (ص{gradeNumber})", 2, subjectId);

        var conceptIds = new int[2];
        conceptIds[0] = await EnsureConceptAsync(db, $"الصوتيات وفك الشفرات (ص{gradeNumber})", "أصوات الحروف والتعرف على الكلمات", DifficultyLevel.Easy, subjectId);
        conceptIds[1] = await EnsureConceptAsync(db, $"بنية الجملة (ص{gradeNumber})", "الاسم والفعل وتركيب الجملة", DifficultyLevel.Medium, subjectId);

        var skillId_C0_S0 = await EnsureSkillAsync(db, $"التعرف على الحروف العربية (ص{gradeNumber})", 70, 15, conceptIds[0]);
        var skillId_C0_S1 = await EnsureSkillAsync(db, $"قراءة كلمات بحركات قصيرة (ص{gradeNumber})", 75, 20, conceptIds[0]);
        var skillId_C1_S0 = await EnsureSkillAsync(db, $"تمييز الأسماء والأفعال (ص{gradeNumber})", 75, 15, conceptIds[1]);
        var skillId_C1_S1 = await EnsureSkillAsync(db, $"كتابة جمل عربية بسيطة (ص{gradeNumber})", 80, 25, conceptIds[1]);

        await EnsureLessonAsync(db, $"مراجعة الحروف الهجائية (ص{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[0], skillId_C0_S0);
        await EnsureLessonAsync(db, $"قراءة نصوص قصيرة (ص{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[0], skillId_C0_S1);

        await EnsureLessonAsync(db, $"أجزاء الجملة (ص{gradeNumber})", DifficultyLevel.Easy, 1, false, unitIds[1], skillId_C1_S0);
        await EnsureLessonAsync(db, $"كتابة الجمل (ص{gradeNumber})", DifficultyLevel.Medium, 2, true, unitIds[1], skillId_C1_S1);
    }

    // =========================================================================
    // English — English tree only (ENGLISH is always En, pinned by resolution rule)
    // 2 Units × 2 Lessons, 2 Concepts × 2 Skills (per grade)
    // =========================================================================

    private static async Task SeedEnglishEnAsync(LearningDbContext db, int gradeId, int gradeNumber)
    {
        var subjectId = await EnsureSubjectAsync(db, gradeId, SubjectCode.ENGLISH, ContentLanguage.En,
            $"English (G{gradeNumber})");

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
    // Demo lesson content + quick-check questions (P2-05)
    // Seeds Explanation + Visual + one MCQ QuizQuestion for the Grade-1 root
    // lesson of each subject.  Fully idempotent: re-running adds zero rows.
    //
    // P8-02: Seeds demo content for both Math/Ar and Math/En Grade-1 root lessons,
    // plus Science/Ar, Science/En, Arabic/Ar, and English/En.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Seeds static <see cref="Lesson.Explanation"/>, <see cref="Lesson.Visual"/>, and one
    /// MCQ <see cref="QuizQuestion"/> for the Grade-1 root lessons (one per language tree).
    ///
    /// Idempotency:
    /// - Lesson content is updated only when <c>Explanation IS NULL</c> (first run only).
    /// - A <see cref="QuizQuestion"/> is inserted only when none already exists for the lesson.
    ///
    /// Lesson names are the stable P2-10/P8-02 seeder keys. If a name is not found the lesson
    /// is skipped with a warning and no exception is thrown.
    ///
    /// <c>CorrectAnswer</c> and <c>Options</c> are stored as jsonb — values are JSON-encoded
    /// via <see cref="JsonSerializer.Serialize{T}(T)"/>.
    /// </summary>
    private static async Task SeedDemoLessonContentAsync(LearningDbContext db, ILoggerManager? logger)
    {
        // Grade-1 root lessons — one per language tree (both Math/Ar and Math/En, etc.).
        var demoContent = new[]
        {
            // Math / Arabic tree
            new
            {
                LessonName   = "مقدمة في العد (ص1)",
                Explanation  = "العد هو الطريقة التي نعرف بها **كم** عدد الأشياء. نبدأ من **1** ونقول كل رقم بالترتيب.",
                Visual       = "https://learnexia-demo.local/visuals/math-ar-g1-counting.png",
                QuestionText = "ما الرقم الذي يأتي بعد 5؟",
                Options      = new[] { "4", "5", "6", "7" },
                CorrectAnswer = "6",
            },
            // Math / English tree
            new
            {
                LessonName   = "Introduction to Counting (G1)",
                Explanation  = "Counting is how we tell **how many** of something we have. We start at **1** and say each number in order.",
                Visual       = "https://learnexia-demo.local/visuals/math-en-g1-counting.png",
                QuestionText = "What number comes after 5?",
                Options      = new[] { "4", "5", "6", "7" },
                CorrectAnswer = "6",
            },
            // Science / Arabic tree
            new
            {
                LessonName   = "ما هي الكائنات الحية؟ (ص1)",
                Explanation  = "الكائنات الحية **تنمو** و**تتنفس** و**تستجيب** لبيئتها. النباتات والحيوانات كائنات حية؛ الصخور والماء ليست كذلك.",
                Visual       = "https://learnexia-demo.local/visuals/science-ar-g1-living-things.png",
                QuestionText = "أي من هذه كائن حي؟",
                Options      = new[] { "صخرة", "ماء", "شجرة", "سحاب" },
                CorrectAnswer = "شجرة",
            },
            // Science / English tree
            new
            {
                LessonName   = "What Are Living Things? (G1)",
                Explanation  = "Living things **grow**, **breathe**, and **respond** to their environment. Plants and animals are living things; rocks and water are not.",
                Visual       = "https://learnexia-demo.local/visuals/science-en-g1-living-things.png",
                QuestionText = "Which of these is a living thing?",
                Options      = new[] { "Rock", "Water", "Tree", "Cloud" },
                CorrectAnswer = "Tree",
            },
            // Arabic / Arabic tree
            new
            {
                LessonName   = "مراجعة الحروف الهجائية (ص1)",
                Explanation  = "اللغة العربية تُكتب من **اليمين إلى اليسار**. الأبجدية العربية تحتوي على **28 حرفاً**.",
                Visual       = "https://learnexia-demo.local/visuals/arabic-g1-alphabet.png",
                QuestionText = "كم عدد حروف الأبجدية العربية؟",
                Options      = new[] { "24", "26", "28", "30" },
                CorrectAnswer = "28",
            },
            // English / English tree
            new
            {
                LessonName   = "Sight Words and Fluency (G1)",
                Explanation  = "Sight words are common words we **recognize by sight** without sounding them out. Examples: *the*, *and*, *is*, *are*.",
                Visual       = "https://learnexia-demo.local/visuals/english-g1-sight-words.png",
                QuestionText = "Which word is a sight word?",
                Options      = new[] { "elephant", "the", "banana", "purple" },
                CorrectAnswer = "the",
            },
        };

        var updatedLessons  = 0;
        var addedQuestions  = 0;

        foreach (var demo in demoContent)
        {
            // Look up the lesson by name — not by hard-coded id (names are P8-02 stable keys).
            var lesson = await db.Lessons.FirstOrDefaultAsync(l => l.Name == demo.LessonName);
            if (lesson is null)
            {
                logger?.LogWarn($"P2-05 seed: lesson '{demo.LessonName}' not found; skipping.");
                continue;
            }

            // ── Step 1: seed Explanation + Visual (only when not yet set) ─────────────────
            if (lesson.Explanation is null && lesson.Visual is null)
            {
                lesson.Explanation = demo.Explanation;
                lesson.Visual      = demo.Visual;
                db.Lessons.Update(lesson);
                updatedLessons++;
            }

            // ── Step 2: seed one MCQ QuizQuestion (idempotent) ───────────────────────────
            var hasQuestion = await db.QuizQuestions.AnyAsync(q => q.LessonId == lesson.Id);
            if (!hasQuestion)
            {
                // Options is stored as jsonb — serialize the string array to a JSON array.
                // CorrectAnswer is stored as jsonb — serialize the string value to a JSON string
                // (e.g. "6" → "\"6\"") so it is valid JSON in the jsonb column.
                var question = new QuizQuestion
                {
                    LessonId       = lesson.Id,
                    SkillId        = lesson.SkillId,
                    QuestionType   = QuestionType.MCQ,
                    QuestionText   = demo.QuestionText,
                    Options        = JsonSerializer.Serialize(demo.Options),
                    CorrectAnswer  = JsonSerializer.Serialize(demo.CorrectAnswer),
                    Difficulty     = DifficultyLevel.Easy,
                    GeneratedBy    = GeneratedBy.Curated,
                    LifecycleState = LifecycleState.Published,
                    IsActive       = true,
                };
                await db.QuizQuestions.AddAsync(question);
                addedQuestions++;
            }
        }

        if (updatedLessons > 0 || addedQuestions > 0)
        {
            await db.SaveChangesAsync(SystemUserId);
        }

        logger?.LogInfo($"P2-05 seed: SeedDemoLessonContent inserted {addedQuestions} quiz question(s) and updated {updatedLessons} lesson(s) with content.");
    }

    // -------------------------------------------------------------------------
    // Skill dependency graph (P2-11, P8-02)
    // Maps every seeded Skill to a KnowledgeNode, then authors Math prereq edges
    // within each language tree separately (MATH/Ar and MATH/En, G1–G6).
    // No cross-language edges — KnowledgeNode.SubjectId always points to the
    // correct language root.  Both steps are fully idempotent.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Step 1: creates one <see cref="KnowledgeNode"/> per <see cref="Skill"/> (idempotent on SkillId).
    /// Step 2: authors Prerequisite edges for Math G1–G6, separately within the MATH/Ar tree and
    /// the MATH/En tree; validates acyclicity via <see cref="SkillGraphValidator.AssertAcyclic"/>
    /// per-language tree before saving.
    ///
    /// Skill name → KnowledgeNode lookup uses SkillId (not name). Edge candidate names are looked
    /// up by exact P8-02 seed strings; any name that resolves to a missing Skill or KnowledgeNode
    /// is skipped with a logged warning.
    ///
    /// P8-02 note on per-language graphs: Arabic-tree skill names carry the Arabic suffix (ص{n}),
    /// English-tree skill names carry the English suffix (G{n}). The two candidate-edge sets are
    /// entirely separate — no edge spans two language trees.
    /// </summary>
    private static async Task SeedSkillGraphAsync(LearningDbContext db, ILoggerManager? logger)
    {
        // ── Step 1: map every Skill to a KnowledgeNode ───────────────────────────────────────────

        // Load all skills with the Concept → Subject → Grade navigation chain so we can resolve
        // SubjectId and GradeId without a second round-trip.
        var allSkills = await db.Skills
            .AsNoTracking()
            .Include(s => s.Concept)
                .ThenInclude(c => c.Subject)
                    .ThenInclude(sub => sub.Grade)
            .ToListAsync();

        var newNodes = new List<KnowledgeNode>();
        foreach (var skill in allSkills)
        {
            var alreadyExists = await db.KnowledgeNodes
                .AnyAsync(n => n.SkillId == skill.Id);

            if (alreadyExists)
                continue;

            newNodes.Add(new KnowledgeNode
            {
                Name       = skill.Name,
                NodeType   = KnowledgeNodeType.Skill,
                SubjectId  = skill.Concept.Subject.Id,
                GradeId    = skill.Concept.Subject.Grade.Id,
                Difficulty = 3,
                SkillId    = skill.Id,
            });
        }

        if (newNodes.Count > 0)
        {
            db.KnowledgeNodes.AddRange(newNodes);
            await db.SaveChangesAsync(SystemUserId);
            logger?.LogInfo($"P2-11 seed: created {newNodes.Count} new KnowledgeNode(s).");
        }
        else
        {
            logger?.LogInfo("P2-11 seed: KnowledgeNodes — 0 new rows (all already present).");
        }

        // ── Step 2: author Prerequisite edges per Math language tree (G1–G6) ─────────────────────

        // Reload skills after node creation to ensure the skill-name dictionary is fresh.
        var allSkillsForEdges = await db.Skills
            .AsNoTracking()
            .Include(s => s.Concept)
                .ThenInclude(c => c.Subject)
            .ToListAsync();

        // Build a lookup: SkillId → KnowledgeNodeId (only nodes backed by a skill).
        var nodeBySkillId = await db.KnowledgeNodes
            .AsNoTracking()
            .Where(n => n.SkillId != null)
            .ToDictionaryAsync(n => n.SkillId!.Value, n => n.Id);

        // Build a lookup: exact skill name → SkillId (all skills).
        var skillIdByName = allSkillsForEdges.ToDictionary(s => s.Name, s => s.Id);

        // Helper: resolve a skill name → KnowledgeNodeId; returns null and logs if not found.
        int? ResolveNode(string skillName)
        {
            if (!skillIdByName.TryGetValue(skillName, out var skillId))
            {
                logger?.LogWarn($"P2-11 seed: skipping edge involving \"{skillName}\": skill name not found in seed.");
                return null;
            }
            if (!nodeBySkillId.TryGetValue(skillId, out var nodeId))
            {
                logger?.LogWarn($"P2-11 seed: skipping edge involving \"{skillName}\" (SkillId={skillId}): KnowledgeNode not found.");
                return null;
            }
            return nodeId;
        }

        var existingEdges = await db.KnowledgeEdges.AsNoTracking().ToListAsync();

        // ── Math / Arabic tree prereq edges (within-subject, cross-grade G1–G6) ─────────────────
        // Arabic-tree skill names use the Arabic grade suffix (ص{n}).
        var candidateEdgesAr = new (string Source, string Target)[]
        {
            // G1 intra-grade
            ("العد حتى 1000 (ص1)",                              "المقارنة وترتيب الأعداد (ص1)"),
            ("المقارنة وترتيب الأعداد (ص1)",                    "جمع الأرقام الأحادية (ص1)"),
            // G1 → G2
            ("جمع الأرقام الأحادية (ص1)",                       "الطرح حتى 100 (ص2)"),
            // G2 → G3
            ("الطرح حتى 100 (ص2)",                              "ضرب الأرقام الأحادية (ص3)"),
            // G3 → G5 (G4 Division not seeded as a skill)
            ("ضرب الأرقام الأحادية (ص3)",                       "التعرف على الكسور الوحدية (ص5)"),
            // G5 intra / G5→G6
            ("التعرف على الكسور الوحدية (ص5)",                  "مقارنة الكسور بنفس المقام (ص5)"),
            ("مقارنة الكسور بنفس المقام (ص5)",                  "تحويل الكسور إلى أعداد عشرية (ص6)"),
        };

        // ── Math / English tree prereq edges (within-subject, cross-grade G1–G6) ───────────────
        // English-tree skill names use the English grade suffix (G{n}).
        // Skipped edges (no matching skill in the P8-02 seed):
        //   "Counting (G1)" → "Place Value (G1)"  : not a seeded skill; nearest is
        //       "Compare and Order Numbers (G1)" — counted below.
        //   "Multiply Single-Digit Factors (G3)" → "Division (G4)" : no Division skill seeded;
        //       division lessons have null SkillId.  Chain jumps to G5 Fractions instead.
        var candidateEdgesEn = new (string Source, string Target)[]
        {
            // G1 intra-grade
            ("Count to 1000 (G1)",                         "Compare and Order Numbers (G1)"),
            ("Compare and Order Numbers (G1)",             "Add Single-Digit Numbers (G1)"),
            // G1 → G2
            ("Add Single-Digit Numbers (G1)",              "Subtract Within 100 (G2)"),
            // G2 → G3
            ("Subtract Within 100 (G2)",                   "Multiply Single-Digit Factors (G3)"),
            // G3 → G5
            ("Multiply Single-Digit Factors (G3)",         "Identify Unit Fractions (G5)"),
            // G5 intra / G5→G6
            ("Identify Unit Fractions (G5)",               "Compare Fractions with Same Denominator (G5)"),
            ("Compare Fractions with Same Denominator (G5)", "Convert Fractions to Decimals (G6)"),
        };

        var newEdges = new List<KnowledgeEdge>();

        // Author edges for both language trees using the same helper.
        await AuthorEdgesAsync(candidateEdgesAr, "MATH/Ar");
        await AuthorEdgesAsync(candidateEdgesEn, "MATH/En");

        if (newEdges.Count == 0)
        {
            logger?.LogInfo("P2-11 seed: KnowledgeEdges — 0 new rows (all already present).");
            return;
        }

        // Validate that existing + proposed edges remain acyclic before saving.
        // Run the validator once over the union of both trees' candidate edges.
        try
        {
            SkillGraphValidator.AssertAcyclic(existingEdges.Concat(newEdges));
        }
        catch (InvalidOperationException ex)
        {
            logger?.LogError(ex, $"P2-11 seed: cycle detected, skipping new graph edges. {ex.Message}");
            return;
        }

        db.KnowledgeEdges.AddRange(newEdges);
        await db.SaveChangesAsync(SystemUserId);
        logger?.LogInfo($"P2-11 seed: created {newEdges.Count} new KnowledgeEdge(s) across MATH/Ar and MATH/En trees.");

        // Local helper — builds KnowledgeEdge rows for one candidate set, checking idempotency.
        async Task AuthorEdgesAsync(
            (string Source, string Target)[] candidates,
            string treeLabel)
        {
            foreach (var (sourceName, targetName) in candidates)
            {
                var sourceNodeId = ResolveNode(sourceName);
                var targetNodeId = ResolveNode(targetName);

                if (sourceNodeId is null || targetNodeId is null)
                    continue;

                var src = sourceNodeId.Value;
                var tgt = targetNodeId.Value;

                var alreadyExists = existingEdges.Any(e =>
                    e.SourceNodeId == src &&
                    e.TargetNodeId == tgt &&
                    e.RelationshipType == EdgeRelationshipType.Prerequisite)
                    || newEdges.Any(e =>
                    e.SourceNodeId == src &&
                    e.TargetNodeId == tgt &&
                    e.RelationshipType == EdgeRelationshipType.Prerequisite);

                if (alreadyExists)
                    continue;

                newEdges.Add(new KnowledgeEdge
                {
                    SourceNodeId     = src,
                    TargetNodeId     = tgt,
                    RelationshipType = EdgeRelationshipType.Prerequisite,
                    Strength         = 1.0m,
                });
            }
        }
    }

    // -------------------------------------------------------------------------
    // Boss-lesson marking (P2-03)
    // Marks the highest-SequenceOrder lesson in each Unit as IsBoss = true.
    // All other lessons in the unit are set to IsBoss = false (prevents drift).
    // Idempotent: commits only when the change tracker detects a difference.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Marks the highest-<see cref="Lesson.SequenceOrder"/> lesson of each <see cref="Unit"/>
    /// as <see cref="Lesson.IsBoss"/> = <c>true</c>; sets all other lessons in the unit to
    /// <c>false</c>. Idempotent: on subsequent runs the change tracker has no pending changes
    /// and <see cref="LearningDbContext.SaveChangesAsync(int)"/> is not called.
    /// Called at the end of <see cref="SeedAsync"/> after <c>SeedDemoLessonContentAsync</c>.
    /// </summary>
    private static async Task MarkBossLessonsAsync(LearningDbContext db, ILoggerManager? logger)
    {
        try
        {
            // Group lessons by UnitId, find the Id of the max-SequenceOrder lesson per group.
            // Tie-break: lowest Id wins (defensive; seed invariant prevents ties).
            var bossLessonIds = await db.Lessons
                .GroupBy(l => l.UnitId)
                .Select(g => g.OrderByDescending(l => l.SequenceOrder).ThenBy(l => l.Id).First().Id)
                .ToListAsync();

            // Load only the lessons that should be boss-marked (tracked — EF can update them).
            var bossLessons = await db.Lessons
                .Where(l => bossLessonIds.Contains(l.Id))
                .ToListAsync();

            // Load non-boss lessons (tracked — EF can update them if drift occurred).
            var nonBossLessons = await db.Lessons
                .Where(l => !bossLessonIds.Contains(l.Id))
                .ToListAsync();

            var updated = 0;

            foreach (var lesson in bossLessons)
            {
                if (!lesson.IsBoss)
                {
                    lesson.IsBoss = true;
                    updated++;
                }
            }

            foreach (var lesson in nonBossLessons)
            {
                if (lesson.IsBoss)
                {
                    lesson.IsBoss = false;
                    updated++;
                }
            }

            if (db.ChangeTracker.HasChanges())
            {
                await db.SaveChangesAsync(SystemUserId);
                logger?.LogInfo($"P2-03 seed: marked {bossLessons.Count(l => l.IsBoss)} lesson(s) as boss; {updated} row(s) updated.");
            }
            else
            {
                logger?.LogInfo("P2-03 seed: MarkBossLessonsAsync — all bosses already marked; nothing to do.");
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "P2-03: MarkBossLessonsAsync failed");
            // Do NOT throw — keep startup tolerant per existing seeder pattern.
        }
    }

    // -------------------------------------------------------------------------
    // Per-node idempotency helpers
    // Each helper checks by stable natural key before inserting; returns the id.
    // -------------------------------------------------------------------------

    /// <summary>
    /// P8-02: idempotency key is the UNIQUE triplet (GradeId, SubjectCode, Language).
    /// SubjectCode and Language are always explicitly passed — never derived from Name.
    /// Name is a display label only and may differ between language trees.
    /// </summary>
    private static async Task<int> EnsureSubjectAsync(
        LearningDbContext db,
        int gradeId,
        SubjectCode subjectCode,
        ContentLanguage language,
        string name)
    {
        var existing = await db.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.GradeId == gradeId &&
                s.SubjectCode == subjectCode &&
                s.Language == language);

        if (existing is not null)
            return existing.Id;

        var subject = new Subject
        {
            Name           = name,
            GradeId        = gradeId,
            SubjectCode    = subjectCode,
            Language       = language,
            LifecycleState = LifecycleState.Published,
            IsActive       = true,
        };
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

        var unit = new Unit
        {
            Name           = name,
            SequenceOrder  = sequenceOrder,
            SubjectId      = subjectId,
            LifecycleState = LifecycleState.Published,
            IsActive       = true,
        };
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
            Name                 = name,
            MasteryThreshold     = masteryThreshold,
            EstimatedTimeMinutes = estimatedTimeMinutes,
            ConceptId            = conceptId,
            IsActive             = true,
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
            Name           = name,
            Difficulty     = difficulty,
            SequenceOrder  = sequenceOrder,
            IsLocked       = isLocked,
            UnitId         = unitId,
            SkillId        = skillId,
            LifecycleState = LifecycleState.Published,
            IsActive       = true,
        };
        db.Lessons.Add(lesson);
        await db.SaveChangesAsync(SystemUserId);
    }
}
