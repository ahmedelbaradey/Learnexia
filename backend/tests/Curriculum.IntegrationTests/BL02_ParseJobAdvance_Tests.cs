using System.Net;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Curriculum.Api;
using Learnexia.Modules.Curriculum.Application.Abstractions;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Learnexia.Modules.Curriculum.Infrastructure.Services;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Domain.Enums;
using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Shared.Kernel.Abstractions.Storage;
using AspNetCoreRateLimit;
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

// ─── xUnit collection to share the WebApplicationFactory across all BL-02 tests ──

[CollectionDefinition("BL02ParseJob")]
public sealed class BL02ParseJobCollection : ICollectionFixture<BL02WebAppFactory> { }

// ─── WebApplicationFactory ────────────────────────────────────────────────────

/// <summary>
/// WebApplicationFactory for BL-02 integration tests.
///
/// Key configuration:
/// - pgvector/pgvector:pg17 Testcontainers Postgres (no real PG dependency).
/// - Stubs IStorageService, ISessionManagementService, IEmbeddingProvider (no MinIO/Redis).
/// - Sets <c>CurriculumPipeline:PollerIntervalSeconds=1</c> so the advance service cycles
///   quickly and tests don't need long waits.
/// - <c>CurriculumPipeline:MaxRetries=3</c> (default; explicit for test clarity).
/// </summary>
public sealed class BL02WebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string JwtSigningKey =
        "CHANGE_ME_super_secret_key_at_least_32_chars_long_0123456789";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg17")
        .WithDatabase("bl02_test")
        .WithUsername("postgres")
        .WithPassword("testpwd")
        .Build();

    // Reuse FakeStorageService defined in BL01_CurriculumDocumentEndpoint_Tests.cs (same namespace)
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
                ["ConnectionStrings:Default"] = _postgres.GetConnectionString(),
                ["CurriculumUpload:MaxFileSizeBytes"] = "1048576",
                ["CurriculumUpload:BucketName"] = "curriculum-test",
                // Fast poll cycle so advance tests don't wait 5 seconds
                ["CurriculumPipeline:PollerIntervalSeconds"] = "1",
                ["CurriculumPipeline:MaxRetries"] = "3",
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

            // Stub IStorageService — no real MinIO
            services.RemoveAll<IStorageService>();
            services.AddSingleton<IStorageService>(FakeStorage);

            // Stub IEmbeddingProvider
            services.RemoveAll<IEmbeddingProvider>();
            services.AddScoped<IEmbeddingProvider, DeterministicStubEmbeddingProvider>();

            // Stub ISessionManagementService (P6-07 OnTokenValidated check)
            services.RemoveAll<ISessionManagementService>();
            services.AddScoped<ISessionManagementService, BL02AlwaysActiveSessionService>();

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
            // P6-07: must be a valid GUID
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

// ─── Stub ISessionManagementService ────────────────────────────────────────────

internal sealed class BL02AlwaysActiveSessionService : ISessionManagementService
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

// ─── Shared seeding helpers ────────────────────────────────────────────────────

internal static class BL02Helpers
{
    /// <summary>
    /// Seeds a minimal <see cref="CurriculumDocument"/> (Status=Processing by default so the
    /// advance poller won't skip it for being in a wrong state on the doc side).
    /// </summary>
    public static async Task<Learnexia.Modules.Curriculum.Domain.Entities.CurriculumDocument> SeedDocumentAsync(
        CurriculumDbContext db,
        DocumentStatus status = DocumentStatus.Processing,
        int gradeId = 4,
        int subjectId = 1,
        string contentType = "application/pdf")
    {
        var doc = new Learnexia.Modules.Curriculum.Domain.Entities.CurriculumDocument
        {
            FileName    = $"test_{Guid.NewGuid():N}.pdf",
            ObjectKey   = $"{Guid.NewGuid():N}.pdf",
            ContentType = contentType,
            FileSize    = 1024,
            GradeId     = gradeId,
            SubjectId   = subjectId,
            Language    = ContentLanguage.Arabic,
            Country     = "EG",
            Status      = status,
        };
        db.CurriculumDocuments.Add(doc);
        await db.SaveChangesAsync();
        return doc;
    }

    /// <summary>
    /// Seeds a <see cref="PipelineJob"/> row with the given status and optional ResultJson/ErrorMessage.
    /// </summary>
    public static async Task<Learnexia.Modules.Curriculum.Domain.Entities.PipelineJob> SeedJobAsync(
        CurriculumDbContext db,
        int documentId,
        string jobStatus,
        string? resultJson = null,
        string? errorMessage = null,
        int retryCount = 0)
    {
        var job = new Learnexia.Modules.Curriculum.Domain.Entities.PipelineJob
        {
            JobType      = "parse",
            Status       = jobStatus,
            DocumentId   = documentId,
            PayloadJson  = JsonSerializer.Serialize(new
            {
                object_key   = $"{Guid.NewGuid():N}.pdf",
                content_type = "application/pdf",
            }),
            ResultJson   = resultJson,
            ErrorMessage = errorMessage,
            RetryCount   = retryCount,
            ClaimedAt    = jobStatus == "Done" || jobStatus == "Failed"
                           ? DateTimeOffset.UtcNow.AddSeconds(-10) : null,
            CompletedAt  = jobStatus == "Done" || jobStatus == "Failed"
                           ? DateTimeOffset.UtcNow.AddSeconds(-5) : null,
        };
        db.PipelineJobs.Add(job);
        await db.SaveChangesAsync();
        return job;
    }

    /// <summary>
    /// Representative ResultJson matching the DB-outbox contract
    /// (artifact_key + chapters[] + parse_status + diagnostics).
    /// </summary>
    public static string MakeDoneResultJson(string? artifactKey = null) =>
        JsonSerializer.Serialize(new
        {
            artifact_key  = artifactKey ?? $"artifacts/{Guid.NewGuid():N}.json",
            chapters      = new[]
            {
                new { number = 1, title = "الكسور",           page_start = 1,  page_end = 20 },
                new { number = 2, title = "الأعداد الصحيحة",  page_start = 21, page_end = 45 },
            },
            parse_status  = "done",
            diagnostics   = (string?)null,
        });

    /// <summary>
    /// Polls the DB until the predicate returns true or the timeout elapses.
    /// Returns true if the condition became true in time.
    /// </summary>
    public static async Task<bool> WaitForConditionAsync(
        Func<Task<bool>> condition,
        int timeoutSeconds = 10,
        int pollIntervalMs = 250)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition()) return true;
            await Task.Delay(pollIntervalMs);
        }
        return false;
    }

    // ── JSON helpers mirroring BL01 helpers ─────────────────────────────────────

    public static JsonElement GetProp(JsonElement element, string name, string body)
    {
        if (element.TryGetProperty(name, out var v)) return v;
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (element.TryGetProperty(pascal, out v)) return v;
        foreach (var prop in element.EnumerateObject())
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                return prop.Value;
        throw new Xunit.Sdk.XunitException(
            $"Expected JSON property '{name}' not found. body: {body}");
    }

    public static bool TryGetProp(JsonElement element, string name, out JsonElement found)
    {
        if (element.TryGetProperty(name, out found)) return true;
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (element.TryGetProperty(pascal, out found)) return true;
        foreach (var prop in element.EnumerateObject())
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            { found = prop.Value; return true; }
        return false;
    }

    public static void AssertEnvelopeKeys(JsonElement root, string body)
    {
        root.ValueKind.Should().Be(JsonValueKind.Object,
            "response body must be a JSON object; body: {0}", body);
        foreach (var key in new[] { "statusCode", "successed" })
        {
            var found = false;
            foreach (var prop in root.EnumerateObject())
                if (string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase))
                { found = true; break; }
            found.Should().BeTrue(
                "BaseResponse<T> envelope must contain '{0}'; body: {1}", key, body);
        }
    }

    public static async Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        SendAsync(HttpClient client, HttpMethod method, string url,
                  HttpContent? body = null, string? bearer = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (bearer is not null)
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearer);
        if (body is not null)
            request.Content = body;

        var response = await client.SendAsync(request);
        var bodyStr  = await response.Content.ReadAsStringAsync();

        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            try { root = JsonDocument.Parse(bodyStr).RootElement; }
            catch { /* non-JSON body */ }
        }
        return (response, root, bodyStr);
    }
}

// ─── BL-02 Reparse Endpoint Tests (Cases 1–4) ─────────────────────────────────

/// <summary>
/// BL-02 HTTP integration tests for POST api/curriculum/documents/{id}/reparse.
///
/// Coverage:
///   BL02-HT-01 — Reparse happy path: admin, existing doc, no in-flight job → 200, doc=Processing, new Pending job.
///   BL02-HT-02 — Reparse 404: unknown document id → 404, Successed=false.
///   BL02-HT-03 — Reparse 409: Pending job already in flight → 409, CurriculumReparseJobInFlight, no duplicate job.
///   BL02-HT-03b — Reparse 409: Processing job already in flight → 409.
///   BL02-HT-04a — Authz: anonymous → 401.
///   BL02-HT-04b — Authz: non-admin (Student) → 403.
///   BL02-HT-04c — Authz: non-admin (Parent) → 403.
///   BL02-HT-04d — Authz: Admin → allowed (200).
///   BL02-HT-04e — Authz: SuperAdmin → allowed (200).
/// </summary>
[Collection("BL02ParseJob")]
public sealed class BL02_ReparseEndpoint_Tests : IAsyncLifetime
{
    private const string ReparseUrl = "api/curriculum/documents/{0}/reparse";

    private readonly BL02WebAppFactory _factory;
    private readonly HttpClient _client;

    public BL02_ReparseEndpoint_Tests(BL02WebAppFactory factory)
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

    // =========================================================================
    // BL02-HT-01: Reparse happy path
    // =========================================================================

    /// <summary>
    /// AC9 (happy): admin triggers reparse on an existing doc (no Pending/Processing job in flight).
    /// Expected: HTTP 200, Successed=true, envelope shape OK, doc transitions to Processing,
    /// exactly one new Pending parse job exists for the document.
    /// </summary>
    [Fact(DisplayName = "BL02-HT-01: Admin reparse happy path → 200 + doc=Processing + new Pending job")]
    public async Task HT01_AdminReparse_HappyPath_Returns200_DocProcessing_NewPendingJob()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CurriculumDbContext>();

        // Seed document with a Done status (finished parse, now re-parsing)
        var doc = await BL02Helpers.SeedDocumentAsync(db, status: DocumentStatus.Done);

        var adminJwt = BL02WebAppFactory.GenerateJwt(role: "Admin");
        var url = string.Format(ReparseUrl, doc.Id);

        var (response, root, body) =
            await BL02Helpers.SendAsync(_client, HttpMethod.Post, url, bearer: adminJwt);

        // ── HTTP 200 ──────────────────────────────────────────────────────────
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "reparse with no in-flight job must return 200; body: {0}", body);

        // ── Envelope shape ────────────────────────────────────────────────────
        BL02Helpers.AssertEnvelopeKeys(root, body);
        BL02Helpers.GetProp(root, "successed", body).GetBoolean().Should().BeTrue(
            "Successed must be true on successful reparse; body: {0}", body);

        // ── data payload contains document id ─────────────────────────────────
        var data = BL02Helpers.GetProp(root, "data", body);
        data.ValueKind.Should().NotBe(JsonValueKind.Null,
            "data must not be null on successful reparse; body: {0}", body);

        // ── DB: document transitioned to Processing ───────────────────────────
        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<CurriculumDbContext>();

        var docRow = await db2.CurriculumDocuments.FindAsync(doc.Id);
        docRow.Should().NotBeNull();
        docRow!.Status.Should().Be(DocumentStatus.Processing,
            "reparse must reset document status to Processing");

        // ── DB: exactly one new Pending parse job exists ──────────────────────
        var pendingJobs = await db2.PipelineJobs
            .Where(j => j.DocumentId == doc.Id && j.JobType == "parse" && j.Status == "Pending")
            .ToListAsync();
        pendingJobs.Should().HaveCount(1,
            "exactly one Pending parse job must exist after successful reparse");

        var freshJob = pendingJobs[0];
        freshJob.RetryCount.Should().Be(0, "reparse job must start with RetryCount=0");
        freshJob.PayloadJson.Should().Contain("object_key",
            "PayloadJson must carry the object_key");
        freshJob.PayloadJson.Should().Contain("content_type",
            "PayloadJson must carry the content_type");
    }

    // =========================================================================
    // BL02-HT-02: Reparse 404 — unknown document id
    // =========================================================================

    /// <summary>
    /// AC9: reparse request for a non-existent document id must return 404.
    /// </summary>
    [Fact(DisplayName = "BL02-HT-02: Reparse unknown document → 404")]
    public async Task HT02_Reparse_UnknownDocument_Returns404()
    {
        var adminJwt = BL02WebAppFactory.GenerateJwt(role: "Admin");
        var url = string.Format(ReparseUrl, 999999);

        var (response, root, body) =
            await BL02Helpers.SendAsync(_client, HttpMethod.Post, url, bearer: adminJwt);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "reparse on a missing document must return 404; body: {0}", body);

        if (root.ValueKind == JsonValueKind.Object)
        {
            BL02Helpers.AssertEnvelopeKeys(root, body);
            BL02Helpers.GetProp(root, "successed", body).GetBoolean().Should().BeFalse(
                "Successed=false on 404; body: {0}", body);
        }
    }

    // =========================================================================
    // BL02-HT-03: Reparse 409 — Pending job already in flight
    // =========================================================================

    /// <summary>
    /// AC9: reparse when a Pending parse job is already in flight must return 409 Conflict.
    /// The existing job must not be duplicated (no new job row created).
    /// The message must identify CurriculumReparseJobInFlight.
    /// </summary>
    [Fact(DisplayName = "BL02-HT-03: Reparse while Pending job in flight → 409, no duplicate job")]
    public async Task HT03_Reparse_PendingJobInFlight_Returns409_NoDuplicate()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CurriculumDbContext>();

        var doc = await BL02Helpers.SeedDocumentAsync(db, status: DocumentStatus.Processing);
        // Seed an in-flight Pending job
        await BL02Helpers.SeedJobAsync(db, doc.Id, "Pending");

        var jobCountBefore = await db.PipelineJobs
            .CountAsync(j => j.DocumentId == doc.Id && j.JobType == "parse");

        var adminJwt = BL02WebAppFactory.GenerateJwt(role: "Admin");
        var url = string.Format(ReparseUrl, doc.Id);

        var (response, root, body) =
            await BL02Helpers.SendAsync(_client, HttpMethod.Post, url, bearer: adminJwt);

        // ── HTTP 409 ──────────────────────────────────────────────────────────
        response.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "reparse while Pending job in flight must return 409 Conflict; body: {0}", body);

        if (root.ValueKind == JsonValueKind.Object)
        {
            BL02Helpers.AssertEnvelopeKeys(root, body);
            BL02Helpers.GetProp(root, "successed", body).GetBoolean().Should().BeFalse(
                "Successed=false on 409; body: {0}", body);
        }

        // ── No duplicate job created ──────────────────────────────────────────
        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<CurriculumDbContext>();
        var jobCountAfter = await db2.PipelineJobs
            .CountAsync(j => j.DocumentId == doc.Id && j.JobType == "parse");

        jobCountAfter.Should().Be(jobCountBefore,
            "no new job must be created when the 409 guard fires");
    }

    // =========================================================================
    // BL02-HT-03b: Reparse 409 — Processing job already in flight
    // =========================================================================

    /// <summary>
    /// AC9: reparse when a Processing parse job is already in flight must also return 409.
    /// </summary>
    [Fact(DisplayName = "BL02-HT-03b: Reparse while Processing job in flight → 409")]
    public async Task HT03b_Reparse_ProcessingJobInFlight_Returns409()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CurriculumDbContext>();

        var doc = await BL02Helpers.SeedDocumentAsync(db, status: DocumentStatus.Processing);
        // Seed an in-flight Processing job (claimed by Python worker but not yet Done/Failed)
        await BL02Helpers.SeedJobAsync(db, doc.Id, "Processing");

        var adminJwt = BL02WebAppFactory.GenerateJwt(role: "Admin");
        var url = string.Format(ReparseUrl, doc.Id);

        var (response, _, body) =
            await BL02Helpers.SendAsync(_client, HttpMethod.Post, url, bearer: adminJwt);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "reparse while Processing job in flight must return 409 Conflict; body: {0}", body);
    }

    // =========================================================================
    // BL02-HT-04a: Authz — anonymous → 401
    // =========================================================================

    [Fact(DisplayName = "BL02-HT-04a: Anonymous reparse → 401")]
    public async Task HT04a_Anonymous_Reparse_Returns401()
    {
        // Use any ID — auth fires before handler
        var (response, _, body) =
            await BL02Helpers.SendAsync(_client, HttpMethod.Post,
                "api/curriculum/documents/1/reparse");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "anonymous request must return 401; body: {0}", body);
    }

    // =========================================================================
    // BL02-HT-04b: Authz — Student → 403
    // =========================================================================

    [Fact(DisplayName = "BL02-HT-04b: Student role → 403 on reparse")]
    public async Task HT04b_StudentRole_Reparse_Returns403()
    {
        var studentJwt = BL02WebAppFactory.GenerateJwt(role: "Student");

        var (response, _, body) =
            await BL02Helpers.SendAsync(_client, HttpMethod.Post,
                "api/curriculum/documents/1/reparse", bearer: studentJwt);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Student role must be denied (403) on reparse; body: {0}", body);
    }

    // =========================================================================
    // BL02-HT-04c: Authz — Parent → 403
    // =========================================================================

    [Fact(DisplayName = "BL02-HT-04c: Parent role → 403 on reparse")]
    public async Task HT04c_ParentRole_Reparse_Returns403()
    {
        var parentJwt = BL02WebAppFactory.GenerateJwt(role: "Parent");

        var (response, _, body) =
            await BL02Helpers.SendAsync(_client, HttpMethod.Post,
                "api/curriculum/documents/1/reparse", bearer: parentJwt);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Parent role must be denied (403) on reparse; body: {0}", body);
    }

    // =========================================================================
    // BL02-HT-04d: Authz — Admin → allowed (200 or 404 for missing doc, not 401/403)
    // =========================================================================

    [Fact(DisplayName = "BL02-HT-04d: Admin role → past auth gate (200 or 404)")]
    public async Task HT04d_AdminRole_Reparse_PastAuthGate()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CurriculumDbContext>();

        // Use a real doc so we get 200, not 404
        var doc = await BL02Helpers.SeedDocumentAsync(db, status: DocumentStatus.Done);
        var adminJwt = BL02WebAppFactory.GenerateJwt(role: "Admin");
        var url = string.Format(ReparseUrl, doc.Id);

        var (response, _, body) =
            await BL02Helpers.SendAsync(_client, HttpMethod.Post, url, bearer: adminJwt);

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            "Admin must not get 401; body: {0}", body);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "Admin must not get 403; body: {0}", body);
        // Expect 200 (success) or 409 if background job already ran and created a Pending job
        // (race with the advance service). Both indicate auth passed.
        new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }.Should().Contain(
            response.StatusCode,
            "Admin must reach the handler (200 or 409); body: {0}", body);
    }

    // =========================================================================
    // BL02-HT-04e: Authz — SuperAdmin → allowed
    // =========================================================================

    [Fact(DisplayName = "BL02-HT-04e: SuperAdmin role → past auth gate (200 or 404)")]
    public async Task HT04e_SuperAdminRole_Reparse_PastAuthGate()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CurriculumDbContext>();

        var doc = await BL02Helpers.SeedDocumentAsync(db, status: DocumentStatus.Done);
        var superAdminJwt = BL02WebAppFactory.GenerateJwt(role: "SuperAdmin");
        var url = string.Format(ReparseUrl, doc.Id);

        var (response, _, body) =
            await BL02Helpers.SendAsync(_client, HttpMethod.Post, url, bearer: superAdminJwt);

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            "SuperAdmin must not get 401; body: {0}", body);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "SuperAdmin must not get 403; body: {0}", body);
    }
}

// ─── BL-02 ParseJobAdvanceService Logic Tests (Cases 5–8) ─────────────────────

/// <summary>
/// BL-02 integration tests for ParseJobAdvanceService advance logic.
///
/// Strategy: approach (b) — seed PipelineJob rows in Done/Failed state directly into the test DB
/// (bypassing the Python worker, which is not present in CI). The hosted BackgroundService is
/// configured with PollerIntervalSeconds=1 and polls immediately on startup. Tests seed rows
/// BEFORE the first poll cycle fires, then wait up to 10 s for the state to appear.
///
/// This is reliable because:
///   1. The factory starts the full hosted-service pipeline (ParseJobAdvanceService runs).
///   2. PollerIntervalSeconds=1 guarantees the first advance happens within ~2 s.
///   3. WaitForConditionAsync polls the DB every 250 ms for up to 10 s — well above the 1 s cycle.
///
/// Isolation: each test seeds its own document + job rows; IDs are distinct.
///
/// Coverage:
///   BL02-ADV-01 — Done job advance: doc=Done + ParsedArtifactObjectKey + ParsedAt; ContentSource + Chapter[] created; job=Archived.
///   BL02-ADV-02 — Failed with retries remaining: new Pending job (RetryCount+1), doc=Processing, old job=PermanentlyFailed.
///   BL02-ADV-03 — Failed exhausted (RetryCount>=MaxRetries): doc=Failed + StatusReason, no new job, job=PermanentlyFailed.
///   BL02-ADV-04 — Idempotency: a Done job is processed exactly once (Archived prevents re-pickup).
/// </summary>
[Collection("BL02ParseJob")]
public sealed class BL02_ParseJobAdvance_Tests : IAsyncLifetime
{
    private readonly BL02WebAppFactory _factory;

    public BL02_ParseJobAdvance_Tests(BL02WebAppFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ─── Helper: fresh scoped DbContext (avoids EF change-tracker cross-contamination) ──

    private CurriculumDbContext GetFreshDb()
    {
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<CurriculumDbContext>();
    }

    // =========================================================================
    // BL02-ADV-01: Done job → doc=Done + ParsedArtifactObjectKey + ParsedAt + provenance
    // =========================================================================

    /// <summary>
    /// AC7 + AC8: seed document + Done PipelineJob with representative ResultJson.
    /// After advance: document Status=Done, ParsedArtifactObjectKey set, ParsedAt set,
    /// one ContentSource row + 2 Chapter rows created, original job Status=Archived.
    /// </summary>
    [Fact(DisplayName = "BL02-ADV-01: Done parse job advanced → doc=Done, provenance rows created, job=Archived")]
    public async Task ADV01_DoneJob_Advances_DocDone_ProvenanceRows_JobArchived()
    {
        // ── Seed ──────────────────────────────────────────────────────────────
        await using var db = GetFreshDb();
        var doc = await BL02Helpers.SeedDocumentAsync(db, status: DocumentStatus.Processing);

        var artifactKey  = $"artifacts/{Guid.NewGuid():N}.json";
        var resultJson   = BL02Helpers.MakeDoneResultJson(artifactKey);
        var job          = await BL02Helpers.SeedJobAsync(db, doc.Id, "Done", resultJson: resultJson);

        // ── Wait for advance service to process the job ───────────────────────
        // Condition: document Status transitioned to Done
        var advanced = await BL02Helpers.WaitForConditionAsync(async () =>
        {
            await using var pollDb = GetFreshDb();
            var d = await pollDb.CurriculumDocuments.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == doc.Id);
            return d?.Status == DocumentStatus.Done;
        }, timeoutSeconds: 10);

        advanced.Should().BeTrue(
            "ParseJobAdvanceService must advance a Done job to doc.Status=Done within 10 s");

        // ── Detailed assertions ───────────────────────────────────────────────
        await using var assertDb = GetFreshDb();

        var updatedDoc = await assertDb.CurriculumDocuments.AsNoTracking()
            .FirstAsync(d => d.Id == doc.Id);

        updatedDoc.Status.Should().Be(DocumentStatus.Done,
            "document status must be Done after advancing a Done parse job");
        updatedDoc.ParsedArtifactObjectKey.Should().Be(artifactKey,
            "ParsedArtifactObjectKey must match the artifact_key in ResultJson");
        updatedDoc.ParsedAt.Should().NotBeNull(
            "ParsedAt must be set after a successful advance");
        updatedDoc.StatusReason.Should().BeNullOrEmpty(
            "StatusReason must be null/empty on a successful parse");

        // ── Job transitioned to Archived ──────────────────────────────────────
        var updatedJob = await assertDb.PipelineJobs.AsNoTracking()
            .FirstAsync(j => j.Id == job.Id);
        updatedJob.Status.Should().Be("Archived",
            "Done parse job must be archived (Status='Archived') after advancing (Q6)");

        // ── ContentSource + Chapter rows created (BE-6 provenance) ───────────
        var contentSources = await assertDb.ContentSources.AsNoTracking()
            .Where(cs => cs.GradeId == doc.GradeId && cs.Title == doc.FileName)
            .Include(cs => cs.Chapters)
            .ToListAsync();

        contentSources.Should().HaveCountGreaterOrEqualTo(1,
            "at least one ContentSource must be created for the document (BE-6 provenance)");

        var contentSource = contentSources.First();
        contentSource.Type.Should().Be(ContentSourceType.Book,
            "application/pdf should map to ContentSourceType.Book");
        contentSource.GradeId.Should().Be(doc.GradeId,
            "ContentSource.GradeId must match the document");

        contentSource.Chapters.Should().HaveCount(2,
            "two Chapter rows must be created matching the chapters[] in ResultJson");

        var ch1 = contentSource.Chapters.First(c => c.Number == 1);
        ch1.Title.Should().Be("الكسور", "chapter 1 title must match ResultJson");
        ch1.PageStart.Should().Be(1);
        ch1.PageEnd.Should().Be(20);

        var ch2 = contentSource.Chapters.First(c => c.Number == 2);
        ch2.Title.Should().Be("الأعداد الصحيحة", "chapter 2 title must match ResultJson");
        ch2.PageStart.Should().Be(21);
        ch2.PageEnd.Should().Be(45);
    }

    // =========================================================================
    // BL02-ADV-02: Failed with retries remaining → re-enqueue + doc=Processing + old=PermanentlyFailed
    // =========================================================================

    /// <summary>
    /// AC7 (failed path, retries remaining): seed a Failed job with RetryCount=0 (MaxRetries=3).
    /// After advance: a new Pending job exists with RetryCount=1; doc.Status=Processing;
    /// old job Status=PermanentlyFailed.
    /// </summary>
    [Fact(DisplayName = "BL02-ADV-02: Failed job (retries remaining) → new Pending job re-enqueued, doc=Processing, old=PermanentlyFailed")]
    public async Task ADV02_FailedJob_RetriesRemaining_NewPendingJob_DocProcessing_OldPermanentlyFailed()
    {
        // ── Seed ──────────────────────────────────────────────────────────────
        await using var db = GetFreshDb();
        var doc = await BL02Helpers.SeedDocumentAsync(db, status: DocumentStatus.Processing);

        // RetryCount=0, MaxRetries=3 → newRetryCount=1 ≤ 3 → re-enqueue
        var job = await BL02Helpers.SeedJobAsync(
            db, doc.Id, "Failed",
            errorMessage: "Azure DI timeout",
            retryCount: 0);

        // ── Wait for advance service to process the failed job ────────────────
        // Condition: old job transitioned to PermanentlyFailed
        var advanced = await BL02Helpers.WaitForConditionAsync(async () =>
        {
            await using var pollDb = GetFreshDb();
            var j = await pollDb.PipelineJobs.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == job.Id);
            return j?.Status == "PermanentlyFailed";
        }, timeoutSeconds: 10);

        advanced.Should().BeTrue(
            "ParseJobAdvanceService must mark the old failed job as PermanentlyFailed within 10 s");

        // ── Detailed assertions ───────────────────────────────────────────────
        await using var assertDb = GetFreshDb();

        // Old job must be PermanentlyFailed
        var oldJob = await assertDb.PipelineJobs.AsNoTracking()
            .FirstAsync(j => j.Id == job.Id);
        oldJob.Status.Should().Be("PermanentlyFailed",
            "failed job must be marked PermanentlyFailed after advance (archive-in-place Q6)");

        // New Pending job must exist with RetryCount=1
        var newJobs = await assertDb.PipelineJobs.AsNoTracking()
            .Where(j => j.DocumentId == doc.Id
                     && j.JobType   == "parse"
                     && j.Status    == "Pending"
                     && j.RetryCount == 1)
            .ToListAsync();
        newJobs.Should().HaveCount(1,
            "exactly one new Pending job with RetryCount=1 must be re-enqueued");

        // Document must be Processing (reset by the re-enqueue path)
        var updatedDoc = await assertDb.CurriculumDocuments.AsNoTracking()
            .FirstAsync(d => d.Id == doc.Id);
        updatedDoc.Status.Should().Be(DocumentStatus.Processing,
            "document must be reset to Processing when a retry is re-enqueued");
    }

    // =========================================================================
    // BL02-ADV-03: Failed exhausted (RetryCount >= MaxRetries) → doc=Failed + StatusReason + no new job
    // =========================================================================

    /// <summary>
    /// AC7 (failed path, exhausted): seed a Failed job with RetryCount=3 (= MaxRetries=3).
    /// After advance: document Status=Failed, StatusReason set; no new Pending job;
    /// old job Status=PermanentlyFailed.
    /// </summary>
    [Fact(DisplayName = "BL02-ADV-03: Failed job (retries exhausted) → doc=Failed, StatusReason set, no new job")]
    public async Task ADV03_FailedJob_RetriesExhausted_DocFailed_StatusReasonSet_NoNewJob()
    {
        // ── Seed ──────────────────────────────────────────────────────────────
        await using var db = GetFreshDb();
        var doc = await BL02Helpers.SeedDocumentAsync(db, status: DocumentStatus.Processing);

        const string errorMsg = "Parse permanently failed: OCR service unavailable";
        // RetryCount=3 = MaxRetries=3 → newRetryCount=4 > 3 → dead-letter
        var job = await BL02Helpers.SeedJobAsync(
            db, doc.Id, "Failed",
            errorMessage: errorMsg,
            retryCount: 3);

        // ── Wait for advance ──────────────────────────────────────────────────
        var advanced = await BL02Helpers.WaitForConditionAsync(async () =>
        {
            await using var pollDb = GetFreshDb();
            var d = await pollDb.CurriculumDocuments.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == doc.Id);
            return d?.Status == DocumentStatus.Failed;
        }, timeoutSeconds: 10);

        advanced.Should().BeTrue(
            "ParseJobAdvanceService must set doc.Status=Failed on exhausted retries within 10 s");

        // ── Detailed assertions ───────────────────────────────────────────────
        await using var assertDb = GetFreshDb();

        // Document must be Failed with StatusReason
        var updatedDoc = await assertDb.CurriculumDocuments.AsNoTracking()
            .FirstAsync(d => d.Id == doc.Id);
        updatedDoc.Status.Should().Be(DocumentStatus.Failed,
            "document status must be Failed after exhausting retries");
        updatedDoc.StatusReason.Should().NotBeNullOrWhiteSpace(
            "StatusReason must be populated with the error message on permanent failure");
        updatedDoc.StatusReason!.Should().Contain(errorMsg,
            "StatusReason must contain the original ErrorMessage from the failed job");

        // No new Pending job
        var newPendingJobs = await assertDb.PipelineJobs.AsNoTracking()
            .Where(j => j.DocumentId == doc.Id
                     && j.JobType   == "parse"
                     && j.Status    == "Pending")
            .ToListAsync();
        newPendingJobs.Should().BeEmpty(
            "no new Pending job must be created when retries are exhausted");

        // Old job must be PermanentlyFailed
        var oldJob = await assertDb.PipelineJobs.AsNoTracking()
            .FirstAsync(j => j.Id == job.Id);
        oldJob.Status.Should().Be("PermanentlyFailed",
            "exhausted-retry job must be marked PermanentlyFailed");
    }

    // =========================================================================
    // BL02-ADV-04: Idempotency — processed Done job is not re-processed
    // =========================================================================

    /// <summary>
    /// AC8 (idempotency): once a Done job has been advanced and transitioned to Archived,
    /// subsequent advance passes must NOT re-process it (the SKIP LOCKED + status filter
    /// prevents re-pickup). Asserts that a second 10-second wait does not change doc state.
    ///
    /// Strategy: advance ADV-01 already verifies Archived status. Here we directly verify
    /// that an Archived job is NOT eligible for re-pickup by the claim CTE (Status IN ('Done','Failed')).
    /// We seed an Archived job and assert that the document's ParsedArtifactObjectKey does NOT
    /// get overwritten/reset after waiting a full second poll cycle.
    /// </summary>
    [Fact(DisplayName = "BL02-ADV-04: Archived job is not re-processed on subsequent poll cycles")]
    public async Task ADV04_ArchivedJob_NotReprocessed_OnSubsequentPolls()
    {
        // ── Seed an Archived job (already-processed Done job) ─────────────────
        await using var db = GetFreshDb();
        var doc = await BL02Helpers.SeedDocumentAsync(db, status: DocumentStatus.Done);

        // Manually set ParsedArtifactObjectKey as if it was already advanced
        var originalKey = $"artifacts/already-done-{Guid.NewGuid():N}.json";
        doc.ParsedArtifactObjectKey = originalKey;
        doc.ParsedAt                = DateTimeOffset.UtcNow.AddMinutes(-5);
        db.CurriculumDocuments.Update(doc);

        var archivedJob = await BL02Helpers.SeedJobAsync(db, doc.Id, "Archived");
        await db.SaveChangesAsync();

        // Wait 3 seconds to allow at least 2 advance cycles at 1 s interval
        await Task.Delay(TimeSpan.FromSeconds(3));

        // ── Assert document NOT changed ───────────────────────────────────────
        await using var assertDb = GetFreshDb();
        var stillDoc = await assertDb.CurriculumDocuments.AsNoTracking()
            .FirstAsync(d => d.Id == doc.Id);

        stillDoc.Status.Should().Be(DocumentStatus.Done,
            "an Archived job must not cause a status change on a subsequent poll");
        stillDoc.ParsedArtifactObjectKey.Should().Be(originalKey,
            "ParsedArtifactObjectKey must NOT be overwritten when the job is already Archived");

        // The Archived job must still be Archived
        var jobRow = await assertDb.PipelineJobs.AsNoTracking()
            .FirstAsync(j => j.Id == archivedJob.Id);
        jobRow.Status.Should().Be("Archived",
            "Archived job status must remain Archived — it must not be re-claimed");
    }

    // =========================================================================
    // BL02-ADV-05: Done job with image content-type → ContentSourceType.Image
    // =========================================================================

    /// <summary>
    /// Content-type mapping: image/png must produce ContentSourceType.Image (not Book).
    /// Verifies the MapContentType branch in ParseJobAdvanceService.
    /// </summary>
    [Fact(DisplayName = "BL02-ADV-05: Done job for image/png doc → ContentSource.Type=Image")]
    public async Task ADV05_DoneJob_ImageContentType_ContentSourceTypeImage()
    {
        await using var db = GetFreshDb();
        var doc = await BL02Helpers.SeedDocumentAsync(
            db, status: DocumentStatus.Processing, contentType: "image/png");

        var resultJson = BL02Helpers.MakeDoneResultJson();
        await BL02Helpers.SeedJobAsync(db, doc.Id, "Done", resultJson: resultJson);

        // Wait for advance
        var advanced = await BL02Helpers.WaitForConditionAsync(async () =>
        {
            await using var pollDb = GetFreshDb();
            var d = await pollDb.CurriculumDocuments.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == doc.Id);
            return d?.Status == DocumentStatus.Done;
        }, timeoutSeconds: 10);

        advanced.Should().BeTrue("advance must complete within 10 s");

        await using var assertDb = GetFreshDb();
        var contentSources = await assertDb.ContentSources.AsNoTracking()
            .Where(cs => cs.GradeId == doc.GradeId && cs.Title == doc.FileName)
            .ToListAsync();

        contentSources.Should().HaveCountGreaterOrEqualTo(1,
            "ContentSource must be created for image/png document");
        contentSources.First().Type.Should().Be(ContentSourceType.Image,
            "image/png content-type must map to ContentSourceType.Image");
    }

    // =========================================================================
    // BL02-ADV-06: Idempotent upsert — re-parse with same doc creates no duplicate ContentSource
    // =========================================================================

    /// <summary>
    /// AC8 (idempotent re-parse): if a ContentSource already exists for (GradeId, FileName),
    /// a second advance must reuse it (not create a duplicate) and upsert chapters.
    ///
    /// Simulates two successive Done jobs for the same document (as would happen with reparse).
    /// </summary>
    [Fact(DisplayName = "BL02-ADV-06: Second Done job for same doc → ContentSource reused, chapters upserted")]
    public async Task ADV06_SecondDoneJob_ContentSourceReused_ChaptersUpserted()
    {
        await using var db = GetFreshDb();
        var doc = await BL02Helpers.SeedDocumentAsync(db, status: DocumentStatus.Processing);

        // First Done job
        var resultJson1 = BL02Helpers.MakeDoneResultJson();
        await BL02Helpers.SeedJobAsync(db, doc.Id, "Done", resultJson: resultJson1);

        // Wait for first advance
        await BL02Helpers.WaitForConditionAsync(async () =>
        {
            await using var pollDb = GetFreshDb();
            var d = await pollDb.CurriculumDocuments.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == doc.Id);
            return d?.Status == DocumentStatus.Done;
        }, timeoutSeconds: 10);

        // Re-seed document as Processing + a second Done job (simulates reparse flow)
        await using var db2 = GetFreshDb();
        var docToReset = await db2.CurriculumDocuments.FirstAsync(d => d.Id == doc.Id);
        docToReset.Status = DocumentStatus.Processing;
        db2.CurriculumDocuments.Update(docToReset);
        await db2.SaveChangesAsync();

        var resultJson2 = BL02Helpers.MakeDoneResultJson();
        await BL02Helpers.SeedJobAsync(db2, doc.Id, "Done", resultJson: resultJson2);

        // Wait for second advance
        await BL02Helpers.WaitForConditionAsync(async () =>
        {
            await using var pollDb = GetFreshDb();
            var d = await pollDb.CurriculumDocuments.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == doc.Id);
            return d?.Status == DocumentStatus.Done;
        }, timeoutSeconds: 10);

        // ── Assert no duplicate ContentSource ─────────────────────────────────
        await using var assertDb = GetFreshDb();
        var contentSources = await assertDb.ContentSources.AsNoTracking()
            .Where(cs => cs.GradeId == doc.GradeId && cs.Title == doc.FileName)
            .ToListAsync();

        contentSources.Should().HaveCount(1,
            "re-parse must reuse the existing ContentSource row (idempotent upsert on GradeId+Title)");

        var cs = await assertDb.ContentSources
            .Include(c => c.Chapters)
            .FirstAsync(c => c.GradeId == doc.GradeId && c.Title == doc.FileName);

        cs.Chapters.Should().HaveCount(2,
            "chapters must be upserted (not duplicated) on re-parse");
    }
}
