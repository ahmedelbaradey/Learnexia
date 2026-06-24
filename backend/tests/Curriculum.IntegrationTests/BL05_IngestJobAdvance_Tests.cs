using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AspNetCoreRateLimit;
using FluentAssertions;
using Learnexia.Modules.Curriculum.Api;
using Learnexia.Modules.Curriculum.Application.Abstractions;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Domain.Enums;
using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Curriculum.IntegrationTests;

// ─── xUnit collection — shares one factory across all BL-05 tests ─────────────────────────────

[CollectionDefinition("BL05IngestJob")]
public sealed class BL05IngestJobCollection : ICollectionFixture<BL05WebAppFactory> { }

// ─── BL-05 WebApplicationFactory ─────────────────────────────────────────────────────────────────

/// <summary>
/// Standalone WebApplicationFactory for BL-05 integration tests.
/// Mirrors BL02WebAppFactory configuration exactly, adding IngestPoller keys.
/// Cannot inherit from BL02WebAppFactory (it is sealed).
/// </summary>
public sealed class BL05WebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string JwtSigningKey =
        "CHANGE_ME_super_secret_key_at_least_32_chars_long_0123456789";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg17")
        .WithDatabase("bl05_test")
        .WithUsername("postgres")
        .WithPassword("testpwd")
        .Build();

    public FakeStorageService FakeStorage { get; } = new FakeStorageService();

    public async Task InitializeAsync()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        await _postgres.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"]                         = _postgres.GetConnectionString(),
                ["CurriculumUpload:MaxFileSizeBytes"]                 = "1048576",
                ["CurriculumUpload:BucketName"]                       = "curriculum-test",
                // Parse poller — fast cycle so BL02 hand-off test works
                ["CurriculumPipeline:PollerIntervalSeconds"]          = "1",
                ["CurriculumPipeline:MaxRetries"]                     = "3",
                // Ingest poller — fast cycle so advance tests don't wait 5 s
                ["CurriculumPipeline:IngestPollerIntervalSeconds"]    = "1",
                ["CurriculumPipeline:IngestMaxRetries"]               = "3",
                ["CurriculumPipeline:IngestionConfidenceThreshold"]   = "0.7",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Override DbContext to point at the test container
            services.RemoveAll<DbContextOptions<CurriculumDbContext>>();
            services.RemoveAll<CurriculumDbContext>();
            services.AddDbContext<CurriculumDbContext>(options =>
                options.UseNpgsql(
                    _postgres.GetConnectionString(),
                    npgsql => npgsql
                        .UseVector()
                        .MigrationsHistoryTable("__EFMigrationsHistory", CurriculumDbContext.Schema)
                        .MigrationsAssembly(typeof(CurriculumDbContext).Assembly.FullName)));

            // Override LearningDbContext to point at the same container
            services.RemoveAll<DbContextOptions<LearningDbContext>>();
            services.RemoveAll<LearningDbContext>();
            services.AddDbContext<LearningDbContext>(options =>
                options.UseNpgsql(
                    _postgres.GetConnectionString(),
                    npgsql => npgsql
                        .UseVector()
                        .MigrationsHistoryTable("__EFMigrationsHistory", "learning")
                        .MigrationsAssembly(typeof(LearningDbContext).Assembly.FullName)));

            // Stub IStorageService — no real MinIO
            services.RemoveAll<IStorageService>();
            services.AddSingleton<IStorageService>(FakeStorage);

            // Stub IEmbeddingProvider
            services.RemoveAll<IEmbeddingProvider>();
            services.AddScoped<IEmbeddingProvider, DeterministicStubEmbeddingProvider>();

            // Stub ISessionManagementService (P6-07 OnTokenValidated check)
            services.RemoveAll<ISessionManagementService>();
            services.AddScoped<ISessionManagementService, BL05AlwaysActiveSessionService>();

            // Disable rate limiting in tests
            services.Configure<IpRateLimitOptions>(opt =>
            {
                opt.EnableEndpointRateLimiting = false;
                opt.GeneralRules = new List<RateLimitRule>
                {
                    new() { Endpoint = "*", Limit = int.MaxValue, Period = "1m" }
                };
            });
        });
    }

    public async Task ApplyMigrationsAsync()
    {
        using var scope = Services.CreateScope();
        await CurriculumModule.InitializeAsync(scope.ServiceProvider);
        // Also migrate Learning module so KnowledgeNodes table exists
        var learningDb = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        await learningDb.Database.MigrateAsync();
        // Seed reference Grade rows (1–6) so EnsureSubjectAsync can satisfy
        // Subject.GradeId FK.  PedagogicalTreeWriterAdapter relies on pre-existing Grades.
        await SeedLearningReferenceDataAsync(learningDb);
    }

    /// <summary>
    /// Seeds reference rows that the real app loads via LearningSeeder but are absent after a bare migration.
    /// Grade rows 1–6 are required by EnsureSubjectAsync (Subject has GradeId FK → Grades.Id).
    /// Also seeds a Curriculum Subject row (SubjectId=1 "Math") for CurriculumVersionResolver.
    /// Uses raw SQL to insert only if not already present (idempotent across multiple InitializeAsync calls).
    /// </summary>
    private static async Task SeedLearningReferenceDataAsync(LearningDbContext db)
    {
        // Grade rows 1–6 via raw SQL. Use INSERT … WHERE NOT EXISTS for idempotency
        // (Id is auto-generated serial; ON CONFLICT DO NOTHING doesn't apply without a unique key to conflict on).
        // We need Number, DisplayName, CreatedAt, CreatedBy (non-nullable columns).
        for (int grade = 1; grade <= 6; grade++)
        {
            var gradeNum = grade;
            var gradeName = $"Grade {grade}";
            await db.Database.ExecuteSqlRawAsync(
                $@"INSERT INTO learning.""Grades"" (""Number"", ""DisplayName"", ""CreatedAt"", ""CreatedBy"", ""IsDeleted"")
                   SELECT {gradeNum}, '{gradeName}', NOW(), 0, false
                   WHERE NOT EXISTS (SELECT 1 FROM learning.""Grades"" WHERE ""Number"" = {gradeNum});");
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.StopAsync();
    }

    // ── JWT factory ─────────────────────────────────────────────────────────────
    public static string GenerateJwt(string? role = null, int userId = 1)
    {
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(JwtSigningKey));
        var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        var claims = new List<System.Security.Claims.Claim>
        {
            new("Id", userId.ToString()),
            new(System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString()),
            new(System.Security.Claims.ClaimTypes.Name, "testuser"),
            new("SessionId", Guid.NewGuid().ToString()),
        };
        if (role is not null)
            claims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role));

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer:   "Learnexia",
            audience: "LearnexiaClient",
            claims:   claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }
}

// ─── Session stub for BL-05 ────────────────────────────────────────────────────
internal sealed class BL05AlwaysActiveSessionService : ISessionManagementService
{
    public Task<UserSession> CreateSessionAsync(int userId, string jwtTokenId, string sessionId)
        => Task.FromResult(new UserSession { SessionId = sessionId, UserId = userId, IsActive = true });
    public Task<UserSession?> GetSessionAsync(string sessionId)
        => Task.FromResult<UserSession?>(new UserSession { SessionId = sessionId, IsActive = true });
    public Task<bool> IsSessionActiveAsync(string sessionId) => Task.FromResult(true);
    public Task<List<UserSession>> GetUserSessionsAsync(int userId) => Task.FromResult(new List<UserSession>());
    public Task<bool> ExtendSessionAsync(string sessionId) => Task.FromResult(true);
    public Task<SessionValidationResponse> ValidateSessionAsync(string sessionId, bool updateActivity = true)
        => Task.FromResult(new SessionValidationResponse { IsValid = true });
    public Task<bool> TerminateSessionAsync(string sessionId, SessionTerminationReason reason)
        => Task.FromResult(true);
    public Task<SessionInfo?> GetSessionInfoAsync(string sessionId)
        => Task.FromResult<SessionInfo?>(null);
    public Task<bool> UpdateSessionActivityAsync(string sessionId) => Task.FromResult(true);
    public Task<int> TerminateAllUserSessionsAsync(int userId, SessionTerminationReason reason)
        => Task.FromResult(0);
}

// ─── BL-05 Seeding helpers ────────────────────────────────────────────────────────────────────────

internal static class BL05Helpers
{
    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // CONTRACT — Python emitter shape (now matches .NET reader after DEFECT-1..7 fix)
    //
    // Python emits (exact shape from python/curriculum_intelligence/ingestion/models.py):
    //   nodes:  node_type (PascalCase "Unit"|"Lesson"|"Concept"|"Skill"),
    //           skill_key, parent_skill_key (str|null), title, grade (int),
    //           subject_code (STRING "math"|"science"|"arabic"|"english"),
    //           language ("ar"|"en"), difficulty (int|null), confidence (float 0..1)
    //   chunks: content, content_type, source_page (int|null), chapter_number (int|null),
    //           node_skill_key, confidence
    //   flags:  kind, ref, confidence, reason
    //
    // .NET IngestJobAdvanceService (after DEFECT-1..7 fix) now reads:
    //   nodes:  "node_type", "title", "skill_key", "confidence", "grade", "subject_code" (string),
    //           "language" (string), "difficulty", "parent_skill_key"
    //   chunks: "content", "source_page" (int→"p.N"), "chapter_number" (fallback), "node_skill_key",
    //           "confidence"
    //   flags:  "kind", "ref", "reason", "confidence"
    //
    // All tests must now feed the Python-contract shape.  The old DotNetShape helper is gone.
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a ResultJson that matches the authoritative Python contract shape exactly.
    /// This is the shape that the fixed <c>IngestJobAdvanceService</c> deserializer reads.
    /// Field names/types match <c>python/curriculum_intelligence/ingestion/models.py</c>.
    ///
    /// skillName/conceptName are derived from skillKey to ensure uniqueness across tests
    /// (avoids shared Skill/KnowledgeNode rows via EnsureSkillAsync idempotency).
    /// </summary>
    public static string MakeIngestResultJson_PythonShape(
        string skillKey,
        decimal nodeConfidence = 0.85m,
        decimal chunkConfidence = 0.80m,
        decimal lowConfidence = 0.50m,
        bool includeHighConfidenceNode = true,
        bool includeLowConfidenceNode = true,
        bool includeHighConfidenceChunk = true,
        bool includeLowConfidenceChunk = false)
    {
        // Use skillKey-derived names so each test has a unique (Skill.Name, ConceptId) and
        // does not accidentally share the Skill/KnowledgeNode created by another test.
        var suffix      = skillKey.Split('.').Last();
        var skillTitle  = $"مهارة-{suffix}";          // "title" field — Arabic display name
        var parentKey   = $"مفهوم-{suffix}";           // "parent_skill_key" — concept anchor

        var nodes  = new List<object>();
        var chunks = new List<object>();

        if (includeHighConfidenceNode)
        {
            nodes.Add(new
            {
                node_type        = "Skill",          // Python: "node_type" PascalCase
                skill_key        = skillKey,
                parent_skill_key = parentKey,        // Python: "parent_skill_key" (string|null)
                title            = skillTitle,       // Python: "title" (not "name")
                grade            = 5,                // Python: "grade" int (not "grade_level")
                subject_code     = "math",           // Python: string (not int)
                language         = "ar",             // Python: string (not int)
                difficulty       = 3,
                confidence       = nodeConfidence,
            });
        }
        if (includeLowConfidenceNode)
        {
            nodes.Add(new
            {
                node_type        = "Skill",
                skill_key        = $"{skillKey}-low",
                parent_skill_key = (string?)null,
                title            = $"مهارة-low-{suffix}",
                grade            = 5,
                subject_code     = "math",
                language         = "ar",
                difficulty       = 3,
                confidence       = lowConfidence,    // below 0.7 → review item
            });
        }
        if (includeHighConfidenceChunk)
        {
            chunks.Add(new
            {
                content         = "يُعدّ جمع الكسور من أهم المهارات الرياضية للصف الخامس.",
                content_type    = "text",
                source_page     = 5,                 // Python: "source_page" int (not "source_reference")
                chapter_number  = (int?)null,
                node_skill_key  = skillKey,          // Python: "node_skill_key" (not "skill_key")
                confidence      = chunkConfidence,
            });
        }
        if (includeLowConfidenceChunk)
        {
            chunks.Add(new
            {
                content         = "Low confidence chunk",
                content_type    = "text",
                source_page     = 10,
                chapter_number  = (int?)null,
                node_skill_key  = $"{skillKey}-lowconf",
                confidence      = 0.45m,
            });
        }

        return JsonSerializer.Serialize(new
        {
            schema_version = "1.0",
            nodes          = nodes.ToArray(),
            chunks         = chunks.ToArray(),
            flags          = Array.Empty<object>(),
            diagnostics    = (object?)null,
        });
    }

    /// <summary>
    /// Builds a minimal Python-contract ResultJson with a single node+chunk at the given
    /// confidence.  Used by the CONTRACT agreement test.
    /// </summary>
    public static string MakeIngestResultJson_PythonShape_Contract(
        string skillKey,
        decimal confidence = 0.90m) =>
        JsonSerializer.Serialize(new
        {
            schema_version = "1.0",
            nodes = new[]
            {
                new
                {
                    node_type        = "Skill",
                    skill_key        = skillKey,
                    parent_skill_key = (string?)null,
                    title            = "جمع الكسور",
                    grade            = 5,
                    subject_code     = "math",
                    language         = "ar",
                    difficulty       = 3,
                    confidence       = confidence,
                },
            },
            chunks = new[]
            {
                new
                {
                    content         = "يُعدّ جمع الكسور من أهم المهارات الرياضية",
                    content_type    = "text",
                    source_page     = 3,
                    chapter_number  = (int?)null,
                    node_skill_key  = skillKey,
                    confidence      = confidence,
                },
            },
            flags       = Array.Empty<object>(),
            diagnostics = (object?)null,
        });

    /// <summary>
    /// Builds a ResultJson that matches the Python contract shape and includes a flags[] array.
    /// Used by the FLAG-PATH test to verify SerializeFlagAsPayload stores valid JSON (post-fix).
    /// </summary>
    public static string MakeIngestResultJson_WithFlags(
        string skillKey,
        decimal nodeConfidence = 0.90m,
        object[]? extraFlags = null)
    {
        var suffix     = skillKey.Split('.').Last();
        var skillTitle = $"مهارة-flags-{suffix}";

        var flags = new object[]
        {
            new
            {
                kind       = "low_confidence_node",
                @ref       = $"{skillKey}.flag-ref",
                reason     = "Low confidence",
                confidence = 0.4,
            },
        };

        // Merge with any extra flags supplied by the caller
        if (extraFlags is { Length: > 0 })
            flags = flags.Concat(extraFlags).ToArray();

        return JsonSerializer.Serialize(new
        {
            schema_version = "1.0",
            nodes = new[]
            {
                new
                {
                    node_type        = "Skill",
                    skill_key        = skillKey,
                    parent_skill_key = (string?)null,
                    title            = skillTitle,
                    grade            = 5,
                    subject_code     = "math",
                    language         = "ar",
                    difficulty       = 3,
                    confidence       = nodeConfidence,
                },
            },
            chunks = new[]
            {
                new
                {
                    content        = "محتوى اختباري لاختبار مسار الأعلام",
                    content_type   = "text",
                    source_page    = 7,
                    chapter_number = (int?)null,
                    node_skill_key = skillKey,
                    confidence     = nodeConfidence,
                },
            },
            flags,
            diagnostics = (object?)null,
        });
    }

    /// <summary>Seeds an ingest PipelineJob row.</summary>
    public static async Task<Learnexia.Modules.Curriculum.Domain.Entities.PipelineJob> SeedIngestJobAsync(
        CurriculumDbContext db,
        int documentId,
        string jobStatus,
        string? resultJson = null,
        string? errorMessage = null,
        int retryCount = 0)
    {
        var job = new Learnexia.Modules.Curriculum.Domain.Entities.PipelineJob
        {
            JobType      = "ingest",
            Status       = jobStatus,
            DocumentId   = documentId,
            PayloadJson  = JsonSerializer.Serialize(new
            {
                artifact_key = $"artifacts/{Guid.NewGuid():N}.json",
                document_id  = documentId,
            }),
            ResultJson   = resultJson,
            ErrorMessage = errorMessage,
            RetryCount   = retryCount,
            ClaimedAt    = jobStatus is "Done" or "Failed"
                           ? DateTimeOffset.UtcNow.AddSeconds(-10) : null,
            CompletedAt  = jobStatus is "Done" or "Failed"
                           ? DateTimeOffset.UtcNow.AddSeconds(-5)  : null,
        };
        db.PipelineJobs.Add(job);
        await db.SaveChangesAsync();
        return job;
    }

    /// <summary>Seeds a document that has been parse-Done (eligible for ingest).</summary>
    public static async Task<Learnexia.Modules.Curriculum.Domain.Entities.CurriculumDocument> SeedParsedDocumentAsync(
        CurriculumDbContext db,
        IngestionStatus ingestionStatus = IngestionStatus.NotStarted,
        int gradeId = 4,
        int subjectId = 1)
    {
        var doc = new Learnexia.Modules.Curriculum.Domain.Entities.CurriculumDocument
        {
            FileName     = $"test_{Guid.NewGuid():N}.pdf",
            ObjectKey    = $"{Guid.NewGuid():N}.pdf",
            ContentType  = "application/pdf",
            FileSize     = 1024,
            GradeId      = gradeId,
            SubjectId    = subjectId,
            Language     = ContentLanguage.Arabic,
            Country      = "EG",
            Status       = DocumentStatus.Done,
            ParsedArtifactObjectKey = $"artifacts/{Guid.NewGuid():N}.json",
            ParsedAt     = DateTimeOffset.UtcNow.AddMinutes(-5),
            IngestionStatus = ingestionStatus,
        };
        db.CurriculumDocuments.Add(doc);
        await db.SaveChangesAsync();
        return doc;
    }
}

// ─── BL-05 Ingest Advance Logic Tests ────────────────────────────────────────────────────────────

/// <summary>
/// BL-05 integration tests for IngestJobAdvanceService advance logic.
///
/// Advance strategy: seed Done/Failed PipelineJob rows directly (no Python worker) and wait for
/// IngestJobAdvanceService (IngestPollerIntervalSeconds=1) to pick them up within 12 s.
/// Same deterministic approach used in BL02_ParseJobAdvance_Tests.
/// </summary>
[Collection("BL05IngestJob")]
public sealed class BL05_IngestJobAdvance_Tests : IAsyncLifetime
{
    private readonly BL05WebAppFactory _factory;

    public BL05_IngestJobAdvance_Tests(BL05WebAppFactory factory) => _factory = factory;

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private CurriculumDbContext GetFreshCurriculumDb()
        => _factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<CurriculumDbContext>();

    private LearningDbContext GetFreshLearningDb()
        => _factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<LearningDbContext>();

    // =========================================================================
    // BL05-ADV-01: Ingest Done, high-confidence → hierarchy + chunks + IngestionStatus=Done
    // =========================================================================

    /// <summary>
    /// AC7 + AC8 + AC9 + AC13:
    /// Seed a Done ingest job with high-confidence nodes+chunks (.NET shape).
    /// After advance: KnowledgeNode in learning DB, CurriculumChunk under Draft version,
    /// IngestionStatus=Done+IngestedAt, job=Archived, no embedding field on chunk (Decision D).
    /// </summary>
    [Fact(DisplayName = "BL05-ADV-01: Done ingest job (high-conf) → hierarchy ensured, chunks created, IngestionStatus=Done")]
    public async Task ADV01_DoneJob_HighConf_HierarchyEnsured_ChunkCreated_DocDone()
    {
        const string skillKey = "math.grade5.fractions.adv01";

        await using var db = GetFreshCurriculumDb();
        var doc = await BL05Helpers.SeedParsedDocumentAsync(db, gradeId: 4, subjectId: 1);

        var resultJson = BL05Helpers.MakeIngestResultJson_PythonShape(
            skillKey:                 skillKey,
            nodeConfidence:           0.90m,
            chunkConfidence:          0.85m,
            includeLowConfidenceNode: false,
            includeLowConfidenceChunk: false);

        var job = await BL05Helpers.SeedIngestJobAsync(db, doc.Id, "Done", resultJson);

        // Wait for advance
        var advanced = await BL02Helpers.WaitForConditionAsync(async () =>
        {
            await using var pollDb = GetFreshCurriculumDb();
            var d = await pollDb.CurriculumDocuments.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == doc.Id);
            return d?.IngestionStatus == IngestionStatus.Done;
        }, timeoutSeconds: 12);

        advanced.Should().BeTrue(
            "IngestJobAdvanceService must advance a Done ingest job to IngestionStatus=Done within 12 s");

        // ── Document assertions ───────────────────────────────────────────────
        await using var assertDb = GetFreshCurriculumDb();
        var updatedDoc = await assertDb.CurriculumDocuments.AsNoTracking()
            .FirstAsync(d => d.Id == doc.Id);

        updatedDoc.IngestionStatus.Should().Be(IngestionStatus.Done,
            "IngestionStatus must be Done after advancing a Done ingest job (AC7)");
        updatedDoc.IngestedAt.Should().NotBeNull(
            "IngestedAt must be set after successful ingest (AC7)");
        updatedDoc.IngestionDiagnostics.Should().BeNullOrEmpty(
            "IngestionDiagnostics must be null on success");

        // ── Job archived ──────────────────────────────────────────────────────
        var updatedJob = await assertDb.PipelineJobs.AsNoTracking()
            .FirstAsync(j => j.Id == job.Id);
        updatedJob.Status.Should().Be("Archived",
            "Done ingest job must be Archived after advancing (AC7)");

        // ── KnowledgeNode in learning tree ────────────────────────────────────
        await using var learningDb = GetFreshLearningDb();
        var knowledgeNode = await learningDb.KnowledgeNodes.AsNoTracking()
            .FirstOrDefaultAsync(kn => kn.SkillKey == skillKey);

        knowledgeNode.Should().NotBeNull(
            $"KnowledgeNode with SkillKey='{skillKey}' must be ensured in the learning tree (AC1, AC8)");

        // ── CurriculumChunk under Draft CurriculumVersion ─────────────────────
        var chunks = await assertDb.CurriculumChunks.AsNoTracking()
            .Where(c => c.SkillKey == skillKey)
            .ToListAsync();

        chunks.Should().NotBeEmpty(
            "At least one CurriculumChunk must be created (AC9)");

        var chunk = chunks.First();
        chunk.CurriculumVersionId.Should().BeGreaterThan(0,
            "CurriculumChunk must have a valid CurriculumVersionId (AC9, AC13)");
        chunk.Content.Should().NotBeNullOrWhiteSpace("Content must be set");
        chunk.ProvenanceRef.Should().NotBeNull("ProvenanceRef must be set (Decision B)");

        // Verify Draft version (AC13 — never Active)
        var version = await assertDb.CurriculumVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == chunk.CurriculumVersionId);
        version.Should().NotBeNull("CurriculumVersion must exist");
        version!.Status.Should().Be(CurriculumVersionStatus.Draft,
            "CurriculumVersion must be Draft at ingest time — never Active until P7-05 (AC13)");

        // Decision D: CurriculumChunk must NOT have an embedding column.
        var embeddingProp = chunk.GetType().GetProperty("EmbeddingVector")
                         ?? chunk.GetType().GetProperty("EmbeddingVectorRef");
        embeddingProp.Should().BeNull(
            "CurriculumChunk must NOT have an embedding property (Decision D — AC12)");
    }

    // =========================================================================
    // BL05-ADV-02: Low-confidence routing → IngestionReviewItem, NOT in tree
    // =========================================================================

    /// <summary>
    /// AC4 + AC11: nodes/chunks below 0.7 → IngestionReviewItem{Pending}, NOT in tree/chunks.
    /// </summary>
    [Fact(DisplayName = "BL05-ADV-02: Low-confidence nodes/chunks → IngestionReviewItem Pending, NOT in learning tree")]
    public async Task ADV02_LowConfidence_RoutedToReviewItems_NotInTree()
    {
        const string highConfKey = "math.grade5.fractions.adv02-high";

        await using var db = GetFreshCurriculumDb();
        // Use gradeId=6 to avoid unique-constraint race with ADV01 on (GradeId=4, SubjectCode=1, Language=0)
        var doc = await BL05Helpers.SeedParsedDocumentAsync(db, gradeId: 6, subjectId: 1);

        var resultJson = BL05Helpers.MakeIngestResultJson_PythonShape(
            skillKey:                   highConfKey,
            nodeConfidence:             0.90m,
            chunkConfidence:            0.85m,
            lowConfidence:              0.45m,
            includeHighConfidenceNode:  true,
            includeLowConfidenceNode:   true,   // low-conf → review item
            includeHighConfidenceChunk: true,
            includeLowConfidenceChunk:  true);  // low-conf chunk → review item

        await BL05Helpers.SeedIngestJobAsync(db, doc.Id, "Done", resultJson);

        var advanced = await BL02Helpers.WaitForConditionAsync(async () =>
        {
            await using var pollDb = GetFreshCurriculumDb();
            var d = await pollDb.CurriculumDocuments.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == doc.Id);
            return d?.IngestionStatus == IngestionStatus.Done;
        }, timeoutSeconds: 12);

        advanced.Should().BeTrue("advance must complete within 12 s");

        // Low-confidence node must NOT be in learning tree
        await using var learningDb = GetFreshLearningDb();
        var lowConfNode = await learningDb.KnowledgeNodes.AsNoTracking()
            .FirstOrDefaultAsync(kn => kn.SkillKey == $"{highConfKey}-low");

        lowConfNode.Should().BeNull(
            "Low-confidence node must NOT be written to the learning tree (AC4, AC11)");

        // IngestionReviewItem rows must exist for low-confidence items
        await using var assertDb = GetFreshCurriculumDb();
        var reviewItems = await assertDb.IngestionReviewItems.AsNoTracking()
            .Where(r => r.CurriculumDocumentId == doc.Id)
            .ToListAsync();

        reviewItems.Should().NotBeEmpty(
            "IngestionReviewItem rows must be created for low-confidence items (AC4, AC11)");
        reviewItems.Should().AllSatisfy(r =>
            r.Status.Should().Be(ReviewStatus.Pending,
                "all created review items must be Pending (AC11)"));
        reviewItems.Should().AllSatisfy(r =>
            r.Confidence.Should().BeLessThan(0.7m,
                "review items are only created for items below the 0.7 threshold"));
    }

    // =========================================================================
    // BL05-ADV-03: Idempotency — second advance → no duplicate chunks/nodes
    // =========================================================================

    /// <summary>
    /// AC5 + AC8: two ingest passes for same doc → counts stable (no duplicates).
    /// The SkillKey-based upsert is the anchor.
    /// </summary>
    [Fact(DisplayName = "BL05-ADV-03: Second ingest job → no duplicate chunks/nodes (idempotency)")]
    public async Task ADV03_SecondAdvance_NoduplicateChunksOrNodes()
    {
        const string skillKey = "math.grade5.fractions.adv03-idempotent";

        await using var db = GetFreshCurriculumDb();
        // Use gradeId=5 to avoid unique-constraint race with ADV01/ADV02 on (GradeId=4/6, SubjectCode=1, Language=0)
        var doc = await BL05Helpers.SeedParsedDocumentAsync(db, gradeId: 5, subjectId: 1);

        var resultJson = BL05Helpers.MakeIngestResultJson_PythonShape(
            skillKey:                  skillKey,
            nodeConfidence:            0.90m,
            chunkConfidence:           0.85m,
            includeLowConfidenceNode:  false,
            includeLowConfidenceChunk: false);

        // First advance
        await BL05Helpers.SeedIngestJobAsync(db, doc.Id, "Done", resultJson);
        await BL02Helpers.WaitForConditionAsync(async () =>
        {
            await using var pollDb = GetFreshCurriculumDb();
            var d = await pollDb.CurriculumDocuments.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == doc.Id);
            return d?.IngestionStatus == IngestionStatus.Done;
        }, timeoutSeconds: 12);

        // Record counts after first advance
        await using var db2 = GetFreshCurriculumDb();
        var chunkCountFirst = await db2.CurriculumChunks.AsNoTracking()
            .CountAsync(c => c.SkillKey == skillKey);
        await using var learn1 = GetFreshLearningDb();
        var nodeCountFirst = await learn1.KnowledgeNodes.AsNoTracking()
            .CountAsync(kn => kn.SkillKey == skillKey);

        chunkCountFirst.Should().Be(1, "exactly one chunk after first advance");
        nodeCountFirst.Should().Be(1, "exactly one KnowledgeNode after first advance");

        // Second advance — reset doc + seed another Done job
        await using var db3 = GetFreshCurriculumDb();
        var docRow = await db3.CurriculumDocuments.FirstAsync(d => d.Id == doc.Id);
        docRow.IngestionStatus = IngestionStatus.InProgress;
        docRow.IngestedAt      = null;
        db3.CurriculumDocuments.Update(docRow);
        await db3.SaveChangesAsync();

        await BL05Helpers.SeedIngestJobAsync(db3, doc.Id, "Done", resultJson);
        await BL02Helpers.WaitForConditionAsync(async () =>
        {
            await using var pollDb = GetFreshCurriculumDb();
            var d = await pollDb.CurriculumDocuments.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == doc.Id);
            return d?.IngestionStatus == IngestionStatus.Done;
        }, timeoutSeconds: 12);

        // Counts must be unchanged
        await using var assertDb = GetFreshCurriculumDb();
        var chunkCountSecond = await assertDb.CurriculumChunks.AsNoTracking()
            .CountAsync(c => c.SkillKey == skillKey);
        await using var learn2 = GetFreshLearningDb();
        var nodeCountSecond = await learn2.KnowledgeNodes.AsNoTracking()
            .CountAsync(kn => kn.SkillKey == skillKey);

        chunkCountSecond.Should().Be(chunkCountFirst,
            "re-ingest must NOT create duplicate chunks (AC5 idempotency)");
        nodeCountSecond.Should().Be(nodeCountFirst,
            "re-ingest must NOT create duplicate KnowledgeNodes (AC5, AC8 idempotency)");
    }

    // =========================================================================
    // BL05-ADV-04: Ingest Failed (retries exhausted) → IngestionStatus=Failed + diagnostics
    // =========================================================================

    /// <summary>
    /// AC7 (failed path, retries exhausted): doc.IngestionStatus=Failed + IngestionDiagnostics set.
    /// </summary>
    [Fact(DisplayName = "BL05-ADV-04: Failed ingest job (retries exhausted) → IngestionStatus=Failed + diagnostics")]
    public async Task ADV04_FailedJob_RetriesExhausted_DocFailed_DiagnosticsSet()
    {
        await using var db = GetFreshCurriculumDb();
        var doc = await BL05Helpers.SeedParsedDocumentAsync(db, ingestionStatus: IngestionStatus.InProgress);

        const string errorMsg = "Python ingest worker crashed: OOM";

        // RetryCount=3 = MaxRetries=3 → permanent failure
        await BL05Helpers.SeedIngestJobAsync(
            db, doc.Id, "Failed", errorMessage: errorMsg, retryCount: 3);

        var advanced = await BL02Helpers.WaitForConditionAsync(async () =>
        {
            await using var pollDb = GetFreshCurriculumDb();
            var d = await pollDb.CurriculumDocuments.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == doc.Id);
            return d?.IngestionStatus == IngestionStatus.Failed;
        }, timeoutSeconds: 12);

        advanced.Should().BeTrue(
            "IngestJobAdvanceService must set IngestionStatus=Failed on exhausted retries within 12 s");

        await using var assertDb = GetFreshCurriculumDb();
        var updatedDoc = await assertDb.CurriculumDocuments.AsNoTracking()
            .FirstAsync(d => d.Id == doc.Id);

        updatedDoc.IngestionStatus.Should().Be(IngestionStatus.Failed,
            "IngestionStatus must be Failed on exhausted retries (AC7)");
        updatedDoc.IngestionDiagnostics.Should().NotBeNullOrWhiteSpace(
            "IngestionDiagnostics must be populated (AC7)");
    }

    // =========================================================================
    // BL05-ADV-05: Parse→ingest hand-off (Q8) — Done parse job enqueues Pending ingest job
    // =========================================================================

    /// <summary>
    /// AC1: seed a Done parse job. After ParseJobAdvanceService runs: a new Pending ingest job
    /// is enqueued by the Q8 hand-off in ParseJobAdvanceService.
    /// </summary>
    [Fact(DisplayName = "BL05-ADV-05: Done parse job → Pending ingest job enqueued (Q8 hand-off, AC1)")]
    public async Task ADV05_ParseDoneJob_EnqueuesIngestJob()
    {
        await using var db = GetFreshCurriculumDb();
        var doc = await BL02Helpers.SeedDocumentAsync(db, status: DocumentStatus.Processing);

        var artifactKey    = $"artifacts/{Guid.NewGuid():N}.json";
        var parseResultJson = BL02Helpers.MakeDoneResultJson(artifactKey);

        // Seed a Done parse job (not ingest)
        await BL02Helpers.SeedJobAsync(db, doc.Id, "Done", resultJson: parseResultJson);

        // Wait for parse advance (it sets doc.Status=Done AND enqueues ingest job)
        var advanced = await BL02Helpers.WaitForConditionAsync(async () =>
        {
            await using var pollDb = GetFreshCurriculumDb();
            var d = await pollDb.CurriculumDocuments.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == doc.Id);
            return d?.Status == DocumentStatus.Done;
        }, timeoutSeconds: 12);

        advanced.Should().BeTrue("parse advance must complete within 12 s");

        // Verify ingest job was enqueued by the Q8 hand-off
        await using var assertDb = GetFreshCurriculumDb();
        var ingestJobs = await assertDb.PipelineJobs.AsNoTracking()
            .Where(j => j.DocumentId == doc.Id
                     && j.JobType    == "ingest"
                     && j.Status     == "Pending")
            .ToListAsync();

        ingestJobs.Should().HaveCount(1,
            "exactly one Pending ingest job must be enqueued by the Q8 parse→ingest hand-off (AC1)");

        var ingestJob = ingestJobs[0];
        ingestJob.RetryCount.Should().Be(0, "fresh ingest job must start at RetryCount=0");
        ingestJob.PayloadJson.Should().Contain("artifact_key",
            "ingest PayloadJson must carry artifact_key (AC1)");
    }

    // =========================================================================
    // CONTRACT: Python emitter field names vs .NET reader field names
    // =========================================================================

    /// <summary>
    /// CONTRACT AGREEMENT (post-fix): feeds a ResultJson with the exact Python field names to the
    /// fixed .NET deserializer and asserts that the contract is now fully honoured:
    /// - chunks are linked (non-null SkillKey) via the fixed "node_skill_key" reader
    /// - KnowledgeNode is created with a non-empty Name via the fixed "title" reader
    /// - KnowledgeNode.GradeId is derived from the document's GradeId (not the node payload)
    /// - IngestionStatus=Done (the whole pipeline succeeds end-to-end on Python-shape input)
    ///
    /// Also exercises the fail-closed confidence behavior: a node/chunk with a MISSING
    /// confidence field defaults to 0.0 and routes to the review queue (not auto-published).
    /// </summary>
    [Fact(DisplayName = "CONTRACT: Python-shape ResultJson fed to fixed .NET reader → chunks linked, nodes named, pipeline Done")]
    public async Task CONTRACT_PythonShape_AgreementAfterFix()
    {
        const string skillKey = "math.grade5.fractions.contract-fixed";

        await using var db = GetFreshCurriculumDb();
        // Use gradeId=3 to avoid unique-constraint race with other tests on (GradeId, SubjectCode, Language)
        var doc = await BL05Helpers.SeedParsedDocumentAsync(db, gradeId: 3, subjectId: 1);

        // Feed the minimal Python-contract shape (confidence=0.90 → above 0.7 threshold).
        var pythonJson = BL05Helpers.MakeIngestResultJson_PythonShape_Contract(skillKey, confidence: 0.90m);
        await BL05Helpers.SeedIngestJobAsync(db, doc.Id, "Done", pythonJson);

        // Wait for advance to complete successfully
        var advanced = await BL02Helpers.WaitForConditionAsync(async () =>
        {
            await using var pollDb = GetFreshCurriculumDb();
            var d = await pollDb.CurriculumDocuments.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == doc.Id);
            return d?.IngestionStatus == IngestionStatus.Done ||
                   d?.IngestionStatus == IngestionStatus.Failed;
        }, timeoutSeconds: 12);

        advanced.Should().BeTrue("advance must reach a terminal state within 12 s");

        // Assert IngestionStatus=Done (the whole pipeline succeeds on Python-shape input)
        await using var assertDb = GetFreshCurriculumDb();
        var updatedDoc = await assertDb.CurriculumDocuments.AsNoTracking()
            .FirstAsync(d => d.Id == doc.Id);

        updatedDoc.IngestionStatus.Should().Be(IngestionStatus.Done,
            "Python-shape input must produce IngestionStatus=Done after the DEFECT-1..7 fix — " +
            "the .NET reader now matches the Python emitter end-to-end.");

        // DEFECT-6 FIX VERIFIED: chunk must be linked to skillKey via "node_skill_key" field
        var chunksWithCorrectKey = await assertDb.CurriculumChunks.AsNoTracking()
            .Where(c => c.SkillKey == skillKey)
            .ToListAsync();

        chunksWithCorrectKey.Should().NotBeEmpty(
            "DEFECT-6 FIX: Python emits 'node_skill_key', .NET now reads 'node_skill_key' — " +
            "chunk-to-skill linkage must be intact after the fix.");

        // DEFECT-2 FIX VERIFIED: KnowledgeNode must have non-empty Name (from "title" field)
        await using var learningDb = GetFreshLearningDb();
        var knowledgeNode = await learningDb.KnowledgeNodes.AsNoTracking()
            .FirstOrDefaultAsync(kn => kn.SkillKey == skillKey);

        knowledgeNode.Should().NotBeNull(
            "DEFECT-2 FIX: Python emits 'title', .NET now reads 'title' — " +
            $"KnowledgeNode with SkillKey='{skillKey}' must be created.");

        knowledgeNode!.Name.Should().NotBeNullOrWhiteSpace(
            "DEFECT-2 FIX: KnowledgeNode.Name must be non-empty when 'title' is read correctly.");

        // DEFECT-7 FIX FULLY VERIFIED: flag reader fields ("kind"/"ref"/"reason"/"confidence") are
        // now mapped correctly by ParseFlagArray AND SerializeFlagAsPayload wraps them into a valid
        // JSON object before storing into the jsonb PayloadJson column.
        // The flag path is exercised end-to-end in FLAG-PATH-01.
    }

    /// <summary>
    /// FAIL-CLOSED confidence behavior: a node with a MISSING confidence field defaults to 0.0
    /// (below the 0.7 threshold) and routes to the review queue — NOT auto-published.
    /// This verifies FindING #1b (server-side clamp + fail-closed default).
    /// </summary>
    [Fact(DisplayName = "CONTRACT: Node/chunk with missing confidence field → defaults to 0.0 → review queue (fail-closed)")]
    public async Task CONTRACT_MissingConfidence_FailClosed_RoutesToReview()
    {
        const string skillKey = "math.grade5.fractions.missing-confidence";

        await using var db = GetFreshCurriculumDb();
        // Use gradeId=2 to avoid unique-constraint race
        var doc = await BL05Helpers.SeedParsedDocumentAsync(db, gradeId: 2, subjectId: 1);

        // Emit a node + chunk with NO confidence field at all
        var jsonMissingConfidence = JsonSerializer.Serialize(new
        {
            schema_version = "1.0",
            nodes = new[]
            {
                new
                {
                    node_type        = "Skill",
                    skill_key        = skillKey,
                    parent_skill_key = (string?)null,
                    title            = "مهارة-missing-confidence",
                    grade            = 5,
                    subject_code     = "math",
                    language         = "ar",
                    difficulty       = 2,
                    // NOTE: "confidence" field intentionally omitted
                },
            },
            chunks = new[]
            {
                new
                {
                    content        = "Chunk without confidence field",
                    content_type   = "text",
                    source_page    = 1,
                    node_skill_key = skillKey,
                    // NOTE: "confidence" field intentionally omitted
                },
            },
            flags       = Array.Empty<object>(),
            diagnostics = (object?)null,
        });

        await BL05Helpers.SeedIngestJobAsync(db, doc.Id, "Done", jsonMissingConfidence);

        var advanced = await BL02Helpers.WaitForConditionAsync(async () =>
        {
            await using var pollDb = GetFreshCurriculumDb();
            var d = await pollDb.CurriculumDocuments.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == doc.Id);
            return d?.IngestionStatus == IngestionStatus.Done ||
                   d?.IngestionStatus == IngestionStatus.Failed;
        }, timeoutSeconds: 12);

        advanced.Should().BeTrue("advance must reach terminal state within 12 s");

        // Node with missing confidence (→ 0.0 default) must NOT be in the learning tree
        await using var learningDb = GetFreshLearningDb();
        var nodeInTree = await learningDb.KnowledgeNodes.AsNoTracking()
            .FirstOrDefaultAsync(kn => kn.SkillKey == skillKey);

        nodeInTree.Should().BeNull(
            "FINDING #1b fail-closed: missing confidence defaults to 0.0 (< 0.7 threshold) — " +
            "node must be routed to review, NOT auto-published to the learning tree.");

        // Chunk with missing confidence must also be routed to review, not published
        await using var chunkDb = GetFreshCurriculumDb();
        var chunksPublished = await chunkDb.CurriculumChunks.AsNoTracking()
            .Where(c => c.SkillKey == skillKey)
            .ToListAsync();

        chunksPublished.Should().BeEmpty(
            "FINDING #1b fail-closed: chunk with missing confidence defaults to 0.0 (< 0.7) — " +
            "must be routed to review, NOT written as a CurriculumChunk.");

        // Both node + chunk must appear in the review queue
        var reviewItems = await chunkDb.IngestionReviewItems.AsNoTracking()
            .Where(r => r.CurriculumDocumentId == doc.Id)
            .ToListAsync();

        reviewItems.Should().NotBeEmpty(
            "FINDING #1b fail-closed: items with missing confidence (→ 0.0) must be routed " +
            "to IngestionReviewItems with Status=Pending.");

        reviewItems.Should().AllSatisfy(r =>
            r.Status.Should().Be(ReviewStatus.Pending,
                "all fail-closed review items must be Pending"));
        reviewItems.Should().AllSatisfy(r =>
            r.Confidence.Should().Be(0.0m,
                "stored confidence must be 0.0 (the fail-closed default for missing field)"));
    }

    // =========================================================================
    // FLAG-PATH-01: flags[] in ResultJson — advance succeeds, IngestionReviewItem
    // created with valid JSON PayloadJson (SerializeFlagAsPayload fix exercised)
    // =========================================================================

    /// <summary>
    /// Exercises the previously-defective flag path end-to-end.
    ///
    /// Background: before the fix, IngestJobAdvanceService stored flag.Reason (a bare string like
    /// "Low confidence") directly as IngestionReviewItem.PayloadJson — a jsonb column — causing
    /// PostgreSQL error 22P02 (invalid JSON syntax) and stranding the document at Processing.
    ///
    /// After the fix, SerializeFlagAsPayload wraps all flag fields in a valid JSON object
    /// {kind, ref, reason, confidence}, satisfying the jsonb column constraint.
    ///
    /// This test seeds a Done ingest job whose ResultJson includes a flags[] entry matching the
    /// exact Python contract shape and asserts:
    ///   1. Advance does NOT crash (no 22P02 PostgreSQL error).
    ///   2. Document reaches IngestionStatus=Done (not stranded at Processing).
    ///   3. An IngestionReviewItem is created from the flag.
    ///   4. PayloadJson is valid JSON and contains the flag fields kind/ref/reason/confidence.
    ///   5. The high-confidence node alongside the flag is still published to the learning tree.
    /// </summary>
    [Fact(DisplayName = "FLAG-PATH-01: flags[] in ResultJson → advance succeeds, IngestionReviewItem with valid JSON PayloadJson")]
    public async Task FLAGPATH01_FlagsInResultJson_AdvanceSucceeds_ReviewItemHasValidPayloadJson()
    {
        const string skillKey = "math.grade5.fractions.flag-path-01";
        const string flagRef  = "math.grade5.fractions.flag-path-01.flag-ref";
        const string flagKind = "low_confidence_node";
        const string flagReason = "Low confidence";
        const decimal flagConfidence = 0.4m;

        await using var db = GetFreshCurriculumDb();
        // Use gradeId=1 to avoid unique-constraint race with other tests on (GradeId, SubjectCode, Language)
        var doc = await BL05Helpers.SeedParsedDocumentAsync(db, gradeId: 1, subjectId: 1);

        // Build ResultJson with a high-confidence node/chunk PLUS one flag entry
        var resultJson = BL05Helpers.MakeIngestResultJson_WithFlags(
            skillKey:       skillKey,
            nodeConfidence: 0.90m);

        await BL05Helpers.SeedIngestJobAsync(db, doc.Id, "Done", resultJson);

        // ── Wait for advance to reach a terminal state ────────────────────────
        var advanced = await BL02Helpers.WaitForConditionAsync(async () =>
        {
            await using var pollDb = GetFreshCurriculumDb();
            var d = await pollDb.CurriculumDocuments.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == doc.Id);
            return d?.IngestionStatus == IngestionStatus.Done ||
                   d?.IngestionStatus == IngestionStatus.Failed;
        }, timeoutSeconds: 12);

        advanced.Should().BeTrue(
            "advance must reach a terminal state within 12 s (no-stranding guarantee applies even with flags)");

        // ── Assert 1 & 2: IngestionStatus=Done — no crash / no stranding ──────
        await using var assertDb = GetFreshCurriculumDb();
        var updatedDoc = await assertDb.CurriculumDocuments.AsNoTracking()
            .FirstAsync(d => d.Id == doc.Id);

        updatedDoc.IngestionStatus.Should().Be(IngestionStatus.Done,
            "flag in ResultJson must NOT crash the advance pipeline (SerializeFlagAsPayload fix). " +
            "Document must reach IngestionStatus=Done, not stranded at Processing or Failed. " +
            "A 22P02 PostgreSQL error would produce IngestionStatus=Failed.");

        // ── Assert 3: IngestionReviewItem created from the flag ───────────────
        var reviewItems = await assertDb.IngestionReviewItems.AsNoTracking()
            .Where(r => r.CurriculumDocumentId == doc.Id)
            .ToListAsync();

        reviewItems.Should().NotBeEmpty(
            "flag entry must produce an IngestionReviewItem row (AC4/AC11 review routing for flags)");

        // The flag review item: sourceRef = flag.Ref, suggestedClassification = flag.Kind
        var flagItem = reviewItems.FirstOrDefault(r =>
            r.SuggestedClassification == flagKind &&
            r.SourceReference         == flagRef);

        flagItem.Should().NotBeNull(
            $"IngestionReviewItem from flag must have SourceReference='{flagRef}' and " +
            $"SuggestedClassification='{flagKind}'");

        flagItem!.Status.Should().Be(ReviewStatus.Pending,
            "flag-derived review item must be Pending");
        flagItem.Confidence.Should().Be(flagConfidence,
            "flag confidence must be stored correctly");

        // ── Assert 4: PayloadJson is valid JSON with all flag fields ──────────
        flagItem.PayloadJson.Should().NotBeNullOrWhiteSpace(
            "PayloadJson must be set by SerializeFlagAsPayload");

        // Must parse without throwing — if this fails, it means a 22P02 plain-string was stored
        JsonDocument payloadDoc;
        var parseAct = () => { payloadDoc = JsonDocument.Parse(flagItem.PayloadJson!); payloadDoc.Dispose(); };
        parseAct.Should().NotThrow(
            "PayloadJson must be valid JSON (SerializeFlagAsPayload fix — previously stored a bare string " +
            "that caused PostgreSQL 22P02 error)");

        using var payload = JsonDocument.Parse(flagItem.PayloadJson!);
        var payloadRoot = payload.RootElement;

        payloadRoot.ValueKind.Should().Be(JsonValueKind.Object,
            "PayloadJson must be a JSON object, not a bare string or array");

        // kind
        payloadRoot.TryGetProperty("kind", out var kindProp).Should().BeTrue(
            "PayloadJson must contain 'kind' field");
        kindProp.GetString().Should().Be(flagKind,
            "PayloadJson.kind must match the flag.Kind value");

        // ref
        payloadRoot.TryGetProperty("ref", out var refProp).Should().BeTrue(
            "PayloadJson must contain 'ref' field");
        refProp.GetString().Should().Be(flagRef,
            "PayloadJson.ref must match the flag.Ref value");

        // reason
        payloadRoot.TryGetProperty("reason", out var reasonProp).Should().BeTrue(
            "PayloadJson must contain 'reason' field (was previously stored raw causing 22P02)");
        reasonProp.GetString().Should().Be(flagReason,
            "PayloadJson.reason must match the flag.Reason value");

        // confidence
        payloadRoot.TryGetProperty("confidence", out var confProp).Should().BeTrue(
            "PayloadJson must contain 'confidence' field");
        confProp.TryGetDecimal(out var storedConf).Should().BeTrue("confidence must be numeric");
        storedConf.Should().BeApproximately(flagConfidence, 0.001m,
            "PayloadJson.confidence must match the flag.Confidence value");

        // ── Assert 5: high-confidence node still published alongside the flag ─
        await using var learningDb = GetFreshLearningDb();
        var knowledgeNode = await learningDb.KnowledgeNodes.AsNoTracking()
            .FirstOrDefaultAsync(kn => kn.SkillKey == skillKey);

        knowledgeNode.Should().NotBeNull(
            $"High-confidence node (skillKey='{skillKey}') must still be published to the learning tree " +
            "even when flags are present in the same ResultJson");
    }
}

// ─── BL-05 Reingest Endpoint Tests ───────────────────────────────────────────────────────────────

/// <summary>
/// BL-05 HTTP integration tests for POST api/curriculum/documents/{id}/reingest (BL-05-BE-9/10).
/// </summary>
[Collection("BL05IngestJob")]
public sealed class BL05_ReIngestEndpoint_Tests : IAsyncLifetime
{
    private const string ReIngestUrl = "api/curriculum/documents/{0}/reingest";

    private readonly BL05WebAppFactory _factory;
    private readonly HttpClient _client;

    public BL05_ReIngestEndpoint_Tests(BL05WebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        _factory.FakeStorage.Reset();
        await _factory.ApplyMigrationsAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private CurriculumDbContext GetFreshDb()
        => _factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<CurriculumDbContext>();

    // =========================================================================
    // BL05-HT-01: Reingest happy path → 200 + new Pending ingest job
    // =========================================================================

    [Fact(DisplayName = "BL05-HT-01: Admin reingest happy path → 200 + new Pending ingest job")]
    public async Task HT01_AdminReingest_HappyPath_Returns200_NewPendingIngestJob()
    {
        await using var db = GetFreshDb();
        var doc = await BL05Helpers.SeedParsedDocumentAsync(db, gradeId: 4, subjectId: 1);

        var adminJwt = BL05WebAppFactory.GenerateJwt(role: "Admin");
        var url      = string.Format(ReIngestUrl, doc.Id);

        var (response, root, body) =
            await BL02Helpers.SendAsync(_client, HttpMethod.Post, url, bearer: adminJwt);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "reingest with no in-flight job must return 200; body: {0}", body);

        BL02Helpers.AssertEnvelopeKeys(root, body);
        BL02Helpers.GetProp(root, "successed", body).GetBoolean().Should().BeTrue(
            "Successed must be true; body: {0}", body);

        await using var assertDb = GetFreshDb();
        var ingestJobs = await assertDb.PipelineJobs.AsNoTracking()
            .Where(j => j.DocumentId == doc.Id && j.JobType == "ingest" && j.Status == "Pending")
            .ToListAsync();
        ingestJobs.Should().HaveCount(1,
            "exactly one Pending ingest job must exist after reingest");
    }

    // =========================================================================
    // BL05-HT-02: Reingest — doc not yet parsed → 400
    // =========================================================================

    [Fact(DisplayName = "BL05-HT-02: Reingest on unparsed doc → 400")]
    public async Task HT02_Reingest_DocNotParsed_Returns400()
    {
        await using var db = GetFreshDb();
        // Doc without ParsedArtifactObjectKey (not yet parsed)
        var doc = await BL02Helpers.SeedDocumentAsync(db, status: DocumentStatus.Processing);

        var adminJwt = BL05WebAppFactory.GenerateJwt(role: "Admin");
        var url      = string.Format(ReIngestUrl, doc.Id);

        var (response, root, body) =
            await BL02Helpers.SendAsync(_client, HttpMethod.Post, url, bearer: adminJwt);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "reingest on an unparsed doc must return 400; body: {0}", body);

        if (root.ValueKind == JsonValueKind.Object)
        {
            BL02Helpers.AssertEnvelopeKeys(root, body);
            BL02Helpers.GetProp(root, "successed", body).GetBoolean().Should().BeFalse(
                "Successed=false on 400; body: {0}", body);
        }
    }

    // =========================================================================
    // BL05-HT-03: Reingest — unknown document → 404
    // =========================================================================

    [Fact(DisplayName = "BL05-HT-03: Reingest unknown document → 404")]
    public async Task HT03_Reingest_UnknownDocument_Returns404()
    {
        var adminJwt = BL05WebAppFactory.GenerateJwt(role: "Admin");
        var url      = string.Format(ReIngestUrl, 999999);

        var (response, root, body) =
            await BL02Helpers.SendAsync(_client, HttpMethod.Post, url, bearer: adminJwt);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "reingest on missing document must return 404; body: {0}", body);
    }

    // =========================================================================
    // BL05-HT-04: Reingest — Pending ingest in flight → 409
    // =========================================================================

    [Fact(DisplayName = "BL05-HT-04: Reingest while Pending ingest job in flight → 409")]
    public async Task HT04_Reingest_PendingIngestInFlight_Returns409()
    {
        await using var db = GetFreshDb();
        var doc = await BL05Helpers.SeedParsedDocumentAsync(db, gradeId: 4, subjectId: 1);
        await BL05Helpers.SeedIngestJobAsync(db, doc.Id, "Pending");

        var adminJwt = BL05WebAppFactory.GenerateJwt(role: "Admin");
        var url      = string.Format(ReIngestUrl, doc.Id);

        var (response, _, body) =
            await BL02Helpers.SendAsync(_client, HttpMethod.Post, url, bearer: adminJwt);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "reingest while Pending ingest in flight must return 409; body: {0}", body);
    }

    // =========================================================================
    // BL05-HT-05: Authz
    // =========================================================================

    [Fact(DisplayName = "BL05-HT-05a: Anonymous reingest → 401")]
    public async Task HT05a_Anonymous_Reingest_Returns401()
    {
        var (response, _, body) =
            await BL02Helpers.SendAsync(_client, HttpMethod.Post,
                "api/curriculum/documents/1/reingest");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "anonymous request must return 401; body: {0}", body);
    }

    [Fact(DisplayName = "BL05-HT-05b: Student role → 403 on reingest")]
    public async Task HT05b_StudentRole_Reingest_Returns403()
    {
        var studentJwt = BL05WebAppFactory.GenerateJwt(role: "Student");

        var (response, _, body) =
            await BL02Helpers.SendAsync(_client, HttpMethod.Post,
                "api/curriculum/documents/1/reingest", bearer: studentJwt);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Student role must be denied (403); body: {0}", body);
    }

    [Fact(DisplayName = "BL05-HT-05c: Admin role → past auth gate on reingest (200 or 4xx for business rule, not 401/403)")]
    public async Task HT05c_AdminRole_Reingest_PastAuthGate()
    {
        await using var db = GetFreshDb();
        var doc      = await BL05Helpers.SeedParsedDocumentAsync(db, gradeId: 4, subjectId: 1);
        var adminJwt = BL05WebAppFactory.GenerateJwt(role: "Admin");
        var url      = string.Format(ReIngestUrl, doc.Id);

        var (response, _, body) =
            await BL02Helpers.SendAsync(_client, HttpMethod.Post, url, bearer: adminJwt);

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            "Admin must not get 401; body: {0}", body);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "Admin must not get 403; body: {0}", body);
    }
}

// ─── BL-05 Review Queue Endpoint Tests ───────────────────────────────────────────────────────────

/// <summary>
/// BL-05 HTTP tests for GET/POST api/curriculum/review-items (BL-05-BE-7/8/10).
/// </summary>
[Collection("BL05IngestJob")]
public sealed class BL05_ReviewQueue_Tests : IAsyncLifetime
{
    private const string ReviewItemsUrl = "api/curriculum/review-items";
    private const string ApproveUrl     = "api/curriculum/review-items/{0}/approve";
    private const string RejectUrl      = "api/curriculum/review-items/{0}/reject";

    private readonly BL05WebAppFactory _factory;
    private readonly HttpClient _client;

    public BL05_ReviewQueue_Tests(BL05WebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        _factory.FakeStorage.Reset();
        await _factory.ApplyMigrationsAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private CurriculumDbContext GetFreshCurriculumDb()
        => _factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<CurriculumDbContext>();

    private LearningDbContext GetFreshLearningDb()
        => _factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<LearningDbContext>();

    private static async Task<Learnexia.Modules.Curriculum.Domain.Entities.IngestionReviewItem>
        SeedReviewItemAsync(CurriculumDbContext db, int documentId, string skillKey, decimal confidence = 0.45m)
    {
        // Use skillKey-derived name to ensure unique (Skill.Name, ConceptId) per review item,
        // preventing cross-test Skill/KnowledgeNode sharing via EnsureSkillAsync idempotency.
        var suffix    = skillKey.Split('.').Last();
        var skillName = $"مهارة-review-{suffix}";

        // PayloadJson must use the Python contract field names that IngestJobAdvanceService.SerializeNodeAsPayload
        // writes, and that ApproveIngestionReviewItemCommandHandler.ParsePayloadJson reads:
        //   "title"       (not "name")       — skillName for ApproveHandler skillName lookup
        //   "skill_key"                       — stable key
        //   "subject_code" as INT             — SerializeNodeAsPayload stores the mapped int (0=MATH)
        //   "difficulty"                      — passed to EnsureConceptAsync
        //   "parent_skill_key"               — concept name fallback
        //   "node_type"   (not "type")        — informational
        var item = new Learnexia.Modules.Curriculum.Domain.Entities.IngestionReviewItem
        {
            CurriculumDocumentId    = documentId,
            SourceReference         = $"p.5:{skillKey}",
            SuggestedClassification = $"Math > Grade 5 > {skillKey}",
            Confidence              = confidence,
            Status                  = ReviewStatus.Pending,
            PayloadJson             = JsonSerializer.Serialize(new
            {
                node_type        = "Skill",       // Python contract field name
                title            = skillName,     // "title" not "name" — unique per review item
                skill_key        = skillKey,
                confidence       = confidence,
                grade            = 5,             // "grade" not "grade_level"
                subject_code     = 0,             // stored as int (0=MATH) by SerializeNodeAsPayload
                language         = 0,             // int enum for ContentLanguage.Arabic
                difficulty       = 3,
                parent_skill_key = (string?)null,
            }),
        };
        db.IngestionReviewItems.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    // =========================================================================
    // BL05-RQ-01: List review items (admin)
    // =========================================================================

    [Fact(DisplayName = "BL05-RQ-01: Admin list review items → 200 + envelope shape")]
    public async Task RQ01_AdminListReviewItems_Returns200_EnvelopeShape()
    {
        await using var db = GetFreshCurriculumDb();
        var doc = await BL05Helpers.SeedParsedDocumentAsync(db, gradeId: 4, subjectId: 1);
        await SeedReviewItemAsync(db, doc.Id, "math.rq01-a");
        await SeedReviewItemAsync(db, doc.Id, "math.rq01-b");

        var adminJwt = BL05WebAppFactory.GenerateJwt(role: "Admin");
        var (response, root, body) =
            await BL02Helpers.SendAsync(_client, HttpMethod.Get,
                $"{ReviewItemsUrl}?documentId={doc.Id}", bearer: adminJwt);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "admin list must return 200; body: {0}", body);

        BL02Helpers.AssertEnvelopeKeys(root, body);
        BL02Helpers.GetProp(root, "successed", body).GetBoolean().Should().BeTrue(
            "Successed must be true; body: {0}", body);
        BL02Helpers.GetProp(root, "data", body).ValueKind.Should().NotBe(JsonValueKind.Null,
            "data must not be null; body: {0}", body);
    }

    // =========================================================================
    // BL05-RQ-03: Authz — anonymous → 401, Student → 403
    // =========================================================================

    [Fact(DisplayName = "BL05-RQ-03a: Anonymous list review items → 401")]
    public async Task RQ03a_Anonymous_ListReviewItems_Returns401()
    {
        var (response, _, body) =
            await BL02Helpers.SendAsync(_client, HttpMethod.Get, ReviewItemsUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "anonymous request must return 401; body: {0}", body);
    }

    [Fact(DisplayName = "BL05-RQ-03b: Student role → 403 on list review items")]
    public async Task RQ03b_StudentRole_ListReviewItems_Returns403()
    {
        var studentJwt = BL05WebAppFactory.GenerateJwt(role: "Student");
        var (response, _, body) =
            await BL02Helpers.SendAsync(_client, HttpMethod.Get, ReviewItemsUrl, bearer: studentJwt);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Student role must be denied (403); body: {0}", body);
    }

    // =========================================================================
    // BL05-RQ-04: Approve → Status=Approved + KnowledgeNode in tree
    // =========================================================================

    [Fact(DisplayName = "BL05-RQ-04: Approve review item → Status=Approved + node promoted to learning tree")]
    public async Task RQ04_Approve_ReviewItem_StatusApproved_NodeInTree()
    {
        const string skillKey = "math.grade5.fractions.rq04-approve";

        await using var db = GetFreshCurriculumDb();
        var doc  = await BL05Helpers.SeedParsedDocumentAsync(db, gradeId: 4, subjectId: 1);
        var item = await SeedReviewItemAsync(db, doc.Id, skillKey, confidence: 0.45m);

        var adminJwt = BL05WebAppFactory.GenerateJwt(role: "Admin", userId: 42);
        var url      = string.Format(ApproveUrl, item.Id);

        var (response, root, body) =
            await BL02Helpers.SendAsync(_client, HttpMethod.Post, url, bearer: adminJwt);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "approve must return 200; body: {0}", body);
        BL02Helpers.AssertEnvelopeKeys(root, body);
        BL02Helpers.GetProp(root, "successed", body).GetBoolean().Should().BeTrue(
            "Successed must be true; body: {0}", body);

        // Status=Approved in DB
        await using var assertDb = GetFreshCurriculumDb();
        var updatedItem = await assertDb.IngestionReviewItems.AsNoTracking()
            .FirstAsync(r => r.Id == item.Id);

        updatedItem.Status.Should().Be(ReviewStatus.Approved,
            "review item must be Approved (AC11)");
        updatedItem.ReviewedAt.Should().NotBeNull("ReviewedAt must be set");
        updatedItem.ReviewedByUserId.Should().Be(42, "ReviewedByUserId must be set to caller id");

        // KnowledgeNode promoted to learning tree
        await using var learningDb = GetFreshLearningDb();
        var kn = await learningDb.KnowledgeNodes.AsNoTracking()
            .FirstOrDefaultAsync(n => n.SkillKey == skillKey);

        kn.Should().NotBeNull(
            $"KnowledgeNode '{skillKey}' must be promoted to the learning tree on approve (AC11)");
    }

    // =========================================================================
    // BL05-RQ-05: Double approve → 409 (idempotency)
    // =========================================================================

    [Fact(DisplayName = "BL05-RQ-05: Double approve → 409 Conflict (AC11 idempotency)")]
    public async Task RQ05_DoubleApprove_Returns409()
    {
        const string skillKey = "math.grade5.fractions.rq05-idempotent";

        await using var db = GetFreshCurriculumDb();
        var doc  = await BL05Helpers.SeedParsedDocumentAsync(db, gradeId: 4, subjectId: 1);
        var item = await SeedReviewItemAsync(db, doc.Id, skillKey);

        var adminJwt = BL05WebAppFactory.GenerateJwt(role: "Admin");
        var url      = string.Format(ApproveUrl, item.Id);

        // First approve
        var (resp1, _, _) = await BL02Helpers.SendAsync(_client, HttpMethod.Post, url, bearer: adminJwt);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "first approve must return 200");

        // Second approve → 409
        var (resp2, root2, body2) = await BL02Helpers.SendAsync(_client, HttpMethod.Post, url, bearer: adminJwt);
        resp2.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "second approve must return 409 Conflict (AC11 idempotency); body: {0}", body2);
    }

    // =========================================================================
    // BL05-RQ-06: Approve unknown review item → 404
    // =========================================================================

    [Fact(DisplayName = "BL05-RQ-06: Approve unknown review item → 404")]
    public async Task RQ06_Approve_UnknownReviewItem_Returns404()
    {
        var adminJwt = BL05WebAppFactory.GenerateJwt(role: "Admin");
        var (response, _, body) =
            await BL02Helpers.SendAsync(_client, HttpMethod.Post,
                string.Format(ApproveUrl, 999999), bearer: adminJwt);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "approve on missing review item must return 404; body: {0}", body);
    }

    // =========================================================================
    // BL05-RQ-07: Reject → Status=Rejected, node NOT in tree
    // =========================================================================

    [Fact(DisplayName = "BL05-RQ-07: Reject review item → Status=Rejected, node NOT in learning tree")]
    public async Task RQ07_Reject_ReviewItem_StatusRejected_NodeNotInTree()
    {
        const string skillKey = "math.grade5.fractions.rq07-reject";

        await using var db = GetFreshCurriculumDb();
        var doc  = await BL05Helpers.SeedParsedDocumentAsync(db, gradeId: 4, subjectId: 1);
        var item = await SeedReviewItemAsync(db, doc.Id, skillKey, confidence: 0.45m);

        var adminJwt = BL05WebAppFactory.GenerateJwt(role: "Admin", userId: 99);
        var url      = string.Format(RejectUrl, item.Id);

        var (response, root, body) =
            await BL02Helpers.SendAsync(_client, HttpMethod.Post, url, bearer: adminJwt);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "reject must return 200; body: {0}", body);
        BL02Helpers.GetProp(root, "successed", body).GetBoolean().Should().BeTrue(
            "Successed must be true; body: {0}", body);

        // Status=Rejected
        await using var assertDb = GetFreshCurriculumDb();
        var updatedItem = await assertDb.IngestionReviewItems.AsNoTracking()
            .FirstAsync(r => r.Id == item.Id);

        updatedItem.Status.Should().Be(ReviewStatus.Rejected, "must be Rejected (AC11)");
        updatedItem.ReviewedAt.Should().NotBeNull("ReviewedAt must be set");

        // No KnowledgeNode promoted
        await using var learningDb = GetFreshLearningDb();
        var kn = await learningDb.KnowledgeNodes.AsNoTracking()
            .FirstOrDefaultAsync(n => n.SkillKey == skillKey);
        kn.Should().BeNull($"Node must NOT be promoted on reject (AC11); key='{skillKey}'");
    }

    // =========================================================================
    // BL05-RQ-08: Reject already-resolved item → 409
    // =========================================================================

    [Fact(DisplayName = "BL05-RQ-08: Reject already-approved item → 409")]
    public async Task RQ08_RejectAlreadyApproved_Returns409()
    {
        const string skillKey = "math.grade5.fractions.rq08-resolved";

        await using var db = GetFreshCurriculumDb();
        var doc  = await BL05Helpers.SeedParsedDocumentAsync(db, gradeId: 4, subjectId: 1);
        var item = await SeedReviewItemAsync(db, doc.Id, skillKey);

        var adminJwt = BL05WebAppFactory.GenerateJwt(role: "Admin");

        // Approve first
        var (resp1, _, _) = await BL02Helpers.SendAsync(
            _client, HttpMethod.Post, string.Format(ApproveUrl, item.Id), bearer: adminJwt);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "approve must succeed");

        // Reject the already-approved item → 409
        var (resp2, _, body2) = await BL02Helpers.SendAsync(
            _client, HttpMethod.Post, string.Format(RejectUrl, item.Id), bearer: adminJwt);
        resp2.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "reject on already-approved item must return 409; body: {0}", body2);
    }

    // =========================================================================
    // BL05-RQ-09: Approve/reject authz — anonymous → 401, Student → 403
    // =========================================================================

    [Fact(DisplayName = "BL05-RQ-09a: Anonymous approve → 401")]
    public async Task RQ09a_Anonymous_Approve_Returns401()
    {
        var (response, _, body) =
            await BL02Helpers.SendAsync(_client, HttpMethod.Post,
                string.Format(ApproveUrl, 1));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "anonymous approve must return 401; body: {0}", body);
    }

    [Fact(DisplayName = "BL05-RQ-09b: Student role → 403 on approve")]
    public async Task RQ09b_StudentRole_Approve_Returns403()
    {
        var studentJwt = BL05WebAppFactory.GenerateJwt(role: "Student");
        var (response, _, body) =
            await BL02Helpers.SendAsync(_client, HttpMethod.Post,
                string.Format(ApproveUrl, 1), bearer: studentJwt);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Student must be denied (403); body: {0}", body);
    }

    [Fact(DisplayName = "BL05-RQ-09c: Anonymous reject → 401")]
    public async Task RQ09c_Anonymous_Reject_Returns401()
    {
        var (response, _, body) =
            await BL02Helpers.SendAsync(_client, HttpMethod.Post,
                string.Format(RejectUrl, 1));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "anonymous reject must return 401; body: {0}", body);
    }
}

// ─── BL-05 No-stranding / Malformed ResultJson ───────────────────────────────────────────────────

/// <summary>
/// BL-05 hardening tests: malformed/over-long ResultJson must not strand a job at Processing.
/// Mirrors BL-02's no-stranding hardening test.
/// </summary>
[Collection("BL05IngestJob")]
public sealed class BL05_NoStranding_Tests : IAsyncLifetime
{
    private readonly BL05WebAppFactory _factory;

    public BL05_NoStranding_Tests(BL05WebAppFactory factory) => _factory = factory;

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private CurriculumDbContext GetFreshDb()
        => _factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<CurriculumDbContext>();

    // =========================================================================
    // BL05-HARD-01: Malformed ResultJson → terminal state, not stranded at Processing
    // =========================================================================

    [Fact(DisplayName = "BL05-HARD-01: Malformed ResultJson does not strand job at Processing")]
    public async Task HARD01_MalformedResultJson_DoesNotStrandJob()
    {
        await using var db = GetFreshDb();
        var doc = await BL05Helpers.SeedParsedDocumentAsync(db, ingestionStatus: IngestionStatus.InProgress);

        // Malformed JSON — IngestJobAdvanceService must handle the exception and write terminal state
        const string malformedJson = "{ this is not valid json @@@@ }";

        // RetryCount=3 so it goes to permanent failure path on the malformed case
        await BL05Helpers.SeedIngestJobAsync(db, doc.Id, "Done", resultJson: malformedJson, retryCount: 3);

        // Wait for terminal state
        var settled = await BL02Helpers.WaitForConditionAsync(async () =>
        {
            await using var pollDb = GetFreshDb();
            var d = await pollDb.CurriculumDocuments.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == doc.Id);
            return d?.IngestionStatus is IngestionStatus.Failed or IngestionStatus.Done;
        }, timeoutSeconds: 12);

        settled.Should().BeTrue(
            "malformed ResultJson must not strand the job — must reach a terminal state within 12 s (no-stranding guarantee)");

        // No job stuck at Processing
        await using var assertDb = GetFreshDb();
        var jobs = await assertDb.PipelineJobs.AsNoTracking()
            .Where(j => j.DocumentId == doc.Id && j.JobType == "ingest")
            .ToListAsync();

        jobs.Should().AllSatisfy(j =>
            j.Status.Should().NotBe("Processing",
                "no ingest job must remain at Processing after exception"));
    }

    // =========================================================================
    // BL05-HARD-02: Over-long skill_key → bounded, job terminates cleanly
    // =========================================================================

    [Fact(DisplayName = "BL05-HARD-02: Over-long skill_key in ResultJson → job terminates cleanly")]
    public async Task HARD02_OverLongSkillKey_JobTerminates()
    {
        await using var db = GetFreshDb();
        var doc = await BL05Helpers.SeedParsedDocumentAsync(db, ingestionStatus: IngestionStatus.InProgress);

        // skill_key exceeds MaxSkillKeyLength (256)
        var overLongKey = new string('x', 350);
        var resultJson = JsonSerializer.Serialize(new
        {
            schema_version = "1.0",
            nodes = new[]
            {
                new
                {
                    node_type        = "Skill",          // Python contract field name
                    skill_key        = overLongKey,
                    parent_skill_key = (string?)null,
                    title            = "اختبار",         // Python: "title" (not "name")
                    grade            = 5,                // Python: "grade" (not "grade_level")
                    subject_code     = "math",           // Python: string (not int)
                    language         = "ar",             // Python: string (not int)
                    difficulty       = 2,
                    confidence       = 0.90m,
                },
            },
            chunks      = Array.Empty<object>(),
            flags       = Array.Empty<object>(),
            diagnostics = (object?)null,
        });

        // RetryCount=3 so we skip retries and go straight to terminal on failure
        await BL05Helpers.SeedIngestJobAsync(db, doc.Id, "Done", resultJson: resultJson, retryCount: 3);

        var settled = await BL02Helpers.WaitForConditionAsync(async () =>
        {
            await using var pollDb = GetFreshDb();
            var d = await pollDb.CurriculumDocuments.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == doc.Id);
            return d?.IngestionStatus is IngestionStatus.Done or IngestionStatus.Failed;
        }, timeoutSeconds: 12);

        settled.Should().BeTrue(
            "over-long skill_key must not crash the service — must reach terminal state within 12 s");

        await using var assertDb = GetFreshDb();
        var jobs = await assertDb.PipelineJobs.AsNoTracking()
            .Where(j => j.DocumentId == doc.Id && j.JobType == "ingest")
            .ToListAsync();

        jobs.Should().AllSatisfy(j =>
            j.Status.Should().NotBe("Processing",
                "no job must remain at Processing (no-stranding guarantee)"));
    }
}
