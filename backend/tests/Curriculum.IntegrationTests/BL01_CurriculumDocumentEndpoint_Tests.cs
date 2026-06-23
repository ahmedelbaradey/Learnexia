using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Curriculum.Api;
using Learnexia.Modules.Curriculum.Application.Abstractions;
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

// ─── Collection ───────────────────────────────────────────────────────────────

[CollectionDefinition("BL01Http")]
public sealed class BL01HttpCollection : ICollectionFixture<BL01WebAppFactory> { }

// ─── Factory ─────────────────────────────────────────────────────────────────

/// <summary>
/// WebApplicationFactory for BL-01 HTTP integration tests.
///
/// Key differences from <see cref="CurriculumWebAppFactory"/>:
///   - Mocks <see cref="IStorageService"/> (no MinIO container in tests) — returns Success with a
///     deterministic GUID key so the handler can write CurriculumDocument + PipelineJob to the DB.
///   - Overrides <see cref="CurriculumUploadConfiguration"/> to lower MaxFileSizeBytes to 1 MB so
///     the size-limit test can send a small oversize payload (avoids allocating 100 MB in CI).
///   - Uses <c>pgvector/pgvector:pg17</c> (matches BL-01 schema tests).
///   - Does NOT stub <see cref="IEmbeddingProvider"/>; no embedding calls happen on the upload path.
/// </summary>
public sealed class BL01WebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // JWT signing key matching appsettings.json
    private const string JwtSigningKey =
        "CHANGE_ME_super_secret_key_at_least_32_chars_long_0123456789";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg17")
        .WithDatabase("bl01_http_test")
        .WithUsername("postgres")
        .WithPassword("testpwd")
        .Build();

    /// <summary>Allows tests to configure what the mock storage returns.</summary>
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
                // Lower the cap to 1 MB for the size-limit test (avoids 100 MB allocation in CI).
                // The handler enforces MaxFileSizeBytes; we test the validator path with ~1.1 MB.
                ["CurriculumUpload:MaxFileSizeBytes"] = "1048576",  // 1 MB
                ["CurriculumUpload:BucketName"] = "curriculum-test",
            });
        });

        builder.ConfigureServices(services =>
        {
            // ── Override CurriculumDbContext for the test container ────────────────────────────────
            services.RemoveAll<DbContextOptions<CurriculumDbContext>>();
            services.RemoveAll<CurriculumDbContext>();
            services.AddDbContext<CurriculumDbContext>(options =>
                options.UseNpgsql(
                    _postgres.GetConnectionString(),
                    npgsql => npgsql
                        .UseVector()
                        .MigrationsHistoryTable("__EFMigrationsHistory", CurriculumDbContext.Schema)
                        .MigrationsAssembly(typeof(CurriculumDbContext).Assembly.FullName)));

            // ── Stub IStorageService — no real MinIO in tests ────────────────────────────────────
            // BucketEnsureService + UploadCurriculumDocumentCommandHandler both inject IStorageService.
            // Replace the MinIO adapter with our controllable in-memory fake.
            services.RemoveAll<IStorageService>();
            services.AddSingleton<IStorageService>(FakeStorage);

            // ── Stub IEmbeddingProvider (needed only for other Curriculum endpoints) ──────────────
            services.RemoveAll<IEmbeddingProvider>();
            services.AddScoped<IEmbeddingProvider, DeterministicStubEmbeddingProvider>();

            // ── Stub ISessionManagementService ────────────────────────────────────────────────────
            // The P6-07 OnTokenValidated JWT hook checks that the SessionId claim is a valid GUID
            // and that the corresponding session is active in the cache (Redis). Test JWTs carry
            // a synthetic GUID SessionId but have no real Redis-backed session. Replacing the service
            // here makes IsSessionActiveAsync return true for any GUID so the test JWTs pass auth.
            services.RemoveAll<ISessionManagementService>();
            services.AddScoped<ISessionManagementService, AlwaysActiveSessionService>();

            // ── Disable IP rate limiting ──────────────────────────────────────────────────────────
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

    /// <summary>Apply migrations and (optionally) seed data.</summary>
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

    /// <summary>
    /// Generates a signed JWT with the given role claim and optional user-id claim.
    /// The AdminOnly policy requires <c>ClaimTypes.Role</c> = "Admin" or "SuperAdmin".
    /// The handler's <see cref="CurriculumCurrentUserService"/> reads <c>UserId</c> from claim "Id".
    ///
    /// <para>P6-07: the <c>SessionId</c> claim must be a valid GUID for the OnTokenValidated hook to
    /// proceed past the first guard. The <see cref="AlwaysActiveSessionService"/> stub then returns
    /// <c>true</c> for any session so the request is authenticated.</para>
    /// </summary>
    public static string GenerateJwt(string? role = null, int userId = 1)
    {
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(JwtSigningKey));
        var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        var claims = new List<System.Security.Claims.Claim>
        {
            new("Id", userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, "testuser"),
            // P6-07: must be a valid GUID or the OnTokenValidated hook calls context.Fail immediately.
            new("SessionId", Guid.NewGuid().ToString()),
        };
        if (role is not null)
            claims.Add(new System.Security.Claims.Claim(ClaimTypes.Role, role));

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer:   "Learnexia",
            audience: "LearnexiaClient",
            claims:   claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .WriteToken(token);
    }
}

// ─── Fake ISessionManagementService ──────────────────────────────────────────

/// <summary>
/// Stub <see cref="ISessionManagementService"/> for tests — always reports sessions as active.
/// This bypasses the P6-07 OnTokenValidated session-revocation check, which would otherwise
/// reject our synthetically-generated test JWTs (they have no real Redis-backed session).
/// In Testing environment the session check is already fail-open on Redis errors, but the
/// first guard (missing/invalid SessionId GUID) fails regardless of environment. Adding this
/// stub + a valid GUID SessionId claim in the JWT is the idiomatic test fix.
/// </summary>
internal sealed class AlwaysActiveSessionService : ISessionManagementService
{
    public Task<UserSession> CreateSessionAsync(int userId, string jwtTokenId, string sessionId)
        => Task.FromResult(new UserSession { SessionId = sessionId, UserId = userId, IsActive = true });

    public Task<UserSession?> GetSessionAsync(string sessionId)
        => Task.FromResult<UserSession?>(new UserSession { SessionId = sessionId, IsActive = true });

    public Task<bool> IsSessionActiveAsync(string sessionId) => Task.FromResult(true);

    public Task<List<UserSession>> GetUserSessionsAsync(int userId)
        => Task.FromResult(new List<UserSession>());

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

// ─── Fake IStorageService ─────────────────────────────────────────────────────

/// <summary>
/// Controllable in-memory fake for <see cref="IStorageService"/>.
/// By default, UploadFileAsync returns Success with a deterministic key.
/// Tests can set <see cref="ShouldFailUpload"/> = true to simulate storage failure.
/// </summary>
public sealed class FakeStorageService : IStorageService
{
    public bool ShouldFailUpload { get; set; } = false;

    /// <summary>Records all uploaded keys so tests can verify no orphaned objects.</summary>
    public List<string> UploadedKeys { get; } = new();

    public Task<StorageResult> UploadFileAsync(
        Stream content, string fileName, string contentType, long length,
        string bucketName, CancellationToken ct = default)
    {
        if (ShouldFailUpload)
            return Task.FromResult(new StorageResult
            {
                Success = false, ErrorMessage = "Simulated storage failure."
            });

        var key = fileName; // handler already passes a GUID-keyed name
        UploadedKeys.Add(key);
        return Task.FromResult(new StorageResult
        {
            Success = true,
            FilePath = key,
            FileName = key,
            FileSize = length,
            ContentType = contentType,
        });
    }

    public Task<FileDownloadResult> DownloadFileAsync(string objectKey, string bucketName, CancellationToken ct = default)
        => Task.FromResult(new FileDownloadResult { Success = true, FileBytes = Array.Empty<byte>() });

    public Task<string> GetPreviewUrlAsync(string objectKey, string bucketName, int expiryInMinutes = 60, CancellationToken ct = default)
        => Task.FromResult($"https://fake-storage/{bucketName}/{objectKey}");

    public Task<bool> FileExistsAsync(string objectKey, string bucketName, CancellationToken ct = default)
        => Task.FromResult(UploadedKeys.Contains(objectKey));

    public Task<bool> EnsureBucketAsync(string bucketName, CancellationToken ct = default)
        => Task.FromResult(true);

    public void Reset()
    {
        ShouldFailUpload = false;
        UploadedKeys.Clear();
    }
}

// ─── Helpers (shared with the test class) ────────────────────────────────────

internal static class Bl01TestHelpers
{
    /// <summary>
    /// Creates a minimal valid PDF IFormFile byte array: the PDF magic bytes (%PDF-)
    /// followed by enough padding to pass the 5-byte minimum check.
    /// </summary>
    public static byte[] MakePdfBytes(int extraBytes = 100)
    {
        var bytes = new byte[5 + extraBytes];
        bytes[0] = 0x25; // %
        bytes[1] = 0x50; // P
        bytes[2] = 0x44; // D
        bytes[3] = 0x46; // F
        bytes[4] = 0x2D; // -
        return bytes;
    }

    /// <summary>
    /// Creates a minimal valid DOCX (PK/ZIP header) byte array.
    /// </summary>
    public static byte[] MakeDocxBytes(int extraBytes = 100)
    {
        var bytes = new byte[4 + extraBytes];
        bytes[0] = 0x50; // P
        bytes[1] = 0x4B; // K
        bytes[2] = 0x03;
        bytes[3] = 0x04;
        return bytes;
    }

    /// <summary>
    /// Creates a minimal valid PNG byte array: the 8-byte PNG magic signature.
    /// </summary>
    public static byte[] MakePngBytes(int extraBytes = 100)
    {
        var bytes = new byte[8 + extraBytes];
        bytes[0] = 0x89;
        bytes[1] = 0x50; // P
        bytes[2] = 0x4E; // N
        bytes[3] = 0x47; // G
        bytes[4] = 0x0D;
        bytes[5] = 0x0A;
        bytes[6] = 0x1A;
        bytes[7] = 0x0A;
        return bytes;
    }

    /// <summary>
    /// Creates bytes that look like an executable (MZ header) — an unsupported type.
    /// </summary>
    public static byte[] MakeExeBytes(int extraBytes = 100)
    {
        var bytes = new byte[2 + extraBytes];
        bytes[0] = 0x4D; // M
        bytes[1] = 0x5A; // Z
        return bytes;
    }

    /// <summary>
    /// Builds a multipart upload request for the POST api/curriculum/documents endpoint.
    /// </summary>
    public static MultipartFormDataContent BuildUploadForm(
        byte[] fileBytes,
        string fileName,
        string contentType,
        int gradeId = 4,
        int subjectId = 1,
        int language = 0,     // ContentLanguage.Arabic = 0
        string? country = "EG")
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);
        content.Add(new StringContent(gradeId.ToString()), "gradeId");
        content.Add(new StringContent(subjectId.ToString()), "subjectId");
        content.Add(new StringContent(language.ToString()), "language");
        if (country is not null)
            content.Add(new StringContent(country), "country");
        return content;
    }

    // ── Case-insensitive JSON property lookup (matches both camelCase and PascalCase) ────────────

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

    /// <summary>
    /// Asserts the standard BaseResponse&lt;T&gt; envelope keys: statusCode, successed, data.
    /// </summary>
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
        SendAsync(HttpClient client, HttpMethod method, string url, HttpContent? body = null, string? bearer = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (bearer is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        if (body is not null)
            request.Content = body;

        var response = await client.SendAsync(request);
        var bodyStr  = await response.Content.ReadAsStringAsync();

        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            try { root = JsonDocument.Parse(bodyStr).RootElement; }
            catch { /* non-JSON body (e.g. 401 plain text) — root stays default */ }
        }
        return (response, root, bodyStr);
    }
}

// ─── BL-01 HTTP Integration Tests ─────────────────────────────────────────────

/// <summary>
/// BL-01 HTTP integration tests for POST/GET api/curriculum/documents.
///
/// Coverage:
///   BL01-HT-01 — Upload happy path (PDF, admin JWT) → 201, Successed=true, envelope shape, doc row + PipelineJob row.
///   BL01-HT-02 — Atomicity: doc row and PipelineJob row appear together (both-or-neither).
///   BL01-HT-03 — Bad magic bytes (exe payload with PDF content-type) → 422, no DB row, no storage object.
///   BL01-HT-04 — Disallowed content-type header (exe) → 422, no DB row.
///   BL01-HT-05 — Auth: anonymous → 401 on all three endpoints.
///   BL01-HT-06 — Auth: authenticated non-admin (Student role) → 403 on all three endpoints.
///   BL01-HT-07 — Auth: Admin role → allowed (201/200).
///   BL01-HT-08 — List endpoint: admin → 200, paginated envelope (successed/currentPage/totalCount/pageSize), filter by gradeId.
///   BL01-HT-09 — Detail endpoint: existing id → 200, ObjectKey present; missing id → 404.
///   BL01-HT-10 — Oversize payload → 422 (handler size check at configured 1 MB limit).
///   BL01-HT-11 — Validation: gradeId ≤ 0 → 422 from ValidationBehavior.
///   BL01-HT-12 — DOCX upload happy path → 201, doc row, PipelineJob row.
/// </summary>
[Collection("BL01Http")]
public sealed class BL01_CurriculumDocumentEndpoint_Tests : IAsyncLifetime
{
    private const string UploadUrl = "api/curriculum/documents";

    private readonly BL01WebAppFactory _factory;
    private readonly HttpClient _client;

    public BL01_CurriculumDocumentEndpoint_Tests(BL01WebAppFactory factory)
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
    // BL01-HT-01: Upload happy path — valid small PDF, admin JWT → 201
    // =========================================================================

    /// <summary>
    /// AC1 + AC4 + AC5: admin uploads a valid PDF → HTTP 201, Successed=true, correct envelope
    /// shape, data.status = Received; exactly one CurriculumDocument row and one PipelineJob
    /// row (JobType='parse', Status='Pending', correct DocumentId) are written to the DB.
    /// </summary>
    [Fact(DisplayName = "BL01-HT-01: Admin uploads valid PDF → 201 + doc row + PipelineJob row")]
    public async Task HT01_AdminUpload_ValidPdf_Returns201_And_PersistsBothRows()
    {
        var adminJwt = BL01WebAppFactory.GenerateJwt(role: "Admin");
        var pdfBytes = Bl01TestHelpers.MakePdfBytes();
        var form = Bl01TestHelpers.BuildUploadForm(pdfBytes, "textbook.pdf", "application/pdf");

        var (response, root, body) =
            await Bl01TestHelpers.SendAsync(_client, HttpMethod.Post, UploadUrl, form, adminJwt);

        // ── HTTP 201 ──────────────────────────────────────────────────────────
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "valid admin upload must return 201 Created; body: {0}", body);

        // ── Envelope shape ────────────────────────────────────────────────────
        Bl01TestHelpers.AssertEnvelopeKeys(root, body);
        Bl01TestHelpers.GetProp(root, "successed", body).GetBoolean().Should().BeTrue(
            "Successed must be true on successful upload; body: {0}", body);

        // ── data payload ──────────────────────────────────────────────────────
        var data = Bl01TestHelpers.GetProp(root, "data", body);
        data.ValueKind.Should().NotBe(JsonValueKind.Null,
            "data must not be null on 201; body: {0}", body);

        // id must be a positive integer
        var docId = Bl01TestHelpers.GetProp(data, "id", body).GetInt32();
        docId.Should().BeGreaterThan(0, "uploaded document must get a positive id; body: {0}", body);

        // status must be Received (0)
        if (Bl01TestHelpers.TryGetProp(data, "status", out var statusEl))
        {
            // Status may be serialised as int (0 = Received) or string "Received"
            if (statusEl.ValueKind == JsonValueKind.Number)
                statusEl.GetInt32().Should().Be(0, "Status must be Received (0) on upload; body: {0}", body);
            else
                statusEl.GetString()!.Should().ContainAny("Received", "0");
        }

        // ObjectKey must NOT appear in the upload response DTO (security: only detail exposes it)
        Bl01TestHelpers.TryGetProp(data, "objectKey", out _).Should().BeFalse(
            "ObjectKey must NOT be exposed in the upload 201 response; body: {0}", body);

        // ── DB persistence: CurriculumDocument ────────────────────────────────
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CurriculumDbContext>();

        var docRow = await db.CurriculumDocuments.FirstOrDefaultAsync(d => d.Id == docId);
        docRow.Should().NotBeNull("CurriculumDocument row must be persisted; docId={0}", docId);
        docRow!.Status.Should().Be(
            Learnexia.Modules.Curriculum.Domain.Enums.DocumentStatus.Received,
            "document status must be Received immediately after upload");
        docRow.ObjectKey.Should().NotBeNullOrWhiteSpace("ObjectKey must be persisted");
        docRow.GradeId.Should().Be(4, "GradeId from form must be stored");
        docRow.SubjectId.Should().Be(1, "SubjectId from form must be stored");

        // ── DB persistence: PipelineJob ──────────────────────────────────────
        var jobRow = await db.PipelineJobs.FirstOrDefaultAsync(j => j.DocumentId == docId);
        jobRow.Should().NotBeNull("PipelineJob row must be written by the upload handler (AC5)");
        jobRow!.JobType.Should().Be("parse",
            "JobType must be 'parse' (string cross-process contract)");
        jobRow.Status.Should().Be("Pending",
            "Status must be 'Pending' (string cross-process contract)");
        jobRow.PayloadJson.Should().Contain("object_key",
            "PayloadJson must carry the object_key for the Python worker");
        jobRow.PayloadJson.Should().Contain("content_type",
            "PayloadJson must carry the content_type for the Python worker");
        jobRow.RetryCount.Should().Be(0, "RetryCount must default to 0 on a fresh job");
        jobRow.ClaimedAt.Should().BeNull("ClaimedAt must be null on a fresh Pending job");
        jobRow.CompletedAt.Should().BeNull("CompletedAt must be null on a fresh job");
    }

    // =========================================================================
    // BL01-HT-02: Atomicity — doc row and PipelineJob row appear together
    // =========================================================================

    /// <summary>
    /// AC5 (atomicity): the upload transaction writes both rows atomically.
    /// This test verifies the observable invariant: after a 201 response, the document count
    /// and pipeline-job count are both exactly +1, confirming the two writes committed together.
    /// (Full rollback path requires injecting a faulting storage or db — verified at schema level
    /// in BL01_CurriculumDocumentSchema_Tests; here we assert the positive path from the HTTP surface.)
    /// </summary>
    [Fact(DisplayName = "BL01-HT-02: Upload atomicity — doc and job rows appear together after 201")]
    public async Task HT02_Upload_Atomicity_BothRowsCreatedTogether()
    {
        var adminJwt = BL01WebAppFactory.GenerateJwt(role: "Admin");
        _factory.FakeStorage.Reset();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CurriculumDbContext>();

        var docCountBefore = await db.CurriculumDocuments.CountAsync();
        var jobCountBefore = await db.PipelineJobs.CountAsync();

        var pdfBytes = Bl01TestHelpers.MakePdfBytes();
        var form = Bl01TestHelpers.BuildUploadForm(pdfBytes, "atomic_test.pdf", "application/pdf", gradeId: 3, subjectId: 2);

        var (response, _, body) =
            await Bl01TestHelpers.SendAsync(_client, HttpMethod.Post, UploadUrl, form, adminJwt);

        response.StatusCode.Should().Be(HttpStatusCode.Created, "body: {0}", body);

        // Both must have incremented by exactly 1
        var docCountAfter = await db.CurriculumDocuments.CountAsync();
        var jobCountAfter = await db.PipelineJobs.CountAsync();

        (docCountAfter - docCountBefore).Should().Be(1,
            "exactly one CurriculumDocument row must be written per upload");
        (jobCountAfter - jobCountBefore).Should().Be(1,
            "exactly one PipelineJob row must be written per upload (AC5 DB-outbox)");

        // Both must reference the same document
        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<CurriculumDbContext>();

        var latestDoc = await db2.CurriculumDocuments
            .OrderByDescending(d => d.Id)
            .FirstAsync();
        var job = await db2.PipelineJobs
            .FirstOrDefaultAsync(j => j.DocumentId == latestDoc.Id);

        job.Should().NotBeNull(
            "PipelineJob must reference the same document inserted in this request (FK + atomicity)");
    }

    // =========================================================================
    // BL01-HT-03: Bad magic bytes → 422, no DB row, no storage object
    // =========================================================================

    /// <summary>
    /// AC2: an EXE payload with a PDF content-type header must be rejected by the magic-byte
    /// check → 422 UnprocessableEntity. No CurriculumDocument row must be created and the
    /// FakeStorage must not have received an upload call.
    /// </summary>
    [Fact(DisplayName = "BL01-HT-03: EXE bytes with PDF content-type → 422, no DB row, no storage")]
    public async Task HT03_ExeBytesWithPdfContentType_Returns422_NoDbRow_NoStorageUpload()
    {
        var adminJwt = BL01WebAppFactory.GenerateJwt(role: "Admin");
        _factory.FakeStorage.Reset();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CurriculumDbContext>();
        var countBefore = await db.CurriculumDocuments.CountAsync();
        var uploadsBefore = _factory.FakeStorage.UploadedKeys.Count;

        var exeBytes = Bl01TestHelpers.MakeExeBytes();
        // Spoof the content-type as PDF to test magic-byte catches the mismatch
        var form = Bl01TestHelpers.BuildUploadForm(exeBytes, "malicious.pdf", "application/pdf");

        var (response, root, body) =
            await Bl01TestHelpers.SendAsync(_client, HttpMethod.Post, UploadUrl, form, adminJwt);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "EXE bytes with PDF content-type must be rejected with 422; body: {0}", body);

        // Envelope
        if (root.ValueKind == JsonValueKind.Object)
        {
            Bl01TestHelpers.AssertEnvelopeKeys(root, body);
            Bl01TestHelpers.GetProp(root, "successed", body).GetBoolean().Should().BeFalse(
                "Successed must be false on magic-byte rejection; body: {0}", body);
        }

        // No DB row
        var countAfter = await db.CurriculumDocuments.CountAsync();
        countAfter.Should().Be(countBefore,
            "no CurriculumDocument row must be created when magic-bytes are invalid");

        // No storage upload
        _factory.FakeStorage.UploadedKeys.Count.Should().Be(uploadsBefore,
            "storage must not be called when magic-byte validation fails (upload ordered AFTER validation)");
    }

    // =========================================================================
    // BL01-HT-04: Disallowed content-type → 422, no DB row
    // =========================================================================

    /// <summary>
    /// AC2: a file with a content-type not in the allow-list (text/plain) must be rejected → 422.
    /// No DB row must be created.
    /// </summary>
    [Fact(DisplayName = "BL01-HT-04: Disallowed content-type (text/plain) → 422")]
    public async Task HT04_DisallowedContentType_Returns422_NoDbRow()
    {
        var adminJwt = BL01WebAppFactory.GenerateJwt(role: "Admin");
        _factory.FakeStorage.Reset();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CurriculumDbContext>();
        var countBefore = await db.CurriculumDocuments.CountAsync();

        // Use PDF bytes but an explicitly disallowed content-type
        var pdfBytes = Bl01TestHelpers.MakePdfBytes();
        var form = Bl01TestHelpers.BuildUploadForm(pdfBytes, "readme.txt", "text/plain");

        var (response, root, body) =
            await Bl01TestHelpers.SendAsync(_client, HttpMethod.Post, UploadUrl, form, adminJwt);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "text/plain must be rejected by the allow-list check → 422; body: {0}", body);

        if (root.ValueKind == JsonValueKind.Object && Bl01TestHelpers.TryGetProp(root, "successed", out var succEl))
            succEl.GetBoolean().Should().BeFalse("Successed=false on allow-list rejection; body: {0}", body);

        var countAfter = await db.CurriculumDocuments.CountAsync();
        countAfter.Should().Be(countBefore, "no DB row when content-type rejected");
    }

    // =========================================================================
    // BL01-HT-05: Anonymous → 401 on all three endpoints
    // =========================================================================

    /// <summary>
    /// AC3: the AdminOnly policy must reject anonymous requests with 401 on all three endpoints.
    /// </summary>
    [Theory(DisplayName = "BL01-HT-05: Anonymous → 401 on all endpoints")]
    [InlineData("POST", "api/curriculum/documents")]
    [InlineData("GET",  "api/curriculum/documents")]
    [InlineData("GET",  "api/curriculum/documents/999")]
    public async Task HT05_Anonymous_Returns401_AllEndpoints(string method, string url)
    {
        var httpMethod = method == "POST" ? HttpMethod.Post : HttpMethod.Get;

        HttpContent? content = null;
        if (method == "POST")
        {
            // Provide a form so the model binding doesn't fail before auth runs
            var pdfBytes = Bl01TestHelpers.MakePdfBytes();
            content = Bl01TestHelpers.BuildUploadForm(pdfBytes, "test.pdf", "application/pdf");
        }

        var (response, _, body) =
            await Bl01TestHelpers.SendAsync(_client, httpMethod, url, content, bearer: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "anonymous request to {0} {1} must return 401; body: {2}", method, url, body);
    }

    // =========================================================================
    // BL01-HT-06: Non-admin (Student role) → 403 on all three endpoints
    // =========================================================================

    /// <summary>
    /// AC3: an authenticated user with a Student role must be denied with 403 on all endpoints.
    /// This is the critical authz check (AdminOnly policy enforcement).
    /// </summary>
    [Theory(DisplayName = "BL01-HT-06: Non-admin (Student) → 403 on all endpoints")]
    [InlineData("POST", "api/curriculum/documents")]
    [InlineData("GET",  "api/curriculum/documents")]
    [InlineData("GET",  "api/curriculum/documents/999")]
    public async Task HT06_NonAdmin_Student_Returns403_AllEndpoints(string method, string url)
    {
        var studentJwt = BL01WebAppFactory.GenerateJwt(role: "Student");
        var httpMethod = method == "POST" ? HttpMethod.Post : HttpMethod.Get;

        HttpContent? content = null;
        if (method == "POST")
        {
            var pdfBytes = Bl01TestHelpers.MakePdfBytes();
            content = Bl01TestHelpers.BuildUploadForm(pdfBytes, "test.pdf", "application/pdf");
        }

        var (response, _, body) =
            await Bl01TestHelpers.SendAsync(_client, httpMethod, url, content, studentJwt);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Student role must be forbidden (403) from {0} {1}; body: {2}", method, url, body);
    }

    // =========================================================================
    // BL01-HT-07: Parent role → 403 (not just Student — any non-admin role denied)
    // =========================================================================

    /// <summary>
    /// AC3 (extended): a Parent-role JWT must also be denied 403. Confirms policy requires
    /// specifically Admin or SuperAdmin, not just any authenticated user.
    /// </summary>
    [Fact(DisplayName = "BL01-HT-07: Parent role → 403 on upload endpoint")]
    public async Task HT07_ParentRole_Returns403()
    {
        var parentJwt = BL01WebAppFactory.GenerateJwt(role: "Parent");
        var pdfBytes = Bl01TestHelpers.MakePdfBytes();
        var form = Bl01TestHelpers.BuildUploadForm(pdfBytes, "test.pdf", "application/pdf");

        var (response, _, body) =
            await Bl01TestHelpers.SendAsync(_client, HttpMethod.Post, UploadUrl, form, parentJwt);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Parent role must be forbidden (403) from the upload endpoint; body: {0}", body);
    }

    // =========================================================================
    // BL01-HT-08: List endpoint — paginated envelope, filters work
    // =========================================================================

    /// <summary>
    /// AC6: GET api/curriculum/documents returns a paginated result with the correct envelope
    /// keys (successed, currentPage, totalCount, totalPages, pageSize, data array).
    /// Also verifies that the GradeId filter works: uploads to grade 7 then filters by grade 8
    /// and expects zero results.
    /// </summary>
    [Fact(DisplayName = "BL01-HT-08: List endpoint returns paged envelope; gradeId filter works")]
    public async Task HT08_List_ReturnsPaginatedEnvelope_GradeFilter_Works()
    {
        var adminJwt = BL01WebAppFactory.GenerateJwt(role: "Admin");

        // ── Upload a doc in grade 7 ──────────────────────────────────────────
        var pdfBytes = Bl01TestHelpers.MakePdfBytes();
        var form = Bl01TestHelpers.BuildUploadForm(pdfBytes, "grade7_doc.pdf", "application/pdf", gradeId: 7, subjectId: 1);
        var (uploadResp, _, uploadBody) =
            await Bl01TestHelpers.SendAsync(_client, HttpMethod.Post, UploadUrl, form, adminJwt);
        uploadResp.StatusCode.Should().Be(HttpStatusCode.Created, "setup upload failed; body: {0}", uploadBody);

        // ── List without filters ─────────────────────────────────────────────
        var (listResp, listRoot, listBody) =
            await Bl01TestHelpers.SendAsync(_client, HttpMethod.Get, $"{UploadUrl}?pageNumber=1&pageSize=10", bearer: adminJwt);

        listResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "list endpoint must return 200 for admin; body: {0}", listBody);

        if (listRoot.ValueKind == JsonValueKind.Object)
        {
            Bl01TestHelpers.AssertEnvelopeKeys(listRoot, listBody);
            Bl01TestHelpers.GetProp(listRoot, "successed", listBody).GetBoolean().Should().BeTrue(
                "Successed=true on successful list; body: {0}", listBody);

            // Paginated fields
            Bl01TestHelpers.TryGetProp(listRoot, "currentPage", out _).Should().BeTrue(
                "PaginatedResult must include currentPage; body: {0}", listBody);
            Bl01TestHelpers.TryGetProp(listRoot, "totalCount", out _).Should().BeTrue(
                "PaginatedResult must include totalCount; body: {0}", listBody);
            Bl01TestHelpers.TryGetProp(listRoot, "pageSize", out _).Should().BeTrue(
                "PaginatedResult must include pageSize; body: {0}", listBody);

            // data should be an array (could be the list or null if 0 docs in empty state)
            if (Bl01TestHelpers.TryGetProp(listRoot, "data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
            {
                dataEl.GetArrayLength().Should().BeGreaterThanOrEqualTo(1,
                    "at least one document (the one we just uploaded) must appear in the list");

                // Security: ObjectKey must NOT appear in any list DTO item
                foreach (var item in dataEl.EnumerateArray())
                    Bl01TestHelpers.TryGetProp(item, "objectKey", out _).Should().BeFalse(
                        "ObjectKey must not be in the list DTO (least-exposure principle)");
            }
        }

        // ── Filter by gradeId=8 — should return zero docs (we uploaded to grade 7) ────────────
        var (filteredResp, filteredRoot, filteredBody) =
            await Bl01TestHelpers.SendAsync(_client, HttpMethod.Get,
                $"{UploadUrl}?gradeId=8&pageNumber=1&pageSize=10", bearer: adminJwt);

        filteredResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "filtered list must still return 200 (empty is OK); body: {0}", filteredBody);

        if (filteredRoot.ValueKind == JsonValueKind.Object &&
            Bl01TestHelpers.TryGetProp(filteredRoot, "totalCount", out var tcEl))
        {
            tcEl.GetInt32().Should().Be(0,
                "filtering by gradeId=8 must return 0 docs when only gradeId=7 exists; body: {0}", filteredBody);
        }
    }

    // =========================================================================
    // BL01-HT-09: Detail endpoint — existing id → 200 with ObjectKey; missing → 404
    // =========================================================================

    /// <summary>
    /// AC6: GET api/curriculum/documents/{id} returns 200 with full detail including ObjectKey
    /// for an existing document; returns 404 for an unknown id.
    /// </summary>
    [Fact(DisplayName = "BL01-HT-09: Detail — known id → 200+ObjectKey; unknown id → 404")]
    public async Task HT09_Detail_KnownId_Returns200_WithObjectKey_UnknownId_Returns404()
    {
        var adminJwt = BL01WebAppFactory.GenerateJwt(role: "Admin");

        // Upload a document first
        var pdfBytes = Bl01TestHelpers.MakePdfBytes();
        var form = Bl01TestHelpers.BuildUploadForm(pdfBytes, "detail_test.pdf", "application/pdf");
        var (uploadResp, uploadRoot, uploadBody) =
            await Bl01TestHelpers.SendAsync(_client, HttpMethod.Post, UploadUrl, form, adminJwt);
        uploadResp.StatusCode.Should().Be(HttpStatusCode.Created, "setup upload failed; body: {0}", uploadBody);

        var docId = Bl01TestHelpers.GetProp(
            Bl01TestHelpers.GetProp(uploadRoot, "data", uploadBody), "id", uploadBody).GetInt32();

        // ── GET detail by known id ────────────────────────────────────────────
        var (detailResp, detailRoot, detailBody) =
            await Bl01TestHelpers.SendAsync(_client, HttpMethod.Get, $"{UploadUrl}/{docId}", bearer: adminJwt);

        detailResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "known-id GET must return 200; body: {0}", detailBody);

        if (detailRoot.ValueKind == JsonValueKind.Object)
        {
            Bl01TestHelpers.AssertEnvelopeKeys(detailRoot, detailBody);
            Bl01TestHelpers.GetProp(detailRoot, "successed", detailBody).GetBoolean().Should().BeTrue(
                "Successed=true on successful detail fetch; body: {0}", detailBody);

            var data = Bl01TestHelpers.GetProp(detailRoot, "data", detailBody);
            data.ValueKind.Should().NotBe(JsonValueKind.Null, "data must not be null; body: {0}", detailBody);

            // ObjectKey MUST appear in the detail DTO (admin-only endpoint — full detail)
            Bl01TestHelpers.TryGetProp(data, "objectKey", out var okEl).Should().BeTrue(
                "ObjectKey must be present in the admin-only detail DTO; body: {0}", detailBody);
            okEl.GetString().Should().NotBeNullOrWhiteSpace("ObjectKey must be a non-empty string");
        }

        // ── GET detail by unknown id → 404 ───────────────────────────────────
        var unknownId = 999999;
        var (notFoundResp, notFoundRoot, notFoundBody) =
            await Bl01TestHelpers.SendAsync(_client, HttpMethod.Get, $"{UploadUrl}/{unknownId}", bearer: adminJwt);

        notFoundResp.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "unknown-id GET must return 404; body: {0}", notFoundBody);

        if (notFoundRoot.ValueKind == JsonValueKind.Object)
        {
            Bl01TestHelpers.TryGetProp(notFoundRoot, "successed", out var nfSucc).Should().BeTrue();
            nfSucc.GetBoolean().Should().BeFalse("Successed=false on 404; body: {0}", notFoundBody);
        }
    }

    // =========================================================================
    // BL01-HT-10: Oversize payload → 422 from handler size check
    // =========================================================================

    /// <summary>
    /// AC2: the configured MaxFileSizeBytes (overridden to 1 MB in the test factory) is enforced
    /// by the handler. A payload slightly over 1 MB must be rejected with 422.
    ///
    /// Note: we override MaxFileSizeBytes to 1 MB in the test factory (see ConfigureAppConfiguration)
    /// to avoid allocating 100 MB in CI. This tests the handler's size check, not the Kestrel
    /// route limit. The route-level [RequestSizeLimit(100MB)] is a declared attribute on the controller
    /// action — its presence is verified by inspecting the controller source (see notes below).
    ///
    /// CI note: a true 100 MB test is impractical in most CI environments. The approach taken is:
    ///   1. Lower MaxFileSizeBytes to 1 MB in test configuration (factory override).
    ///   2. Send a ~1.1 MB valid-signature payload.
    ///   3. Assert 422 — confirming the handler size guard fires before storage is called.
    /// </summary>
    [Fact(DisplayName = "BL01-HT-10: 1.1 MB PDF (over 1 MB test cap) → 422 size rejection")]
    public async Task HT10_OversizePayload_Returns422()
    {
        var adminJwt = BL01WebAppFactory.GenerateJwt(role: "Admin");
        _factory.FakeStorage.Reset();

        // 1.1 MB — over the test-factory 1 MB cap
        var oversizeBytes = Bl01TestHelpers.MakePdfBytes(extraBytes: 1_100_000);
        var form = Bl01TestHelpers.BuildUploadForm(oversizeBytes, "oversize.pdf", "application/pdf");

        var (response, root, body) =
            await Bl01TestHelpers.SendAsync(_client, HttpMethod.Post, UploadUrl, form, adminJwt);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "oversize upload must return 422 (handler size guard); body: {0}", body);

        if (root.ValueKind == JsonValueKind.Object && Bl01TestHelpers.TryGetProp(root, "successed", out var succEl))
            succEl.GetBoolean().Should().BeFalse("Successed=false on size rejection; body: {0}", body);

        // Storage must not have been called
        _factory.FakeStorage.UploadedKeys.Should().BeEmpty(
            "storage must not be called when the handler size check fails");
    }

    // =========================================================================
    // BL01-HT-11: Metadata validation — gradeId ≤ 0 → 422 from ValidationBehavior
    // =========================================================================

    /// <summary>
    /// The <see cref="UploadCurriculumDocumentCommandValidator"/> enforces GradeId > 0.
    /// ValidationBehavior runs on ICommand — gradeId=0 must return 422.
    /// </summary>
    [Fact(DisplayName = "BL01-HT-11: gradeId=0 → 422 from ValidationBehavior")]
    public async Task HT11_GradeIdZero_Returns422_ValidationError()
    {
        var adminJwt = BL01WebAppFactory.GenerateJwt(role: "Admin");
        var pdfBytes = Bl01TestHelpers.MakePdfBytes();
        // gradeId = 0 is invalid
        var form = Bl01TestHelpers.BuildUploadForm(pdfBytes, "test.pdf", "application/pdf", gradeId: 0, subjectId: 1);

        var (response, root, body) =
            await Bl01TestHelpers.SendAsync(_client, HttpMethod.Post, UploadUrl, form, adminJwt);

        // ValidationBehavior returns 422 for ICommand<> validation failures
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "gradeId=0 must fail metadata validation → 422; body: {0}", body);
    }

    // =========================================================================
    // BL01-HT-12: DOCX upload happy path → 201
    // =========================================================================

    /// <summary>
    /// AC1: admin uploads a valid DOCX (PK/ZIP magic bytes + DOCX content-type) → 201.
    /// Verifies the DOCX file-type branch in CurriculumFileValidator.
    /// </summary>
    [Fact(DisplayName = "BL01-HT-12: Admin uploads valid DOCX → 201")]
    public async Task HT12_AdminUpload_ValidDocx_Returns201()
    {
        var adminJwt = BL01WebAppFactory.GenerateJwt(role: "Admin");
        var docxBytes = Bl01TestHelpers.MakeDocxBytes();
        var form = Bl01TestHelpers.BuildUploadForm(
            docxBytes,
            "worksheet.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            gradeId: 5,
            subjectId: 3);

        var (response, root, body) =
            await Bl01TestHelpers.SendAsync(_client, HttpMethod.Post, UploadUrl, form, adminJwt);

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "valid DOCX upload by admin must return 201; body: {0}", body);

        Bl01TestHelpers.AssertEnvelopeKeys(root, body);
        Bl01TestHelpers.GetProp(root, "successed", body).GetBoolean().Should().BeTrue(
            "Successed=true on DOCX upload; body: {0}", body);

        // Verify PipelineJob was created
        var data = Bl01TestHelpers.GetProp(root, "data", body);
        var docId = Bl01TestHelpers.GetProp(data, "id", body).GetInt32();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CurriculumDbContext>();

        var job = await db.PipelineJobs.FirstOrDefaultAsync(j => j.DocumentId == docId);
        job.Should().NotBeNull("PipelineJob must be created for DOCX upload");
        job!.JobType.Should().Be("parse");
        job.Status.Should().Be("Pending");
    }

    // =========================================================================
    // BL01-HT-13: SuperAdmin role → also allowed (AdminOnly policy covers both)
    // =========================================================================

    /// <summary>
    /// AC3: the AdminOnly policy requires "Admin" OR "SuperAdmin". Verify SuperAdmin can upload.
    /// </summary>
    [Fact(DisplayName = "BL01-HT-13: SuperAdmin role → also allowed (201)")]
    public async Task HT13_SuperAdminRole_Returns201()
    {
        var superAdminJwt = BL01WebAppFactory.GenerateJwt(role: "SuperAdmin");
        var pdfBytes = Bl01TestHelpers.MakePdfBytes();
        var form = Bl01TestHelpers.BuildUploadForm(pdfBytes, "superadmin_test.pdf", "application/pdf");

        var (response, _, body) =
            await Bl01TestHelpers.SendAsync(_client, HttpMethod.Post, UploadUrl, form, superAdminJwt);

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "SuperAdmin role must satisfy the AdminOnly policy → 201; body: {0}", body);
    }

    // =========================================================================
    // BL01-HT-14: PNG upload happy path → 201
    // =========================================================================

    /// <summary>
    /// AC1: admin uploads a valid PNG (PNG magic bytes + image/png content-type) → 201.
    /// Verifies the image file-type branch in CurriculumFileValidator.
    /// </summary>
    [Fact(DisplayName = "BL01-HT-14: Admin uploads valid PNG → 201")]
    public async Task HT14_AdminUpload_ValidPng_Returns201()
    {
        var adminJwt = BL01WebAppFactory.GenerateJwt(role: "Admin");
        var pngBytes = Bl01TestHelpers.MakePngBytes();
        var form = Bl01TestHelpers.BuildUploadForm(
            pngBytes, "diagram.png", "image/png", gradeId: 2, subjectId: 4);

        var (response, _, body) =
            await Bl01TestHelpers.SendAsync(_client, HttpMethod.Post, UploadUrl, form, adminJwt);

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "valid PNG upload by admin must return 201; body: {0}", body);
    }

    // =========================================================================
    // BL01-HT-15: Storage failure → 500 (not 422) from handler's ServerError path
    // =========================================================================

    /// <summary>
    /// When IStorageService.UploadFileAsync returns failure, the handler returns
    /// ServerError&lt;T&gt; → HTTP 500. Verifies the storage-error path is NOT silent.
    /// This is distinct from a validation rejection (422).
    /// </summary>
    [Fact(DisplayName = "BL01-HT-15: Storage failure → 500")]
    public async Task HT15_StorageFailure_Returns500()
    {
        var adminJwt = BL01WebAppFactory.GenerateJwt(role: "Admin");
        _factory.FakeStorage.ShouldFailUpload = true;

        var pdfBytes = Bl01TestHelpers.MakePdfBytes();
        var form = Bl01TestHelpers.BuildUploadForm(pdfBytes, "storage_fail.pdf", "application/pdf");

        try
        {
            var (response, root, body) =
                await Bl01TestHelpers.SendAsync(_client, HttpMethod.Post, UploadUrl, form, adminJwt);

            response.StatusCode.Should().Be(HttpStatusCode.InternalServerError,
                "storage failure must return 500 (ServerError path in handler); body: {0}", body);

            if (root.ValueKind == JsonValueKind.Object && Bl01TestHelpers.TryGetProp(root, "successed", out var succEl))
                succEl.GetBoolean().Should().BeFalse("Successed=false on storage error; body: {0}", body);
        }
        finally
        {
            _factory.FakeStorage.ShouldFailUpload = false;
        }
    }
}
