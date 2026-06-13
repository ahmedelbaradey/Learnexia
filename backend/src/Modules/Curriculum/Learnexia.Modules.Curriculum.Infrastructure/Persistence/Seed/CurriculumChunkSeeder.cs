using Learnexia.Modules.Curriculum.Domain.Entities;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Learnexia.Modules.Curriculum.Infrastructure.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Curriculum.Infrastructure.Persistence.Seed;

/// <summary>
/// Idempotent seed of the MVP curriculum chunk corpus + deterministic placeholder embeddings.
///
/// <para>Seeds one <c>Active</c> <see cref="CurriculumVersion"/> per (SubjectId/GradeId, Language),
/// pre-chunked <see cref="CurriculumChunk"/> rows for the 4 MVP subjects (Math, Science, Arabic,
/// English — NO Social Studies), and deterministic placeholder vectors in
/// <c>chunk_embeddings_bge_m3</c> (IsActive=true).</para>
///
/// <para><strong>PLACEHOLDER EMBEDDINGS — RE-EMBED REQUIRED:</strong>
/// Vectors are computed by <see cref="DeterministicEmbedding.ComputeAsync"/> — a hash-seeded,
/// L2-normalized function that produces reproducible 1024-dim vectors WITHOUT calling the BGE-M3
/// TEI endpoint. Provider/Model/ModelVersion are stamped as placeholder values
/// (<see cref="DeterministicEmbedding.PlaceholderProvider"/>, <see cref="DeterministicEmbedding.PlaceholderModel"/>,
/// <see cref="DeterministicEmbedding.PlaceholderModelVersion"/>).
/// Once BE-0 (self-hosted BGE-M3 TEI on Hetzner) is provisioned, ALL rows with
/// ModelVersion='seed-placeholder-v0' MUST be re-embedded with real BGE-M3 so that
/// semantic retrieval is meaningful. The parity stamp makes the swap detectable.</para>
///
/// <para><strong>Idempotency:</strong>
/// skips a (SubjectId, Language) pair if chunks already exist for it.</para>
///
/// <para>Mirrors <c>LearningSeeder</c> style: static class, <c>SeedAsync</c> entry point,
/// <c>IServiceProvider</c> parameter, no MediatR/UoW, <c>SaveChangesAsync(SystemUserId)</c>.
/// Called from <c>CurriculumModule.InitializeAsync</c>.</para>
///
/// <para><strong>SkillKey format:</strong>
/// <c>{subjectCode}.grade{n}.{unitSlug}.{skillSlug}</c> — matches the stable P2-10/P2-11 skill
/// seed keys. The SubjectId used here is a loose integer reference (no cross-module FK);
/// the value is chosen to match the Learning module's seeded Subject rows for Grade 3.</para>
/// </summary>
public static class CurriculumChunkSeeder
{
    /// <summary>System user id for audit stamp on seeded rows (convention: 0 = seed process).</summary>
    public const int SystemUserId = 0;

    /// <summary>
    /// Minimum similarity distance floor for the placeholder corpus.
    /// Same-text query vs seeded chunk: distance ≈ 0 (identical vector).
    /// Unrelated text: distance will be larger (determined by hash divergence).
    /// </summary>
    private const double PlaceholderSimilarityFloor = 0.4;

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var db     = serviceProvider.GetRequiredService<CurriculumDbContext>();
        var logger = serviceProvider.GetService<ILoggerManager>();

        await SeedMathArAsync(db, logger);
        await SeedMathEnAsync(db, logger);
        await SeedScienceArAsync(db, logger);
        await SeedScienceEnAsync(db, logger);
        await SeedArabicArAsync(db, logger);
        await SeedEnglishEnAsync(db, logger);
    }

    // =========================================================================
    // Math — Arabic (Grade 3 MVP)
    // SubjectId loose ref: we use a constant that the test can override.
    // In production the Seeder does not need the real int FK — just the same value
    // the retrieval query is passed at runtime.
    // =========================================================================

    private static async Task SeedMathArAsync(CurriculumDbContext db, ILoggerManager? logger)
    {
        // Idempotency key: (SubjectCode=1, Language=Ar, GradeId-marker=3) → use Version.Name
        const string versionName = "MVP-G3-Math-Ar-v1";
        if (await db.CurriculumVersions.AnyAsync(v => v.Name == versionName))
        {
            logger?.LogInfo($"P3-07 CurriculumChunkSeeder: '{versionName}' already seeded; skipping.");
            return;
        }

        var version = await EnsureVersionAsync(db, 1, ContentLanguage.Arabic, versionName);

        // Representative MVP chunks for Grade 3 Math (Arabic medium).
        // SkillKey format: math.grade3.{unit}.{skill}
        var chunks = new[]
        {
            // Unit: الأعداد والقيمة المكانية
            new ChunkData("math.grade3.place-value.count-to-1000",
                "العد حتى 1000: يمكننا عد الأعداد حتى 1000 باستخدام القيمة المكانية. كل رقم له قيمة مكانية: الآحاد والعشرات والمئات.",
                1, SubjectId:1, GradeId:3),
            new ChunkData("math.grade3.place-value.compare-order",
                "المقارنة وترتيب الأعداد: نستخدم الرموز < و > و = لمقارنة الأعداد. نرتب الأعداد تصاعدياً أو تنازلياً.",
                2, SubjectId:1, GradeId:3),
            // Unit: الجمع والطرح
            new ChunkData("math.grade3.operations.add-single",
                "جمع الأرقام الأحادية: يمكن جمع أرقام مفردة بسهولة. مثال: 5 + 3 = 8. نستخدم خط الأعداد أو الأصابع للمساعدة.",
                1, SubjectId:1, GradeId:3),
            new ChunkData("math.grade3.operations.subtract-100",
                "الطرح حتى 100: الطرح هو إيجاد الفرق بين عددين. مثال: 75 - 28 = 47. قد نحتاج إلى الاستلاف.",
                2, SubjectId:1, GradeId:3),
            // Unit: الكسور
            new ChunkData("math.grade3.fractions.unit-fractions",
                "الكسور الوحدية: الكسر الوحدي هو كسر بسطه 1 مثل 1/2 و 1/3 و 1/4. يمثل جزءاً واحداً من أجزاء متساوية.",
                1, SubjectId:1, GradeId:3),
            new ChunkData("math.grade3.fractions.compare-same-denom",
                "مقارنة الكسور بنفس المقام: عند مقارنة كسرين بنفس المقام، الكسر الأكبر هو الذي يملك بسطاً أكبر. مثال: 3/4 > 1/4.",
                2, SubjectId:1, GradeId:3),
        };

        await InsertChunksAsync(db, version, chunks, logger);
        logger?.LogInfo($"P3-07 CurriculumChunkSeeder: seeded {chunks.Length} chunks for '{versionName}'.");
    }

    // =========================================================================
    // Math — English (Grade 3 MVP)
    // =========================================================================

    private static async Task SeedMathEnAsync(CurriculumDbContext db, ILoggerManager? logger)
    {
        const string versionName = "MVP-G3-Math-En-v1";
        if (await db.CurriculumVersions.AnyAsync(v => v.Name == versionName))
        {
            logger?.LogInfo($"P3-07 CurriculumChunkSeeder: '{versionName}' already seeded; skipping.");
            return;
        }

        var version = await EnsureVersionAsync(db, 2, ContentLanguage.English, versionName);

        var chunks = new[]
        {
            new ChunkData("math.grade3.place-value.count-to-1000",
                "Counting to 1000: We count numbers up to 1000 using place value. Each digit has a place: ones, tens, and hundreds.",
                1, SubjectId:2, GradeId:3),
            new ChunkData("math.grade3.place-value.compare-order",
                "Comparing and ordering numbers: We use symbols < > = to compare numbers. We arrange numbers in ascending or descending order.",
                2, SubjectId:2, GradeId:3),
            new ChunkData("math.grade3.operations.add-single",
                "Adding single-digit numbers: Single-digit numbers can be added easily. Example: 5 + 3 = 8. Use a number line or fingers to help.",
                1, SubjectId:2, GradeId:3),
            new ChunkData("math.grade3.operations.subtract-100",
                "Subtracting within 100: Subtraction finds the difference between two numbers. Example: 75 - 28 = 47. Regrouping may be needed.",
                2, SubjectId:2, GradeId:3),
            new ChunkData("math.grade3.fractions.unit-fractions",
                "Unit fractions: A unit fraction has 1 as its numerator, such as 1/2, 1/3, 1/4. It represents one equal part of a whole.",
                1, SubjectId:2, GradeId:3),
            new ChunkData("math.grade3.fractions.compare-same-denom",
                "Comparing fractions with the same denominator: When denominators are equal, the fraction with the larger numerator is greater. Example: 3/4 > 1/4.",
                2, SubjectId:2, GradeId:3),
        };

        await InsertChunksAsync(db, version, chunks, logger);
        logger?.LogInfo($"P3-07 CurriculumChunkSeeder: seeded {chunks.Length} chunks for '{versionName}'.");
    }

    // =========================================================================
    // Science — Arabic (Grade 3 MVP)
    // =========================================================================

    private static async Task SeedScienceArAsync(CurriculumDbContext db, ILoggerManager? logger)
    {
        const string versionName = "MVP-G3-Science-Ar-v1";
        if (await db.CurriculumVersions.AnyAsync(v => v.Name == versionName))
        {
            logger?.LogInfo($"P3-07 CurriculumChunkSeeder: '{versionName}' already seeded; skipping.");
            return;
        }

        var version = await EnsureVersionAsync(db, 3, ContentLanguage.Arabic, versionName);

        var chunks = new[]
        {
            new ChunkData("science.grade3.living-things.living-vs-nonliving",
                "الكائنات الحية وغير الحية: الكائنات الحية تنمو وتتنفس وتتكاثر وتستجيب لبيئتها. النباتات والحيوانات كائنات حية. الصخور والماء والهواء ليست كائنات حية.",
                1, SubjectId:3, GradeId:3),
            new ChunkData("science.grade3.living-things.classify-animals",
                "تصنيف الحيوانات حسب بيئتها: تعيش الحيوانات في بيئات مختلفة كالغابة والصحراء والبحر. كل حيوان لديه مميزات تجعله مناسباً لبيئته.",
                2, SubjectId:3, GradeId:3),
            new ChunkData("science.grade3.matter.properties-solids",
                "خصائص المواد الصلبة: المادة الصلبة لها شكل محدد وحجم محدد. لا تتغير تلقائياً. أمثلة: الحجر، الخشب، البلاستيك.",
                1, SubjectId:3, GradeId:3),
            new ChunkData("science.grade3.matter.changes-of-state",
                "تحولات الحالة: يمكن للمادة أن تتحول بين الصلبة والسائلة والغازية بالتسخين أو التبريد. الماء يتجمد عند 0 درجة مئوية ويتبخر عند 100 درجة.",
                2, SubjectId:3, GradeId:3),
        };

        await InsertChunksAsync(db, version, chunks, logger);
        logger?.LogInfo($"P3-07 CurriculumChunkSeeder: seeded {chunks.Length} chunks for '{versionName}'.");
    }

    // =========================================================================
    // Science — English (Grade 3 MVP)
    // =========================================================================

    private static async Task SeedScienceEnAsync(CurriculumDbContext db, ILoggerManager? logger)
    {
        const string versionName = "MVP-G3-Science-En-v1";
        if (await db.CurriculumVersions.AnyAsync(v => v.Name == versionName))
        {
            logger?.LogInfo($"P3-07 CurriculumChunkSeeder: '{versionName}' already seeded; skipping.");
            return;
        }

        var version = await EnsureVersionAsync(db, 4, ContentLanguage.English, versionName);

        var chunks = new[]
        {
            new ChunkData("science.grade3.living-things.living-vs-nonliving",
                "Living vs Non-Living Things: Living things grow, breathe, reproduce and respond to their environment. Plants and animals are living. Rocks, water, and air are non-living.",
                1, SubjectId:4, GradeId:3),
            new ChunkData("science.grade3.living-things.classify-animals",
                "Classifying Animals by Habitat: Animals live in different habitats like forest, desert, and ocean. Each animal has features suited to its environment.",
                2, SubjectId:4, GradeId:3),
            new ChunkData("science.grade3.matter.properties-solids",
                "Properties of Solids: A solid has a definite shape and volume. It does not change shape on its own. Examples: rock, wood, plastic.",
                1, SubjectId:4, GradeId:3),
            new ChunkData("science.grade3.matter.changes-of-state",
                "Changes of State: Matter can change between solid, liquid, and gas through heating or cooling. Water freezes at 0°C and boils at 100°C.",
                2, SubjectId:4, GradeId:3),
        };

        await InsertChunksAsync(db, version, chunks, logger);
        logger?.LogInfo($"P3-07 CurriculumChunkSeeder: seeded {chunks.Length} chunks for '{versionName}'.");
    }

    // =========================================================================
    // Arabic — Arabic only (Grade 3 MVP)
    // =========================================================================

    private static async Task SeedArabicArAsync(CurriculumDbContext db, ILoggerManager? logger)
    {
        const string versionName = "MVP-G3-Arabic-Ar-v1";
        if (await db.CurriculumVersions.AnyAsync(v => v.Name == versionName))
        {
            logger?.LogInfo($"P3-07 CurriculumChunkSeeder: '{versionName}' already seeded; skipping.");
            return;
        }

        var version = await EnsureVersionAsync(db, 5, ContentLanguage.Arabic, versionName);

        var chunks = new[]
        {
            new ChunkData("arabic.grade3.phonics.recognize-letters",
                "التعرف على الحروف العربية: الأبجدية العربية تحتوي على 28 حرفاً. يكتب كل حرف بشكل مختلف حسب موضعه في الكلمة: أول الكلمة، وسطها، أو آخرها.",
                1, SubjectId:5, GradeId:3),
            new ChunkData("arabic.grade3.phonics.read-with-diacritics",
                "قراءة الكلمات بالحركات القصيرة: الحركات القصيرة هي الفتحة والضمة والكسرة والسكون. تساعد الحركات على نطق الكلمات بشكل صحيح.",
                2, SubjectId:5, GradeId:3),
            new ChunkData("arabic.grade3.grammar.nouns-verbs",
                "تمييز الأسماء والأفعال: الاسم هو كلمة تدل على شيء أو شخص أو مكان. الفعل هو كلمة تدل على حدث أو عمل. مثال: الكتاب (اسم)، قرأ (فعل).",
                1, SubjectId:5, GradeId:3),
            new ChunkData("arabic.grade3.grammar.write-simple-sentences",
                "كتابة جمل عربية بسيطة: الجملة الفعلية تبدأ بفعل ثم فاعل. الجملة الاسمية تبدأ بمبتدأ ثم خبر. مثال: ذهب أحمد إلى المدرسة.",
                2, SubjectId:5, GradeId:3),
        };

        await InsertChunksAsync(db, version, chunks, logger);
        logger?.LogInfo($"P3-07 CurriculumChunkSeeder: seeded {chunks.Length} chunks for '{versionName}'.");
    }

    // =========================================================================
    // English — English only (Grade 3 MVP)
    // =========================================================================

    private static async Task SeedEnglishEnAsync(CurriculumDbContext db, ILoggerManager? logger)
    {
        const string versionName = "MVP-G3-English-En-v1";
        if (await db.CurriculumVersions.AnyAsync(v => v.Name == versionName))
        {
            logger?.LogInfo($"P3-07 CurriculumChunkSeeder: '{versionName}' already seeded; skipping.");
            return;
        }

        var version = await EnsureVersionAsync(db, 6, ContentLanguage.English, versionName);

        var chunks = new[]
        {
            new ChunkData("english.grade3.vocabulary.sight-words",
                "Sight Words: Sight words are common words recognized instantly without sounding them out. Examples: the, and, is, are, was, were. Memorizing sight words improves reading fluency.",
                1, SubjectId:6, GradeId:3),
            new ChunkData("english.grade3.vocabulary.context-clues",
                "Using Context Clues: Context clues are words or phrases near an unfamiliar word that help you understand its meaning. Look at surrounding sentences for hints.",
                2, SubjectId:6, GradeId:3),
            new ChunkData("english.grade3.grammar.parts-of-speech",
                "Identifying Parts of Speech: Nouns name people, places, or things. Verbs describe actions or states. Adjectives describe nouns. Example: 'The big dog runs fast.' (big=adjective, dog=noun, runs=verb)",
                1, SubjectId:6, GradeId:3),
            new ChunkData("english.grade3.grammar.simple-past",
                "Writing in Simple Past Tense: The simple past tense describes completed actions. Regular verbs add -ed: walk→walked, jump→jumped. Irregular verbs change form: go→went, see→saw.",
                2, SubjectId:6, GradeId:3),
        };

        await InsertChunksAsync(db, version, chunks, logger);
        logger?.LogInfo($"P3-07 CurriculumChunkSeeder: seeded {chunks.Length} chunks for '{versionName}'.");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static async Task<CurriculumVersion> EnsureVersionAsync(
        CurriculumDbContext db,
        int subjectId,
        ContentLanguage language,
        string name)
    {
        var existing = await db.CurriculumVersions
            .FirstOrDefaultAsync(v => v.SubjectId == subjectId && v.Language == language);

        if (existing is not null)
            return existing;

        var version = new CurriculumVersion
        {
            SubjectId = subjectId,
            Language  = language,
            Status    = CurriculumVersionStatus.Active,
            Name      = name,
        };
        db.CurriculumVersions.Add(version);
        await db.SaveChangesAsync(SystemUserId);
        return version;
    }

    private static async Task InsertChunksAsync(
        CurriculumDbContext db,
        CurriculumVersion version,
        ChunkData[] chunks,
        ILoggerManager? logger)
    {
        foreach (var data in chunks)
        {
            // Check idempotency: skip if a chunk with this SkillKey already exists for this version.
            var exists = await db.CurriculumChunks
                .AnyAsync(c => c.CurriculumVersionId == version.Id && c.SkillKey == data.SkillKey);
            if (exists)
                continue;

            var chunk = new CurriculumChunk
            {
                Content            = data.Content,
                Metadata           = $"{{\"skillKey\":\"{data.SkillKey}\",\"gradeId\":{data.GradeId},\"subjectId\":{data.SubjectId}}}",
                Difficulty         = data.Difficulty,
                SubjectId          = data.SubjectId,
                GradeId            = data.GradeId,
                SkillKey           = data.SkillKey,
                Language           = version.Language,
                CurriculumVersionId = version.Id,
                ProvenanceRef      = null, // null for seeded chunks; BL-05 backfills
            };
            db.CurriculumChunks.Add(chunk);
            await db.SaveChangesAsync(SystemUserId);

            // Compute deterministic placeholder embedding.
            // RE-EMBED REQUIRED: once BE-0 (live BGE-M3 TEI) is provisioned, run re-ingestion
            // against the live TEI endpoint for all rows with ModelVersion='seed-placeholder-v0'.
            var vector = await DeterministicEmbedding.ComputeAsync(data.Content);

            var embedding = new ChunkEmbeddingBgeM3
            {
                ChunkId      = chunk.Id,
                Provider     = DeterministicEmbedding.PlaceholderProvider,
                Model        = DeterministicEmbedding.PlaceholderModel,
                ModelVersion = DeterministicEmbedding.PlaceholderModelVersion,
                Dimension    = DeterministicEmbedding.Dimension,
                Vector       = vector,
                IsActive     = true,
            };
            db.ChunkEmbeddingsBgeM3.Add(embedding);
            await db.SaveChangesAsync(SystemUserId);
        }
    }

    // -------------------------------------------------------------------------
    // Internal record for chunk seed data
    // -------------------------------------------------------------------------
    private sealed record ChunkData(
        string SkillKey,
        string Content,
        int Difficulty,
        int SubjectId,
        int GradeId);
}
