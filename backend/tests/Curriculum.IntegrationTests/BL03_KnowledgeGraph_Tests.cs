using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AspNetCoreRateLimit;
using FluentAssertions;
using Learnexia.Modules.Curriculum.Api;
using Learnexia.Modules.Curriculum.Application.Abstractions;
using Learnexia.Modules.Curriculum.Domain.Entities;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Domain.Enums;
using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
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

// ─── xUnit collection — shares one factory across all BL-03 tests ────────────────────────────────

[CollectionDefinition("BL03KnowledgeGraph")]
public sealed class BL03KnowledgeGraphCollection : ICollectionFixture<BL03WebAppFactory> { }

// ─── BL-03 WebApplicationFactory ─────────────────────────────────────────────────────────────────

/// <summary>
/// Standalone WebApplicationFactory for BL-03 integration tests.
/// Mirrors BL05WebAppFactory configuration exactly, adding InferEdges poller keys.
/// </summary>
public sealed class BL03WebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string JwtSigningKey =
        "CHANGE_ME_super_secret_key_at_least_32_chars_long_0123456789";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg17")
        .WithDatabase("bl03_test")
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
                ["ConnectionStrings:Default"]                              = _postgres.GetConnectionString(),
                ["CurriculumUpload:MaxFileSizeBytes"]                     = "1048576",
                ["CurriculumUpload:BucketName"]                           = "curriculum-test",
                ["CurriculumPipeline:PollerIntervalSeconds"]              = "1",
                ["CurriculumPipeline:MaxRetries"]                         = "3",
                ["CurriculumPipeline:IngestPollerIntervalSeconds"]        = "1",
                ["CurriculumPipeline:IngestMaxRetries"]                   = "3",
                ["CurriculumPipeline:IngestionConfidenceThreshold"]       = "0.7",
                // Fast infer_edges poller so advance tests don't wait 5 s
                ["CurriculumPipeline:InferEdgesPollerIntervalSeconds"]    = "1",
                ["CurriculumPipeline:InferEdgesMaxRetries"]               = "3",
                // KnowledgeGraph query options
                ["KnowledgeGraph:RemediationMaxDepth"]                    = "3",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Override CurriculumDbContext to point at the test container
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
            services.AddScoped<ISessionManagementService, BL03AlwaysActiveSessionService>();

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
        // Also migrate Learning so KnowledgeNode/KnowledgeEdge tables exist
        var learningDb = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        await learningDb.Database.MigrateAsync();
        await SeedLearningReferenceDataAsync(learningDb);
    }

    /// <summary>
    /// Seeds Grade rows 1–6 (required by Subject's GradeId FK) — same pattern as BL05WebAppFactory.
    /// </summary>
    private static async Task SeedLearningReferenceDataAsync(LearningDbContext db)
    {
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

    // ── JWT factory ─────────────────────────────────────────────────────────────────────────────────
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

// ─── Session stub for BL-03 ──────────────────────────────────────────────────────────────────────
internal sealed class BL03AlwaysActiveSessionService : ISessionManagementService
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

// ─── BL-03 Seeding helpers ────────────────────────────────────────────────────────────────────────

internal static class BL03Helpers
{
    // ─────────────────────────────────────────────────────────────────────────────────────────────────
    // FROZEN ResultJson contract for EdgeInferenceAdvanceService (BL-05 divergence guard).
    // Field names: inference_model, edges[].source_skill_key, edges[].target_skill_key,
    //              edges[].relationship_type, edges[].strength, edges[].confidence
    // ─────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a ResultJson matching the FROZEN infer_edges contract:
    /// { "inference_model": "...", "edges": [ { "source_skill_key": "...", "target_skill_key": "...",
    ///   "relationship_type": "Prerequisite"|"Related", "strength": 0.0-1.0, "confidence": 0.0-1.0 } ] }
    /// </summary>
    public static string MakeInferResultJson(
        string inferenceModel,
        IEnumerable<(string sourceKey, string targetKey, string relType, double strength, double confidence)> edges)
    {
        return JsonSerializer.Serialize(new
        {
            inference_model = inferenceModel,
            edges = edges.Select(e => new
            {
                source_skill_key   = e.sourceKey,
                target_skill_key   = e.targetKey,
                relationship_type  = e.relType,
                strength           = e.strength,
                confidence         = e.confidence,
            }).ToArray(),
        });
    }

    /// <summary>
    /// Seeds an infer_edges PipelineJob with given status and result JSON.
    /// PipelineJob.DocumentId is nullable (migration 20260624084010_MakePipelineJobDocumentIdNullable),
    /// so infer_edges jobs do NOT require a CurriculumDocument. DocumentId is left null.
    /// </summary>
    public static async Task<PipelineJob> SeedInferJobAsync(
        CurriculumDbContext db,
        string jobStatus,
        string? resultJson   = null,
        string? payloadJson  = null,
        string? errorMessage = null,
        int retryCount       = 0)
    {
        var job = new PipelineJob
        {
            JobType      = "infer_edges",
            Status       = jobStatus,
            DocumentId   = null,   // nullable per 20260624084010_MakePipelineJobDocumentIdNullable
            PayloadJson  = payloadJson ?? JsonSerializer.Serialize(new
            {
                subject_code = "math",
                grade_id     = 4,
                nodes        = Array.Empty<object>(),
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

    /// <summary>
    /// Seeds a Subject (for Language checks) and a KnowledgeNode with the given SkillKey.
    /// Returns the KnowledgeNode.Id.
    /// </summary>
    public static async Task<int> SeedKnowledgeNodeAsync(
        LearningDbContext db,
        string skillKey,
        string name,
        int gradeId,
        Learnexia.Modules.Learning.Domain.Enums.ContentLanguage language = Learnexia.Modules.Learning.Domain.Enums.ContentLanguage.Ar,
        SubjectCode subjectCode  = SubjectCode.MATH)
    {
        // Upsert Subject for (gradeId, subjectCode, language)
        var subject = await db.Subjects
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.GradeId == gradeId
                                   && s.SubjectCode == subjectCode
                                   && s.Language == language);
        if (subject is null)
        {
            subject = new Subject
            {
                Name        = $"{subjectCode}-G{gradeId}-{language}",
                SubjectCode = subjectCode,
                Language    = language,
                GradeId     = gradeId,
                IsActive    = true,
                LifecycleState = LifecycleState.Draft,
            };
            db.Subjects.Add(subject);
            await db.SaveChangesAsync(0);
        }

        // Insert KnowledgeNode
        var node = new KnowledgeNode
        {
            Name      = name,
            NodeType  = KnowledgeNodeType.Skill,
            SubjectId = subject.Id,
            GradeId   = gradeId,
            Difficulty = 3,
            SkillKey  = skillKey,
        };
        db.KnowledgeNodes.Add(node);
        await db.SaveChangesAsync(0);
        return node.Id;
    }

    /// <summary>
    /// Seeds a KnowledgeEdge between two existing nodes.
    /// </summary>
    public static async Task<int> SeedKnowledgeEdgeAsync(
        LearningDbContext db,
        int sourceNodeId,
        int targetNodeId,
        EdgeRelationshipType relType = EdgeRelationshipType.Prerequisite,
        decimal strength             = 0.8m)
    {
        var edge = new KnowledgeEdge
        {
            SourceNodeId     = sourceNodeId,
            TargetNodeId     = targetNodeId,
            RelationshipType = relType,
            Strength         = strength,
        };
        db.KnowledgeEdges.Add(edge);
        await db.SaveChangesAsync(0);
        return edge.Id;
    }

    /// <summary>
    /// Seeds a pending KGSuggestion directly in curriculum DB.
    /// </summary>
    public static async Task<int> SeedKGSuggestionAsync(
        CurriculumDbContext db,
        int sourceNodeId,
        int targetNodeId,
        CurriculumRelationshipType relType = CurriculumRelationshipType.Prerequisite,
        decimal strength                   = 0.75m,
        KGSuggestionStatus status          = KGSuggestionStatus.Pending,
        string inferenceModel              = "test-model")
    {
        var suggestion = new KGSuggestion
        {
            SourceNodeId     = sourceNodeId,
            TargetNodeId     = targetNodeId,
            RelationshipType = relType,
            Strength         = strength,
            InferenceModel   = inferenceModel,
            Status           = status,
        };
        db.KGSuggestions.Add(suggestion);
        await db.SaveChangesAsync(0);
        return suggestion.Id;
    }
}

// ─── BL-03 Integration Tests ─────────────────────────────────────────────────────────────────────

/// <summary>
/// BL-03 integration tests for the knowledge graph story.
/// Tests cover:
///   1. infer→suggestion advance (EdgeInferenceAdvanceService advance)
///   2. Unresolved SkillKey fail-soft
///   3. Approve → publish (edge created, suggestion Approved)
///   4. Approve guards (cross-language, duplicate, cycle)
///   5. Reject (no edge, status=Rejected)
///   6. Decision-E invariant (only approve writes KnowledgeEdge)
///   7. Auth (anon→401, student→403, admin→200)
///   8. RelatedConcepts endpoint
///   9. RemediationPath endpoint (BFS, depth limit, cycle guard)
///  10. No-stranding: malformed ResultJson → PermanentlyFailed (not stuck at Processing)
///
/// CONTRACT VERIFICATION:
///   INPUT (PayloadJson) — .NET BuildKnowledgeGraphSuggestionsCommand writes:
///     { subject_code, grade_id, nodes: [ { node_id, skill_key, name, node_type, difficulty } ] }
///   Python `pipeline.py` reads: nodes[].skill_key, nodes[].title, nodes[].node_type,
///     nodes[].subject_code, nodes[].grade, nodes[].difficulty
///
///   MISMATCH DETECTED (DEFECT-BL03-1): .NET emits "name" but Python reads "title";
///   .NET emits "node_id" but Python does not read it (harmless extra).
///   Python expects "grade" (int) at node level but .NET does NOT emit a "grade" field
///   (only "grade_id" is at the top level, not per-node).
///   Python expects "subject_code" (string "math") at per-node level but .NET does NOT emit it per-node.
///
///   OUTPUT (ResultJson) — Python emits / .NET reads:
///     { "inference_model", "edges": [ { "source_skill_key", "target_skill_key",
///       "relationship_type", "strength", "confidence" } ] }
///   CONTRACT AGREEMENT: EXACT MATCH — both sides use these same snake_case field names.
/// </summary>
[Collection("BL03KnowledgeGraph")]
public sealed class BL03_KnowledgeGraph_Tests : IAsyncLifetime
{
    private readonly BL03WebAppFactory _factory;

    public BL03_KnowledgeGraph_Tests(BL03WebAppFactory factory) => _factory = factory;

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private CurriculumDbContext GetFreshCurriculumDb()
        => _factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<CurriculumDbContext>();

    private LearningDbContext GetFreshLearningDb()
        => _factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<LearningDbContext>();

    // =========================================================================
    // BL03-01: infer_edges Done → KGSuggestion{Pending}, NO KnowledgeEdge written (Decision E)
    // =========================================================================

    [Fact(DisplayName = "BL03-01: infer_edges Done → KGSuggestion{Pending} created, NO KnowledgeEdge written")]
    public async Task BL03_01_InferDoneJob_CreatesSuggestions_NoEdgeWritten()
    {
        const string srcKey = "math.grade4.division.bl03-01-src";
        const string tgtKey = "math.grade4.fractions.bl03-01-tgt";

        await using var learningDb = GetFreshLearningDb();
        var srcId = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, srcKey, "Division BL03-01", gradeId: 4);
        var tgtId = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, tgtKey, "Fractions BL03-01", gradeId: 4);

        // Edge count before
        var edgeCountBefore = await learningDb.KnowledgeEdges.CountAsync();

        // Seed a Done infer_edges job with 2 edges
        var resultJson = BL03Helpers.MakeInferResultJson(
            "lightrag-mock-v1",
            new[]
            {
                (srcKey, tgtKey, "Prerequisite", 0.82, 0.91),
                (tgtKey, srcKey, "Related",       0.55, 0.70),
            });

        await using var currDb = GetFreshCurriculumDb();
        await BL03Helpers.SeedInferJobAsync(currDb, "Done", resultJson);

        // Wait for EdgeInferenceAdvanceService to process (InferEdgesPollerIntervalSeconds=1)
        var advanced = await BL02Helpers.WaitForConditionAsync(async () =>
        {
            await using var db = GetFreshCurriculumDb();
            return await db.KGSuggestions
                .AnyAsync(s => s.SourceNodeId == srcId
                             && s.TargetNodeId == tgtId
                             && s.Status == KGSuggestionStatus.Pending);
        }, timeoutSeconds: 15);

        advanced.Should().BeTrue("EdgeInferenceAdvanceService must create KGSuggestion{Pending} within 15 s");

        // ── Assertions ────────────────────────────────────────────────────────
        await using var assertCurrDb = GetFreshCurriculumDb();
        var suggestions = await assertCurrDb.KGSuggestions
            .Where(s => (s.SourceNodeId == srcId || s.TargetNodeId == srcId)
                     || (s.SourceNodeId == tgtId || s.TargetNodeId == tgtId))
            .ToListAsync();

        suggestions.Should().HaveCount(2, "both edges must produce KGSuggestion rows (AC2)");
        suggestions.Should().AllSatisfy(s => s.Status.Should().Be(KGSuggestionStatus.Pending,
            "advance must write Pending suggestions, never Approved (Decision E AC6)"));

        // Strength clamp: 0.82 and 0.55 are within [0,1] — must be stored as-is.
        var prereqSug = suggestions.FirstOrDefault(s =>
            s.RelationshipType == CurriculumRelationshipType.Prerequisite);
        prereqSug.Should().NotBeNull();
        prereqSug!.Strength.Should().BeApproximately(0.82m, 0.001m, "strength must be stored from ResultJson");
        prereqSug.InferenceModel.Should().Be("lightrag-mock-v1", "inference_model must be stored");

        // Decision E invariant: NO KnowledgeEdge written by the advance service
        await using var assertLearnDb = GetFreshLearningDb();
        var edgeCountAfter = await assertLearnDb.KnowledgeEdges.CountAsync();
        edgeCountAfter.Should().Be(edgeCountBefore,
            "Decision E: advance service MUST NOT write KnowledgeEdge rows " +
            "(only admin approve is the publish path)");
    }

    // =========================================================================
    // BL03-02: Unresolved SkillKey → fail-soft drop, job still archives, no crash
    // =========================================================================

    [Fact(DisplayName = "BL03-02: Unresolved SkillKey in ResultJson → edge dropped, job archived, no crash")]
    public async Task BL03_02_UnresolvedSkillKey_FailSoft_NotStranded()
    {
        const string goodKey    = "math.grade4.division.bl03-02-good";
        const string missingKey = "math.grade4.nonexistent.bl03-02-gone";

        await using var learningDb = GetFreshLearningDb();
        var goodId = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, goodKey, "Good node BL03-02", gradeId: 4);
        // Note: missingKey is intentionally NOT seeded in KnowledgeNodes

        var resultJson = BL03Helpers.MakeInferResultJson(
            "lightrag-mock-v1",
            new[] { (goodKey, missingKey, "Prerequisite", 0.7, 0.8) });

        await using var currDb = GetFreshCurriculumDb();
        var job = await BL03Helpers.SeedInferJobAsync(currDb, "Done", resultJson);

        // Wait for the job to be archived (success path — fail-soft drop, not crash)
        var archived = await BL02Helpers.WaitForConditionAsync(async () =>
        {
            await using var db = GetFreshCurriculumDb();
            var j = await db.PipelineJobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == job.Id);
            return j?.Status == "Archived" || j?.Status == "PermanentlyFailed";
        }, timeoutSeconds: 15);

        archived.Should().BeTrue("job must reach a terminal state within 15 s even with an unresolved SkillKey");

        // The job must be Archived (not PermanentlyFailed — unresolved SkillKey is fail-soft, not fatal)
        await using var assertDb = GetFreshCurriculumDb();
        var finalJob = await assertDb.PipelineJobs.AsNoTracking()
            .FirstAsync(j => j.Id == job.Id);
        finalJob.Status.Should().Be("Archived",
            "unresolved SkillKey is a soft drop (logged + skipped), not a job-level failure");

        // No suggestion written for the dropped edge
        var suggestionForGone = await assertDb.KGSuggestions
            .AnyAsync(s => s.SourceNodeId == goodId);
        suggestionForGone.Should().BeFalse(
            "dropped edge (unresolved target SkillKey) must NOT produce a KGSuggestion row");
    }

    // =========================================================================
    // BL03-03: Approve Pending suggestion → KnowledgeEdge created, suggestion Approved
    // =========================================================================

    [Fact(DisplayName = "BL03-03: Approve Pending suggestion → KnowledgeEdge created, suggestion Approved")]
    public async Task BL03_03_ApproveSuggestion_PublishesEdge_StampsApproved()
    {
        const string srcKey = "math.grade4.division.bl03-03-src";
        const string tgtKey = "math.grade4.fractions.bl03-03-tgt";

        await using var learningDb = GetFreshLearningDb();
        var srcId = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, srcKey, "Division BL03-03", gradeId: 4);
        var tgtId = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, tgtKey, "Fractions BL03-03", gradeId: 4);

        await using var currDb = GetFreshCurriculumDb();
        var suggId = await BL03Helpers.SeedKGSuggestionAsync(currDb, srcId, tgtId,
            CurriculumRelationshipType.Prerequisite, strength: 0.80m);

        var edgeCountBefore = await learningDb.KnowledgeEdges.CountAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                BL03WebAppFactory.GenerateJwt(role: "Admin", userId: 99));

        var response = await client.PostAsync(
            $"/api/curriculum/kg-suggestions/{suggId}/approve",
            JsonContent.Create<string?>(null));

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "approving a valid acyclic suggestion must succeed with 200");

        var body    = await response.Content.ReadAsStringAsync();
        var root    = JsonDocument.Parse(body).RootElement;
        AssertEnvelope(root, body);
        root.GetProperty("successed").GetBoolean().Should().BeTrue("successed must be true on approve");

        // ── Edge must now exist in learning ──────────────────────────────────
        await using var assertLearnDb = GetFreshLearningDb();
        var edgeCountAfter = await assertLearnDb.KnowledgeEdges.CountAsync();
        edgeCountAfter.Should().Be(edgeCountBefore + 1,
            "exactly one KnowledgeEdge must be published on approve (AC2)");

        var edge = await assertLearnDb.KnowledgeEdges
            .FirstOrDefaultAsync(e => e.SourceNodeId == srcId && e.TargetNodeId == tgtId
                                   && e.RelationshipType == EdgeRelationshipType.Prerequisite);
        edge.Should().NotBeNull("the published KnowledgeEdge must match the suggestion's source/target/type");
        edge!.Strength.Should().BeApproximately(0.80m, 0.001m, "strength must be copied from suggestion to edge");

        // ── Suggestion must be stamped Approved ───────────────────────────────
        await using var assertCurrDb = GetFreshCurriculumDb();
        var updatedSugg = await assertCurrDb.KGSuggestions.FirstAsync(s => s.Id == suggId);
        updatedSugg.Status.Should().Be(KGSuggestionStatus.Approved,
            "suggestion must be stamped Approved after successful publish");
        updatedSugg.ReviewedByUserId.Should().Be(99,
            "ReviewedByUserId must be set to the approver's userId");
        updatedSugg.ReviewedAt.Should().NotBeNull("ReviewedAt must be set on approve");
    }

    // =========================================================================
    // BL03-04a: Approve cycle-inducing suggestion → 422 + suggestion stays Pending + no edge
    // =========================================================================

    [Fact(DisplayName = "BL03-04a: Approve cycle-inducing suggestion → 422 + suggestion stays Pending + no edge")]
    public async Task BL03_04a_ApproveCycle_Returns422_SuggestionStaysPending()
    {
        const string nodeAKey = "math.grade4.div.bl03-04a-A";
        const string nodeBKey = "math.grade4.div.bl03-04a-B";

        await using var learningDb = GetFreshLearningDb();
        var nodeAId = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, nodeAKey, "Node A 04a", gradeId: 4);
        var nodeBId = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, nodeBKey, "Node B 04a", gradeId: 4);

        // Create an existing edge A→B
        await BL03Helpers.SeedKnowledgeEdgeAsync(learningDb, nodeAId, nodeBId,
            EdgeRelationshipType.Prerequisite, 0.9m);

        // Seed a suggestion for B→A (which would close a cycle)
        await using var currDb = GetFreshCurriculumDb();
        var suggId = await BL03Helpers.SeedKGSuggestionAsync(currDb, nodeBId, nodeAId,
            CurriculumRelationshipType.Prerequisite, 0.7m);

        var edgeCountBefore = await learningDb.KnowledgeEdges.CountAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                BL03WebAppFactory.GenerateJwt(role: "Admin", userId: 99));

        var response = await client.PostAsync(
            $"/api/curriculum/kg-suggestions/{suggId}/approve",
            JsonContent.Create<string?>(null));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "cycle-inducing edge must return 422 Unprocessable (AC6 guard)");

        // No new edge
        await using var assertLearnDb = GetFreshLearningDb();
        var edgeCountAfter = await assertLearnDb.KnowledgeEdges.CountAsync();
        edgeCountAfter.Should().Be(edgeCountBefore,
            "no KnowledgeEdge must be written when cycle is detected (Decision E, atomicity)");

        // Suggestion stays Pending
        await using var assertCurrDb = GetFreshCurriculumDb();
        var sug = await assertCurrDb.KGSuggestions.FirstAsync(s => s.Id == suggId);
        sug.Status.Should().Be(KGSuggestionStatus.Pending,
            "cycle rejection must leave suggestion Pending — rollback must have happened");
    }

    // =========================================================================
    // BL03-04b: Approve duplicate edge → 200 success (idempotent), suggestion stamped Approved
    // =========================================================================

    [Fact(DisplayName = "BL03-04b: Approve duplicate edge → 200 (idempotent), suggestion Approved")]
    public async Task BL03_04b_ApproveDuplicate_Returns200_SuggestionApproved()
    {
        const string srcKey = "math.grade4.div.bl03-04b-src";
        const string tgtKey = "math.grade4.frac.bl03-04b-tgt";

        await using var learningDb = GetFreshLearningDb();
        var srcId = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, srcKey, "Div 04b", gradeId: 4);
        var tgtId = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, tgtKey, "Frac 04b", gradeId: 4);

        // Pre-existing edge (hand-authored or prior approval)
        await BL03Helpers.SeedKnowledgeEdgeAsync(learningDb, srcId, tgtId,
            EdgeRelationshipType.Prerequisite, 1.0m);

        // Suggestion for the same pair
        await using var currDb = GetFreshCurriculumDb();
        var suggId = await BL03Helpers.SeedKGSuggestionAsync(currDb, srcId, tgtId,
            CurriculumRelationshipType.Prerequisite, 0.70m);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                BL03WebAppFactory.GenerateJwt(role: "Admin", userId: 99));

        var response = await client.PostAsync(
            $"/api/curriculum/kg-suggestions/{suggId}/approve",
            JsonContent.Create<string?>(null));

        // Duplicate edge is treated as a no-op success (200 OK) per the plan
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "duplicate edge must be treated as success (insert-or-ignore semantics, 200 OK)");

        // Suggestion must be Approved (no-op is still an approval)
        await using var assertCurrDb = GetFreshCurriculumDb();
        var sug = await assertCurrDb.KGSuggestions.FirstAsync(s => s.Id == suggId);
        sug.Status.Should().Be(KGSuggestionStatus.Approved,
            "suggestion must be stamped Approved even on duplicate (idempotent path)");
    }

    // =========================================================================
    // BL03-04c: Approve cross-language suggestion → 422 + suggestion stays Pending + no edge
    // =========================================================================

    [Fact(DisplayName = "BL03-04c: Approve cross-language suggestion → 422 + suggestion stays Pending + no edge")]
    public async Task BL03_04c_ApproveCrossLanguage_Returns422_SuggestionStaysPending()
    {
        // Arabic-language node (Language=Ar)
        await using var learningDb = GetFreshLearningDb();
        var arId = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb,
            "arabic.grade4.reading.bl03-04c-ar", "قراءة", gradeId: 4,
            language: Learnexia.Modules.Learning.Domain.Enums.ContentLanguage.Ar,
            subjectCode: SubjectCode.ARABIC);

        // English-language node (Language=En)
        var enId = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb,
            "english.grade4.reading.bl03-04c-en", "Reading", gradeId: 4,
            language: Learnexia.Modules.Learning.Domain.Enums.ContentLanguage.En,
            subjectCode: SubjectCode.ENGLISH);

        var edgeCountBefore = await learningDb.KnowledgeEdges.CountAsync();

        // Seed a cross-language suggestion
        await using var currDb = GetFreshCurriculumDb();
        var suggId = await BL03Helpers.SeedKGSuggestionAsync(currDb, arId, enId,
            CurriculumRelationshipType.Prerequisite, 0.6m);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                BL03WebAppFactory.GenerateJwt(role: "Admin", userId: 99));

        var response = await client.PostAsync(
            $"/api/curriculum/kg-suggestions/{suggId}/approve",
            JsonContent.Create<string?>(null));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "cross-language edge must be rejected with 422 (AC6)");

        // No new edge
        await using var assertLearnDb = GetFreshLearningDb();
        var edgeCountAfter = await assertLearnDb.KnowledgeEdges.CountAsync();
        edgeCountAfter.Should().Be(edgeCountBefore, "no edge must be written on cross-language rejection");

        // Suggestion stays Pending
        await using var assertCurrDb = GetFreshCurriculumDb();
        var sug = await assertCurrDb.KGSuggestions.FirstAsync(s => s.Id == suggId);
        sug.Status.Should().Be(KGSuggestionStatus.Pending,
            "cross-language rejection must leave suggestion Pending");
    }

    // =========================================================================
    // BL03-05: Reject suggestion → Status=Rejected, no edge written
    // =========================================================================

    [Fact(DisplayName = "BL03-05: Reject Pending suggestion → Status=Rejected, no KnowledgeEdge written")]
    public async Task BL03_05_RejectSuggestion_StampsRejected_NoEdge()
    {
        const string srcKey = "math.grade4.div.bl03-05-src";
        const string tgtKey = "math.grade4.frac.bl03-05-tgt";

        await using var learningDb = GetFreshLearningDb();
        var srcId = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, srcKey, "Div 05", gradeId: 4);
        var tgtId = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, tgtKey, "Frac 05", gradeId: 4);

        await using var currDb = GetFreshCurriculumDb();
        var suggId = await BL03Helpers.SeedKGSuggestionAsync(currDb, srcId, tgtId,
            CurriculumRelationshipType.Prerequisite, 0.65m);

        var edgeCountBefore = await learningDb.KnowledgeEdges.CountAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                BL03WebAppFactory.GenerateJwt(role: "Admin", userId: 99));

        var response = await client.PostAsync(
            $"/api/curriculum/kg-suggestions/{suggId}/reject",
            JsonContent.Create<string?>(null));

        response.StatusCode.Should().Be(HttpStatusCode.OK, "reject must return 200 OK");

        var body = await response.Content.ReadAsStringAsync();
        var root = JsonDocument.Parse(body).RootElement;
        AssertEnvelope(root, body);
        root.GetProperty("successed").GetBoolean().Should().BeTrue();

        // Suggestion must be Rejected
        await using var assertCurrDb = GetFreshCurriculumDb();
        var sug = await assertCurrDb.KGSuggestions.FirstAsync(s => s.Id == suggId);
        sug.Status.Should().Be(KGSuggestionStatus.Rejected,
            "reject must stamp Status=Rejected");
        sug.ReviewedByUserId.Should().Be(99, "ReviewedByUserId must be set on reject");
        sug.ReviewedAt.Should().NotBeNull("ReviewedAt must be set on reject");

        // No edge written
        await using var assertLearnDb = GetFreshLearningDb();
        var edgeCountAfter = await assertLearnDb.KnowledgeEdges.CountAsync();
        edgeCountAfter.Should().Be(edgeCountBefore,
            "Decision E: reject must NEVER write KnowledgeEdge (AC6)");
    }

    // =========================================================================
    // BL03-06: Decision-E invariant — re-approve/re-reject already-resolved → 409
    // =========================================================================

    [Fact(DisplayName = "BL03-06: Re-approve already-Approved suggestion → 409 Conflict")]
    public async Task BL03_06_ReApproveResolved_Returns409()
    {
        const string srcKey = "math.grade4.div.bl03-06-src";
        const string tgtKey = "math.grade4.frac.bl03-06-tgt";

        await using var learningDb = GetFreshLearningDb();
        var srcId = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, srcKey, "Div 06", gradeId: 4);
        var tgtId = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, tgtKey, "Frac 06", gradeId: 4);

        // Seed already-Approved suggestion (simulates prior approve)
        await using var currDb = GetFreshCurriculumDb();
        var suggId = await BL03Helpers.SeedKGSuggestionAsync(currDb, srcId, tgtId,
            CurriculumRelationshipType.Prerequisite, 0.7m, KGSuggestionStatus.Approved);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                BL03WebAppFactory.GenerateJwt(role: "Admin", userId: 99));

        var response = await client.PostAsync(
            $"/api/curriculum/kg-suggestions/{suggId}/approve",
            JsonContent.Create<string?>(null));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "re-approving an already-resolved suggestion must return 409 Conflict");
    }

    // =========================================================================
    // BL03-07a: Auth — KGSuggestions endpoints require Admin, anon→401, student→403
    // =========================================================================

    [Fact(DisplayName = "BL03-07a: KGSuggestions list — anon→401, student→403, admin→200")]
    public async Task BL03_07a_KGSuggestions_List_Authz()
    {
        var client = _factory.CreateClient();

        // Anon → 401
        var anonResponse = await client.GetAsync("/api/curriculum/kg-suggestions");
        anonResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "unauthenticated request to admin endpoint must return 401");

        // Student → 403
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                BL03WebAppFactory.GenerateJwt(role: "Student", userId: 10));
        var studentResponse = await client.GetAsync("/api/curriculum/kg-suggestions");
        studentResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Student role must not access admin-only endpoint (403)");

        // Admin → 200
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                BL03WebAppFactory.GenerateJwt(role: "Admin", userId: 99));
        var adminResponse = await client.GetAsync("/api/curriculum/kg-suggestions");
        adminResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "Admin role must access the list endpoint successfully (200)");

        var body = await adminResponse.Content.ReadAsStringAsync();
        var root = JsonDocument.Parse(body).RootElement;
        AssertEnvelope(root, body);
    }

    // =========================================================================
    // BL03-07b: Auth — approve/reject require Admin
    // =========================================================================

    [Fact(DisplayName = "BL03-07b: approve/reject — anon→401, student→403")]
    public async Task BL03_07b_ApproveReject_RequireAdmin()
    {
        var client = _factory.CreateClient();

        // Anon approve → 401
        var anonApp = await client.PostAsync(
            "/api/curriculum/kg-suggestions/999/approve",
            JsonContent.Create<string?>(null));
        anonApp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "anon approve must return 401");

        // Anon reject → 401
        var anonRej = await client.PostAsync(
            "/api/curriculum/kg-suggestions/999/reject",
            JsonContent.Create<string?>(null));
        anonRej.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "anon reject must return 401");

        // Student approve → 403
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                BL03WebAppFactory.GenerateJwt(role: "Student", userId: 10));
        var studApp = await client.PostAsync(
            "/api/curriculum/kg-suggestions/999/approve",
            JsonContent.Create<string?>(null));
        studApp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "student approve must return 403");

        // Student reject → 403
        var studRej = await client.PostAsync(
            "/api/curriculum/kg-suggestions/999/reject",
            JsonContent.Create<string?>(null));
        studRej.StatusCode.Should().Be(HttpStatusCode.Forbidden, "student reject must return 403");
    }

    // =========================================================================
    // BL03-07c: Auth — RelatedConcepts/RemediationPath allow authenticated students, reject anon
    // =========================================================================

    [Fact(DisplayName = "BL03-07c: RelatedConcepts/RemediationPath — anon→401, student→200")]
    public async Task BL03_07c_StudentQueryEndpoints_Authz()
    {
        const string nodeKey = "math.grade4.frac.bl03-07c";

        await using var learningDb = GetFreshLearningDb();
        var nodeId = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, nodeKey, "Frac 07c", gradeId: 4);

        var client = _factory.CreateClient();

        // Anon → 401 for RelatedConcepts
        var anonRelated = await client.GetAsync($"/api/Learning/KnowledgeGraph/RelatedConcepts/{nodeId}");
        anonRelated.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "unauthenticated request to RelatedConcepts must return 401");

        // Anon → 401 for RemediationPath
        var anonRemediation = await client.GetAsync($"/api/Learning/KnowledgeGraph/RemediationPath/{nodeId}");
        anonRemediation.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "unauthenticated request to RemediationPath must return 401");

        // Student → 200 (student-facing endpoints — any auth role allowed)
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                BL03WebAppFactory.GenerateJwt(role: "Student", userId: 10));

        var studentRelated = await client.GetAsync($"/api/Learning/KnowledgeGraph/RelatedConcepts/{nodeId}");
        studentRelated.StatusCode.Should().Be(HttpStatusCode.OK,
            "Student must be able to access RelatedConcepts (student-facing, AC3)");

        var studentRemediation = await client.GetAsync($"/api/Learning/KnowledgeGraph/RemediationPath/{nodeId}");
        studentRemediation.StatusCode.Should().Be(HttpStatusCode.OK,
            "Student must be able to access RemediationPath (student-facing, AC4)");
    }

    // =========================================================================
    // BL03-08: RelatedConcepts — returns related nodes (both directions, distinct)
    // =========================================================================

    [Fact(DisplayName = "BL03-08: RelatedConcepts returns related nodes both directions")]
    public async Task BL03_08_RelatedConcepts_ReturnsBothDirections()
    {
        const string centerKey = "math.grade4.frac.bl03-08-center";
        const string relAKey   = "math.grade4.frac.bl03-08-relA";
        const string relBKey   = "math.grade4.frac.bl03-08-relB";

        await using var learningDb = GetFreshLearningDb();
        var centerId = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, centerKey, "Center 08", gradeId: 4);
        var relAId   = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, relAKey,   "RelA 08",   gradeId: 4);
        var relBId   = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, relBKey,   "RelB 08",   gradeId: 4);

        // center→relA (Related), relB→center (Related, reversed direction)
        await BL03Helpers.SeedKnowledgeEdgeAsync(learningDb, centerId, relAId, EdgeRelationshipType.Related, 0.5m);
        await BL03Helpers.SeedKnowledgeEdgeAsync(learningDb, relBId,   centerId, EdgeRelationshipType.Related, 0.6m);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                BL03WebAppFactory.GenerateJwt(role: "Student", userId: 10));

        var response = await client.GetAsync($"/api/Learning/KnowledgeGraph/RelatedConcepts/{centerId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK, "RelatedConcepts must return 200 for existing node");

        var body = await response.Content.ReadAsStringAsync();
        var root = JsonDocument.Parse(body).RootElement;
        AssertEnvelope(root, body);
        root.GetProperty("successed").GetBoolean().Should().BeTrue();

        // The "data" array should contain both related nodes
        var data = root.GetProperty("data");
        data.ValueKind.Should().Be(JsonValueKind.Array,
            "data must be an array of related nodes");

        // StudentKnowledgeNodeDto serializes as camelCase: Id → "id"
        var nodeIds = data.EnumerateArray()
            .Select(n => n.GetProperty("id").GetInt32())
            .ToList();

        nodeIds.Should().Contain(relAId,
            "relA connected via forward Related edge must appear in results (AC3)");
        nodeIds.Should().Contain(relBId,
            "relB connected via reverse Related edge must appear in results (AC3 — both directions)");
        nodeIds.Distinct().Should().HaveSameCount(nodeIds,
            "returned node ids must be distinct");
    }

    // =========================================================================
    // BL03-09a: RemediationPath — returns upstream prerequisites, respects max depth
    // =========================================================================

    [Fact(DisplayName = "BL03-09a: RemediationPath returns transitive prereqs up to max depth (default 3)")]
    public async Task BL03_09a_RemediationPath_TransitiveBFS_RespectsDepth()
    {
        // Chain: D(depth1) → C(depth0, starting node) and A(depth2) → B(depth1) → ...
        // We create: A→B→C where C is the starting node → result: B at depth1, A at depth2
        const string nodeAKey = "math.grade4.div.bl03-09a-A";
        const string nodeBKey = "math.grade4.div.bl03-09a-B";
        const string nodeCKey = "math.grade4.div.bl03-09a-C";
        // Also seed D at depth 4 (beyond default RemediationMaxDepth=3) — must be excluded
        const string nodeDKey = "math.grade4.div.bl03-09a-D";

        await using var learningDb = GetFreshLearningDb();
        var nodeAId = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, nodeAKey, "A 09a", gradeId: 4);
        var nodeBId = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, nodeBKey, "B 09a", gradeId: 4);
        var nodeCId = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, nodeCKey, "C 09a", gradeId: 4);
        var nodeDId = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, nodeDKey, "D 09a", gradeId: 4);

        // B is prereq of C (depth1 from C), A is prereq of B (depth2 from C), D is prereq of A (depth3 from C — AT limit)
        await BL03Helpers.SeedKnowledgeEdgeAsync(learningDb, nodeBId, nodeCId, EdgeRelationshipType.Prerequisite, 0.9m);
        await BL03Helpers.SeedKnowledgeEdgeAsync(learningDb, nodeAId, nodeBId, EdgeRelationshipType.Prerequisite, 0.8m);
        await BL03Helpers.SeedKnowledgeEdgeAsync(learningDb, nodeDId, nodeAId, EdgeRelationshipType.Prerequisite, 0.7m);
        // Depth4 node — beyond max=3, must be excluded
        const string nodeEKey = "math.grade4.div.bl03-09a-E";
        var nodeEId = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, nodeEKey, "E 09a", gradeId: 4);
        await BL03Helpers.SeedKnowledgeEdgeAsync(learningDb, nodeEId, nodeDId, EdgeRelationshipType.Prerequisite, 0.6m);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                BL03WebAppFactory.GenerateJwt(role: "Student", userId: 10));

        var response = await client.GetAsync($"/api/Learning/KnowledgeGraph/RemediationPath/{nodeCId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK, "RemediationPath must return 200");

        var body = await response.Content.ReadAsStringAsync();
        var root = JsonDocument.Parse(body).RootElement;
        AssertEnvelope(root, body);
        root.GetProperty("successed").GetBoolean().Should().BeTrue();

        var data = root.GetProperty("data");
        data.ValueKind.Should().Be(JsonValueKind.Array);

        var resultNodeIds = data.EnumerateArray()
            .Select(n => n.GetProperty("nodeId").GetInt32())
            .ToHashSet();

        resultNodeIds.Should().Contain(nodeBId, "B (depth1) must be in remediation path (AC4)");
        resultNodeIds.Should().Contain(nodeAId, "A (depth2) must be in remediation path (AC4)");
        resultNodeIds.Should().Contain(nodeDId, "D (depth3, AT the limit) must be in remediation path");
        resultNodeIds.Should().NotContain(nodeEId,
            "E (depth4, BEYOND RemediationMaxDepth=3) must be excluded (depth-bounded AC4)");

        // Verify depth ordering
        var entries = data.EnumerateArray()
            .Select(n => new { nodeId = n.GetProperty("nodeId").GetInt32(), depth = n.GetProperty("depth").GetInt32() })
            .ToList();

        var bEntry = entries.FirstOrDefault(e => e.nodeId == nodeBId);
        bEntry.Should().NotBeNull();
        bEntry!.depth.Should().Be(1, "B is at depth 1 from C");

        var aEntry = entries.FirstOrDefault(e => e.nodeId == nodeAId);
        aEntry.Should().NotBeNull();
        aEntry!.depth.Should().Be(2, "A is at depth 2 from C");
    }

    // =========================================================================
    // BL03-09b: RemediationPath — cycle in graph does NOT infinite-loop (cycle guard)
    // =========================================================================

    [Fact(DisplayName = "BL03-09b: RemediationPath cycle guard — cyclic edge structure does not infinite-loop")]
    public async Task BL03_09b_RemediationPath_CycleInGraph_TerminatesFinitely()
    {
        // We deliberately bypass the acyclic insert guard by directly seeding both directions
        // to test the BFS cycle guard in the query handler.
        const string nodeXKey = "math.grade4.div.bl03-09b-X";
        const string nodeYKey = "math.grade4.div.bl03-09b-Y";

        await using var learningDb = GetFreshLearningDb();
        var nodeXId = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, nodeXKey, "X 09b", gradeId: 4);
        var nodeYId = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, nodeYKey, "Y 09b", gradeId: 4);

        // Insert X→Y then Y→X directly (bypassing the guard — raw DB)
        await learningDb.Database.ExecuteSqlRawAsync(
            $"""
            INSERT INTO learning."KnowledgeEdges" ("SourceNodeId","TargetNodeId","RelationshipType","Strength","CreatedBy","CreatedAt","IsDeleted")
            VALUES ({nodeXId},{nodeYId},0,0.8,0,NOW(),false),
                   ({nodeYId},{nodeXId},0,0.7,0,NOW(),false)
            ON CONFLICT DO NOTHING;
            """);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                BL03WebAppFactory.GenerateJwt(role: "Student", userId: 10));

        // Must return within a reasonable time (not hang/infinite-loop)
        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
        var response = await client.GetAsync(
            $"/api/Learning/KnowledgeGraph/RemediationPath/{nodeYId}", cts.Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "RemediationPath must terminate and return 200 even with a cycle in the graph (cycle guard, AC4)");

        var body = await response.Content.ReadAsStringAsync();
        var root = JsonDocument.Parse(body).RootElement;
        root.GetProperty("successed").GetBoolean().Should().BeTrue(
            "cycle guard must allow the query to complete without infinite-loop");
    }

    // =========================================================================
    // BL03-10: No-stranding — malformed ResultJson → job PermanentlyFailed, not stuck at Processing
    // =========================================================================

    [Fact(DisplayName = "BL03-10: Malformed infer ResultJson → PermanentlyFailed (no-stranding guarantee)")]
    public async Task BL03_10_MalformedResultJson_JobPermanentlyFailed_NotStranded()
    {
        var malformedJson = "{ this is not valid JSON @@@@ }";

        await using var currDb = GetFreshCurriculumDb();
        var job = await BL03Helpers.SeedInferJobAsync(currDb, "Done", resultJson: malformedJson);

        // Wait for the service to process it
        var reached = await BL02Helpers.WaitForConditionAsync(async () =>
        {
            await using var db = GetFreshCurriculumDb();
            var j = await db.PipelineJobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == job.Id);
            // Either PermanentlyFailed (bad JSON is non-retryable) or Archived (service handled + dropped)
            return j?.Status == "PermanentlyFailed" || j?.Status == "Archived";
        }, timeoutSeconds: 15);

        reached.Should().BeTrue(
            "malformed ResultJson must NOT strand the job at 'Processing'; it must reach PermanentlyFailed or Archived");

        await using var assertDb = GetFreshCurriculumDb();
        var finalJob = await assertDb.PipelineJobs.AsNoTracking()
            .FirstAsync(j => j.Id == job.Id);
        finalJob.Status.Should().NotBe("Processing",
            "the job must not remain stuck at 'Processing' after malformed JSON (no-stranding guarantee)");
    }

    // =========================================================================
    // CONTRACT: /build endpoint happy path + INPUT PayloadJson contract agreement
    // (DEFECTS BL03-1..4 all fixed: title, node_type string, per-node subject_code,
    //  per-node grade, DocumentId nullable)
    // =========================================================================

    /// <summary>
    /// Verifies the INPUT PayloadJson contract between the .NET emitter
    /// (BuildKnowledgeGraphSuggestionsCommandHandler) and Python pipeline.py reader
    /// by ACTUALLY invoking the /build endpoint (admin) and inspecting the persisted
    /// PipelineJob row.
    ///
    /// All four defects are now fixed:
    ///   DEFECT-BL03-1 fixed: nodes[].title (was "name")
    ///   DEFECT-BL03-2 fixed: nodes[].node_type as STRING "Skill"/"Concept"/"Review" (was int)
    ///   DEFECT-BL03-2 fixed: nodes[].subject_code per-node as lowercase string "math" (was top-level int only)
    ///   DEFECT-BL03-3 fixed: nodes[].grade per-node as grade NUMBER int (was top-level grade_id FK only)
    ///   DEFECT-BL03-4 fixed: PipelineJob.DocumentId is nullable (migration 20260624084010)
    ///                        so infer_edges job insert no longer FK-violates
    ///
    /// Python InferPayload.parse contract (pipeline.py):
    ///   nodes[].skill_key     string (required)
    ///   nodes[].title         string (str(n.get("title", "")))
    ///   nodes[].node_type     string (str(n.get("node_type", "Skill")))
    ///   nodes[].subject_code  string lowercase (str(n.get("subject_code","")).strip().lower())
    ///   nodes[].grade         int (int(n.get("grade", 0)))
    ///   nodes[].difficulty    int (optional — None if not int)
    /// </summary>
    [Fact(DisplayName = "CONTRACT: /build endpoint creates PipelineJob{Pending, null DocumentId} with correct PayloadJson shape")]
    public async Task CONTRACT_BuildKGSuggestions_PayloadJson_ContractAgreement()
    {
        // Seed KnowledgeNodes with SkillKeys so the handler has something to include in payload
        // Use a dedicated skill-key prefix to avoid collisions with other tests
        const string skillKey1 = "math.grade4.frac.bl03-contract-input-A";
        const string skillKey2 = "math.grade4.div.bl03-contract-input-B";

        await using var learningDb = GetFreshLearningDb();
        await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, skillKey1, "Fractions Contract", gradeId: 4,
            subjectCode: SubjectCode.MATH);
        await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, skillKey2, "Division Contract", gradeId: 4,
            subjectCode: SubjectCode.MATH);

        // Count PipelineJobs before to identify the new row
        await using var currDbBefore = GetFreshCurriculumDb();
        var jobCountBefore = await currDbBefore.PipelineJobs
            .CountAsync(j => j.JobType == "infer_edges");

        // ── Call POST /api/curriculum/kg-suggestions/build as Admin ─────────────────────────────
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                BL03WebAppFactory.GenerateJwt(role: "Admin", userId: 99));

        // subjectCode=0 (MATH), gradeId=4
        var response = await client.PostAsync(
            "/api/curriculum/kg-suggestions/build?subjectCode=0&gradeId=4",
            null);

        // ── HTTP assertions ──────────────────────────────────────────────────────────────────────
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "/build must return 200 OK when nodes with SkillKeys exist (DEFECT-BL03-4 fix: DocumentId nullable)");

        var body = await response.Content.ReadAsStringAsync();
        var root = JsonDocument.Parse(body).RootElement;
        AssertEnvelope(root, body);
        root.GetProperty("successed").GetBoolean().Should().BeTrue(
            "successed must be true on successful build enqueue");

        // ── PipelineJob assertions ───────────────────────────────────────────────────────────────
        await using var currDbAfter = GetFreshCurriculumDb();
        var newJobs = await currDbAfter.PipelineJobs
            .Where(j => j.JobType == "infer_edges")
            .OrderByDescending(j => j.Id)
            .ToListAsync();

        newJobs.Count.Should().BeGreaterThan(jobCountBefore,
            "a new infer_edges PipelineJob row must have been inserted");

        var job = newJobs.First(); // most recently inserted

        // DEFECT-BL03-4 fix: DocumentId must be null for infer_edges jobs
        job.DocumentId.Should().BeNull(
            "DEFECT-BL03-4 fix: infer_edges job DocumentId must be null (not FK-bound to a document)");

        job.Status.Should().Be("Pending",
            "newly enqueued job must be in Pending status");

        job.JobType.Should().Be("infer_edges",
            "job must be of type infer_edges");

        // ── PayloadJson contract field assertions (field-for-field vs Python InferPayload.parse) ──
        job.PayloadJson.Should().NotBeNullOrWhiteSpace("PayloadJson must not be empty");

        var payload   = JsonDocument.Parse(job.PayloadJson!).RootElement;
        var nodesElem = payload.GetProperty("nodes");
        nodesElem.ValueKind.Should().Be(JsonValueKind.Array,
            "PayloadJson.nodes must be an array");
        nodesElem.GetArrayLength().Should().BeGreaterThan(0,
            "PayloadJson.nodes must be non-empty (we seeded nodes with SkillKeys)");

        var firstNode = nodesElem[0];

        // skill_key: required string — Python InferPayload.parse requires it
        firstNode.TryGetProperty("skill_key", out var skillKeyProp).Should().BeTrue(
            "AGREEMENT: .NET must emit 'skill_key' per node; Python reads 'skill_key' (required)");
        skillKeyProp.ValueKind.Should().Be(JsonValueKind.String,
            "skill_key must be a string value");
        skillKeyProp.GetString().Should().NotBeNullOrWhiteSpace(
            "skill_key must be non-empty");

        // title: DEFECT-BL03-1 fix — was "name", now "title"
        firstNode.TryGetProperty("title", out var titleProp).Should().BeTrue(
            "DEFECT-BL03-1 fix: .NET must emit 'title' per node (not 'name'); Python reads n.get('title','')");
        titleProp.ValueKind.Should().Be(JsonValueKind.String,
            "title must be a string value");

        // node_type: DEFECT-BL03-2b fix — was int, now string "Skill"/"Concept"/"Review"
        firstNode.TryGetProperty("node_type", out var nodeTypeProp).Should().BeTrue(
            "DEFECT-BL03-2b fix: .NET must emit 'node_type' per node; Python reads str(n.get('node_type','Skill'))");
        nodeTypeProp.ValueKind.Should().Be(JsonValueKind.String,
            "DEFECT-BL03-2b fix: node_type must be a STRING (Python does str(...) cast); was previously int");
        nodeTypeProp.GetString().Should().BeOneOf("Skill", "Concept", "Review",
            "node_type string must be one of the mapped values ('Skill', 'Concept', 'Review')");

        // subject_code: DEFECT-BL03-2 fix — must now be per-node lowercase string
        firstNode.TryGetProperty("subject_code", out var subjectCodeProp).Should().BeTrue(
            "DEFECT-BL03-2 fix: .NET must emit per-node 'subject_code' (string); Python reads str(n.get('subject_code','')).strip().lower()");
        subjectCodeProp.ValueKind.Should().Be(JsonValueKind.String,
            "DEFECT-BL03-2 fix: per-node subject_code must be a STRING (e.g. 'math'); Python lowercases it");
        subjectCodeProp.GetString()!.Should().Be(subjectCodeProp.GetString()!.ToLowerInvariant(),
            "subject_code must already be lowercase (Python does .strip().lower() but sender should match)");
        subjectCodeProp.GetString().Should().BeOneOf("math", "science", "arabic", "english",
            "subject_code must map to the domain string Python expects");

        // grade: DEFECT-BL03-3 fix — must now be per-node grade NUMBER int
        firstNode.TryGetProperty("grade", out var gradeProp).Should().BeTrue(
            "DEFECT-BL03-3 fix: .NET must emit per-node 'grade' (int grade number); Python reads int(n.get('grade', 0))");
        gradeProp.ValueKind.Should().Be(JsonValueKind.Number,
            "DEFECT-BL03-3 fix: per-node grade must be a NUMBER (int grade number e.g. 4); Python does int(...)");
        gradeProp.GetInt32().Should().BeGreaterThan(0,
            "per-node grade must be a positive grade number (1–6)");

        // difficulty: always agreed — optional int
        firstNode.TryGetProperty("difficulty", out var difficultyProp).Should().BeTrue(
            "AGREEMENT: .NET must emit 'difficulty' per node; Python reads it as optional int");
        difficultyProp.ValueKind.Should().Be(JsonValueKind.Number,
            "difficulty must be a number");

        // Verify "name" is NOT emitted as the node title key (old broken field)
        firstNode.TryGetProperty("name", out _).Should().BeFalse(
            "DEFECT-BL03-1 fix: the old 'name' key must NOT appear — Python ignores it; " +
            "'title' is the correct key");
    }

    // =========================================================================
    // CONTRACT: ResultJson OUTPUT contract — Python emitter vs .NET reader
    // =========================================================================

    [Fact(DisplayName = "CONTRACT: ResultJson OUTPUT contract — Python and .NET fields agree exactly")]
    public async Task CONTRACT_ResultJson_PythonEmitter_DotNetReader_FieldsAgree()
    {
        // Feed a ResultJson that exactly matches the FROZEN Python contract
        // and verify that EdgeInferenceAdvanceService reads it correctly:
        //   - inference_model: string ✓
        //   - edges[].source_skill_key: string ✓
        //   - edges[].target_skill_key: string ✓
        //   - edges[].relationship_type: "Prerequisite"|"Related" ✓
        //   - edges[].strength: float ✓
        //   - edges[].confidence: float ✓

        const string srcKey = "math.grade4.div.bl03-contract-src";
        const string tgtKey = "math.grade4.frac.bl03-contract-tgt";

        await using var learningDb = GetFreshLearningDb();
        var srcId = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, srcKey, "Contract Src", gradeId: 4);
        var tgtId = await BL03Helpers.SeedKnowledgeNodeAsync(learningDb, tgtKey, "Contract Tgt", gradeId: 4);

        // Frozen Python contract shape — exactly as specified in BL-03.md lockstep item
        var frozenResultJson = JsonSerializer.Serialize(new
        {
            inference_model = "lightrag-mock-v1",
            edges = new[]
            {
                new
                {
                    source_skill_key  = srcKey,
                    target_skill_key  = tgtKey,
                    relationship_type = "Prerequisite",
                    strength          = 0.82,
                    confidence        = 0.91,
                },
            },
        });

        await using var currDb = GetFreshCurriculumDb();
        await BL03Helpers.SeedInferJobAsync(currDb, "Done", frozenResultJson);

        // Wait for the advance service to process
        var advanced = await BL02Helpers.WaitForConditionAsync(async () =>
        {
            await using var db = GetFreshCurriculumDb();
            return await db.KGSuggestions
                .AnyAsync(s => s.SourceNodeId == srcId
                             && s.TargetNodeId == tgtId
                             && s.InferenceModel == "lightrag-mock-v1"
                             && s.Status == KGSuggestionStatus.Pending);
        }, timeoutSeconds: 15);

        advanced.Should().BeTrue(
            "OUTPUT CONTRACT AGREEMENT: .NET deserializer must read the frozen Python ResultJson fields correctly. " +
            "If this fails, there is a field name mismatch between Python emitter and .NET reader.");

        // Verify strength was stored correctly (not lost/truncated beyond rounding)
        await using var assertCurrDb = GetFreshCurriculumDb();
        var sug = await assertCurrDb.KGSuggestions
            .FirstAsync(s => s.SourceNodeId == srcId && s.TargetNodeId == tgtId);
        sug.Strength.Should().BeApproximately(0.82m, 0.001m,
            "OUTPUT CONTRACT: strength=0.82 from ResultJson must be stored correctly in KGSuggestion.Strength");
        sug.RelationshipType.Should().Be(CurriculumRelationshipType.Prerequisite,
            "OUTPUT CONTRACT: relationship_type='Prerequisite' must map to CurriculumRelationshipType.Prerequisite");
        sug.InferenceModel.Should().Be("lightrag-mock-v1",
            "OUTPUT CONTRACT: inference_model must be stored as-is from ResultJson");
    }

    // ─── Helper ──────────────────────────────────────────────────────────────────────────────────────
    private static void AssertEnvelope(JsonElement root, string body)
    {
        root.ValueKind.Should().Be(JsonValueKind.Object,
            "response body must be a JSON object; body: {0}", body);
        foreach (var key in new[] { "statusCode", "successed" })
        {
            (root.TryGetProperty(key, out _) ||
             root.TryGetProperty(char.ToUpperInvariant(key[0]) + key[1..], out _))
                .Should().BeTrue(
                    $"BaseResponse envelope must contain '{key}'; body: {body}");
        }
    }
}
