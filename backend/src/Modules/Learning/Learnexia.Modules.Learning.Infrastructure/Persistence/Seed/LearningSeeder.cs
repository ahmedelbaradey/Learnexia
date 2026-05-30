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
    // Demo lesson content + quick-check questions (P2-05)
    // Seeds Explanation + Visual + one MCQ QuizQuestion for the Grade-1 root
    // lesson of each subject.  Fully idempotent: re-running adds zero rows.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Seeds static <see cref="Lesson.Explanation"/>, <see cref="Lesson.Visual"/>, and one
    /// MCQ <see cref="QuizQuestion"/> for the four Grade-1 root lessons (one per subject).
    ///
    /// Idempotency:
    /// - Lesson content is updated only when <c>Explanation IS NULL</c> (first run only).
    /// - A <see cref="QuizQuestion"/> is inserted only when none already exists for the lesson.
    ///
    /// Lesson names are the stable P2-10 seeder keys.  If a name is not found (environment
    /// missing P2-10 seed), the lesson is skipped with a warning and no exception is thrown.
    ///
    /// <c>CorrectAnswer</c> and <c>Options</c> are stored as jsonb — values are JSON-encoded
    /// via <see cref="JsonSerializer.Serialize{T}(T)"/>.
    /// </summary>
    private static async Task SeedDemoLessonContentAsync(LearningDbContext db, ILoggerManager? logger)
    {
        // Ordered: Math, Science, Arabic, English — Grade-1 root lessons per P2-10 seed.
        var demoContent = new[]
        {
            new
            {
                LessonName  = "Introduction to Counting (G1)",
                Explanation = "Counting is how we tell **how many** of something we have. We start at **1** and say each number in order.",
                Visual      = "https://learnexia-demo.local/visuals/math-g1-counting.png",
                QuestionText = "What number comes after 5?",
                Options      = new[] { "4", "5", "6", "7" },
                CorrectAnswer = "6",
            },
            new
            {
                LessonName  = "What Are Living Things? (G1)",
                Explanation = "Living things **grow**, **breathe**, and **respond** to their environment. Plants and animals are living things; rocks and water are not.",
                Visual      = "https://learnexia-demo.local/visuals/science-g1-living-things.png",
                QuestionText = "Which of these is a living thing?",
                Options      = new[] { "Rock", "Water", "Tree", "Cloud" },
                CorrectAnswer = "Tree",
            },
            new
            {
                LessonName  = "Arabic Alphabet Review (G1)",
                Explanation = "اللغة العربية تُكتب من **اليمين إلى اليسار**. الأبجدية العربية تحتوي على **28 حرفاً**.",
                Visual      = "https://learnexia-demo.local/visuals/arabic-g1-alphabet.png",
                QuestionText = "كم عدد حروف الأبجدية العربية؟",
                Options      = new[] { "24", "26", "28", "30" },
                CorrectAnswer = "28",
            },
            new
            {
                LessonName  = "Sight Words and Fluency (G1)",
                Explanation = "Sight words are common words we **recognize by sight** without sounding them out. Examples: *the*, *and*, *is*, *are*.",
                Visual      = "https://learnexia-demo.local/visuals/english-g1-sight-words.png",
                QuestionText = "Which word is a sight word?",
                Options      = new[] { "elephant", "the", "banana", "purple" },
                CorrectAnswer = "the",
            },
        };

        var updatedLessons  = 0;
        var addedQuestions  = 0;

        foreach (var demo in demoContent)
        {
            // Look up the lesson by name — not by hard-coded id (names are P2-10 stable keys).
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
                    LessonId      = lesson.Id,
                    SkillId       = lesson.SkillId,
                    QuestionType  = QuestionType.MCQ,
                    QuestionText  = demo.QuestionText,
                    Options       = JsonSerializer.Serialize(demo.Options),
                    CorrectAnswer = JsonSerializer.Serialize(demo.CorrectAnswer),
                    Difficulty    = DifficultyLevel.Easy,
                    GeneratedBy   = GeneratedBy.Curated,
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
    // Skill dependency graph (P2-11)
    // Maps every seeded Skill to a KnowledgeNode, then authors Math prereq edges
    // (within-subject, cross-grade, G1–G6).  Both steps are fully idempotent.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Step 1: creates one <see cref="KnowledgeNode"/> per <see cref="Skill"/> (idempotent on SkillId).
    /// Step 2: authors Prerequisite edges for Math G1–G6, validates acyclicity via
    /// <see cref="SkillGraphValidator.AssertAcyclic"/> before saving.
    ///
    /// Skill name → KnowledgeNode lookup uses SkillId (not name) so future skill renames do not
    /// break the graph.  Edge candidate names are looked up by exact P2-10 seed strings; any name
    /// that resolves to a missing Skill or KnowledgeNode is skipped with a logged warning.
    ///
    /// Note on P2-10 name alignment — plan examples vs actual seeded names:
    ///   "Place Value"  — NOT seeded as a skill name; nearest is "Compare and Order Numbers (G1)".
    ///                    Edge Counting→PlaceValue SKIPPED (no "Place Value" skill in P2-10 seed).
    ///   "Division"     — NOT seeded as a skill; division lessons have null SkillId.
    ///                    Edge Multiplication→Division SKIPPED (no Division skill in P2-10 seed).
    ///   All other plan examples map to actual seeded skill names (with grade suffix).
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

        // ── Step 2: author Prerequisite edges for Math, G1–G6 ───────────────────────────────────

        // Build a lookup: SkillId → KnowledgeNodeId (only nodes backed by a skill).
        var nodeBySkillId = await db.KnowledgeNodes
            .AsNoTracking()
            .Where(n => n.SkillId != null)
            .ToDictionaryAsync(n => n.SkillId!.Value, n => n.Id);

        // Build a lookup: exact skill name → SkillId (all skills loaded above).
        var skillIdByName = allSkills.ToDictionary(s => s.Name, s => s.Id);

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

        // Prerequisite chains for Math (within-subject, cross-grade G1–G6).
        // Actual seeded skill names include a "(G{n})" suffix — exact strings from P2-10.
        //
        // Skipped edges (no matching skill in P2-10 seed):
        //   "Counting (G1)" → "Place Value (G1)"  : "Place Value" not a skill name; nearest is
        //       "Compare and Order Numbers (G1)" — counted below as Count→Compare chain.
        //   "Multiply Single-Digit Factors (G3)" → "Division (G4)"  : no Division skill seeded;
        //       division lessons have null SkillId.  Chain jumps to G5 Fractions instead.
        var candidateEdges = new (string Source, string Target)[]
        {
            // G1 intra-grade: counting is prereq for addition (via compare/order)
            ("Count to 1000 (G1)",                         "Compare and Order Numbers (G1)"),
            ("Compare and Order Numbers (G1)",             "Add Single-Digit Numbers (G1)"),
            // G1 → G2: addition prereq for subtraction
            ("Add Single-Digit Numbers (G1)",              "Subtract Within 100 (G2)"),
            // G2 → G3: subtraction prereq for multiplication
            ("Subtract Within 100 (G2)",                   "Multiply Single-Digit Factors (G3)"),
            // G3 → G5: multiplication prereq for fractions
            // (G4 "Division" skipped — no Division skill in P2-10 seed; see note above)
            ("Multiply Single-Digit Factors (G3)",         "Identify Unit Fractions (G5)"),
            // G5 intra / G5→G6: fraction progression to decimals
            ("Identify Unit Fractions (G5)",               "Compare Fractions with Same Denominator (G5)"),
            ("Compare Fractions with Same Denominator (G5)", "Convert Fractions to Decimals (G6)"),
        };

        var existingEdges = await db.KnowledgeEdges.AsNoTracking().ToListAsync();

        var newEdges = new List<KnowledgeEdge>();
        foreach (var (sourceName, targetName) in candidateEdges)
        {
            var sourceNodeId = ResolveNode(sourceName);
            var targetNodeId = ResolveNode(targetName);

            if (sourceNodeId is null || targetNodeId is null)
                continue;

            var src = sourceNodeId.Value;
            var tgt = targetNodeId.Value;

            var alreadyExists = await db.KnowledgeEdges.AnyAsync(e =>
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

        if (newEdges.Count == 0)
        {
            logger?.LogInfo("P2-11 seed: KnowledgeEdges — 0 new rows (all already present).");
            return;
        }

        // Validate that existing + proposed edges remain acyclic before saving.
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
        logger?.LogInfo($"P2-11 seed: created {newEdges.Count} new KnowledgeEdge(s).");
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
