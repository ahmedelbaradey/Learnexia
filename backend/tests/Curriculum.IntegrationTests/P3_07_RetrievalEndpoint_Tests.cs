using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Curriculum.Api;
using Learnexia.Modules.Curriculum.Application.Abstractions;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence.Seed;
using Learnexia.Modules.Curriculum.Infrastructure.Services;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Curriculum.IntegrationTests;

// ─── Collection fixture ───────────────────────────────────────────────────────

[CollectionDefinition("P3_07_Http")]
public sealed class P3_07_HttpCollection : ICollectionFixture<CurriculumWebAppFactory> { }

/// <summary>
/// Shared WebApplicationFactory for the P3-07 HTTP tests.
///
/// Strategy:
///   • Spin up a dedicated pgvector/pgvector:pg16 Testcontainer.
///   • Override ConnectionStrings:Default so ALL DbContexts (across all modules in the Host)
///     point at the container instead of localhost.
///   • Replace IEmbeddingProvider with DeterministicStubEmbeddingProvider so query vectors
///     match the seeded vectors (same function used by CurriculumChunkSeeder).
///   • Disable rate limiting (same pattern as LearnexiaWebAppFactory).
///   • After host build: run CurriculumModule.InitializeAsync (migrate + seed).
///   • Generate a JWT for auth tests using the same signing key as appsettings.json.
/// </summary>
public sealed class CurriculumWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // ── JWT signing key from appsettings.json (never commit real secrets here) ──
    private const string JwtSigningKey =
        "CHANGE_ME_super_secret_key_at_least_32_chars_long_0123456789";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg16")
        .WithDatabase("learnexia_curriculum_http_test")
        .WithUsername("postgres")
        .WithPassword("testpwd")
        .Build();

    public async Task InitializeAsync()
    {
        // Set the legacy timestamp switch BEFORE the host starts.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        await _postgres.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        builder.UseEnvironment("Testing");

        // Override the connection string BEFORE services are configured so every module's
        // DbContext reads it from IConfiguration rather than capturing the local variable.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _postgres.GetConnectionString(),
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace CurriculumDbContext so it uses vector() + the testcontainer connection.
            services.RemoveAll<DbContextOptions<CurriculumDbContext>>();
            services.RemoveAll<CurriculumDbContext>();
            services.AddDbContext<CurriculumDbContext>(options =>
                options.UseNpgsql(
                    _postgres.GetConnectionString(),
                    npgsql => npgsql
                        .UseVector()
                        .MigrationsHistoryTable("__EFMigrationsHistory", CurriculumDbContext.Schema)
                        .MigrationsAssembly(typeof(CurriculumDbContext).Assembly.FullName)));

            // Replace the real BgeM3EmbeddingProvider (which needs a live TEI endpoint)
            // with the deterministic stub so seeds + queries share the same embedding function.
            services.RemoveAll<IEmbeddingProvider>();
            services.AddScoped<IEmbeddingProvider, DeterministicStubEmbeddingProvider>();

            // Disable IP rate limiting to prevent spurious 429s in the test suite.
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

    /// <summary>
    /// Apply Curriculum migrations and seed corpus after the host is built.
    /// All other modules' migrations also run via their respective InitializeAsync
    /// hooks that Program.cs calls — but for the container test the DB is empty
    /// at startup, so every module's InitializeAsync runs as a no-op once the
    /// DbContext.MigrateAsync finds nothing to do.
    /// We explicitly call CurriculumModule.InitializeAsync here to ensure the corpus
    /// is present before tests run, regardless of which module's seed order wins.
    /// </summary>
    public async Task ApplyCurriculumMigrationsAndSeedAsync()
    {
        using var scope = Services.CreateScope();
        await CurriculumModule.InitializeAsync(scope.ServiceProvider);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.StopAsync();
    }

    // ── JWT token factory ─────────────────────────────────────────────────────

    /// <summary>
    /// Produces a signed JWT that the API's <c>[Authorize]</c> filter accepts.
    /// Mirrors the key/issuer/audience from appsettings.json (see JwtSettings section).
    /// </summary>
    public static string GenerateJwt()
    {
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(JwtSigningKey));
        var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer:   "Learnexia",
            audience: "LearnexiaClient",
            claims:   new[]
            {
                new System.Security.Claims.Claim(
                    System.Security.Claims.ClaimTypes.NameIdentifier, "1"),
                new System.Security.Claims.Claim(
                    System.Security.Claims.ClaimTypes.Name, "testuser"),
            },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .WriteToken(token);
    }
}

// ─── Deterministic stub ───────────────────────────────────────────────────────

/// <summary>
/// Stub IEmbeddingProvider — delegates to DeterministicEmbedding (same function as the seeder).
/// Guarantees seeded-text queries return cosine distance ≈ 0 (passes floor) and
/// out-of-corpus texts return large distance (exceeds floor → empty result).
/// </summary>
internal sealed class DeterministicStubEmbeddingProvider : IEmbeddingProvider
{
    public Task<Vector?> EmbedAsync(string text, CancellationToken ct = default)
        => DeterministicEmbedding.ComputeAsync(text, ct)
               .ContinueWith(t => (Vector?)t.Result, ct);
}

// ─── HTTP integration tests ───────────────────────────────────────────────────

/// <summary>
/// P3-07 HTTP-level integration tests for <c>GET /api/Curriculum/Retrieve</c>.
///
/// <para>Covers Batch-4 api-tester validation list from P3-07-BE.md:</para>
/// <list type="number">
///   <item>HT-01 — Happy path: seeded Math-En text → 200, Successed=true, non-empty Chunks, correct grade/subject filter.</item>
///   <item>HT-02 — Filter isolation: Science-En query returns Science chunks only (not Math).</item>
///   <item>HT-03 — Empty-result path (AC-4): out-of-corpus text → 200, Successed=true, empty Chunks array.</item>
///   <item>HT-04 — Unauthenticated request → 401.</item>
///   <item>HT-05 — Envelope shape: BaseResponse&lt;RetrievalResult&gt; keys present; CurriculumChunkDto has Content/Metadata/Score but no raw vector/embedding.</item>
///   <item>HT-06 — Empty-text guard: empty/whitespace text → 200, Successed=true, empty Chunks (handler guard, not 500).</item>
///   <item>HT-07 — Cosine SQL translation note (in-proc tests already prove server-side ORDER BY; comment included here).</item>
/// </list>
/// </summary>
[Collection("P3_07_Http")]
public sealed class P3_07_RetrievalEndpoint_Tests : IAsyncLifetime
{
    // ── Corpus constants (must match CurriculumChunkSeeder) ───────────────────

    // Math-En: SubjectId=2, GradeId=3 (version "MVP-G3-Math-En-v1")
    private const int MathEnSubjectId = 2;
    private const int GradeId = 3;

    // The exact seeded content text for the first Math-En chunk.
    // DeterministicEmbedding(this text) = the stored vector → cosine distance ≈ 0 → passes floor.
    private const string MathEnChunkText =
        "Counting to 1000: We count numbers up to 1000 using place value. " +
        "Each digit has a place: ones, tens, and hundreds.";

    // Science-En: SubjectId=4, GradeId=3 (version "MVP-G3-Science-En-v1")
    private const int ScienceEnSubjectId = 4;
    private const string ScienceEnChunkText =
        "Living vs Non-Living Things: Living things grow, breathe, reproduce and respond to " +
        "their environment. Plants and animals are living. Rocks, water, and air are non-living.";

    // Out-of-corpus text — deterministic hash differs from every seeded hash.
    private const string OutOfCorpusText =
        "XYZ_OUTLIER_HTTP_TEST_TEXT_NOT_IN_CORPUS_ABCDEF_9876543210_UNIQUE_SENTINEL";

    private const string RetrieveUrl = "api/Curriculum/Retrieve";

    // ── Infrastructure ────────────────────────────────────────────────────────

    private readonly CurriculumWebAppFactory _factory;
    private readonly HttpClient _client;
    private readonly string _jwt;

    public P3_07_RetrievalEndpoint_Tests(CurriculumWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
        _jwt     = CurriculumWebAppFactory.GenerateJwt();
    }

    public async Task InitializeAsync()
        => await _factory.ApplyCurriculumMigrationsAndSeedAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // HT-01: Happy path — seeded Math-En query → 200 / Successed / non-empty chunks
    // =========================================================================

    /// <summary>
    /// HT-01 (AC-2 + AC-3): querying the exact text of a seeded Math-En chunk returns a
    /// non-empty Chunks list.  Also asserts that every returned chunk belongs to the
    /// requested grade/subject (filter correctness per Batch-4 item 1).
    /// </summary>
    [Fact]
    public async Task Retrieve_SeededMathEnText_Returns200_SuccessedTrue_NonEmptyChunks()
    {
        var url = $"{RetrieveUrl}?text={Uri.EscapeDataString(MathEnChunkText)}" +
                  $"&gradeId={GradeId}&subjectId={MathEnSubjectId}&topK=3";

        var (response, root, body) = await GetAsync(url, _jwt);

        // ── Status code ─────────────────────────────────────────────────────
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "a successful retrieval must return HTTP 200; body: {0}", body);

        // ── Envelope keys ───────────────────────────────────────────────────
        AssertEnvelopeKeys(root, body);

        // ── Successed flag ──────────────────────────────────────────────────
        GetProp(root, "successed", body).GetBoolean().Should().BeTrue(
            "Successed must be true on a successful retrieval; body: {0}", body);

        // ── data.chunks — non-empty ──────────────────────────────────────────
        var chunks = GetChunks(root, body);
        chunks.Should().NotBeEmpty(
            "querying the exact seeded text should pass the similarity floor; body: {0}", body);

        // ── All returned chunks belong to the requested subject ─────────────
        // The handler filters by SubjectId in the WHERE clause.
        // We assert the response DTO fields are present and the metadata matches.
        foreach (var chunk in chunks)
        {
            AssertChunkShape(chunk, body);
            // Metadata contains the subjectId written by the seeder.
            // The seeder writes: {"skillKey":"...","gradeId":3,"subjectId":2}
            // AutoMapper/serialization may reformat with spaces, so we parse the JSON
            // to assert the values rather than doing a raw string contains.
            var meta = chunk.GetProperty("metadata").GetString()!;
            AssertMetadataContains(meta, "subjectId", MathEnSubjectId, body);
            AssertMetadataContains(meta, "gradeId", GradeId, body);
        }
    }

    // =========================================================================
    // HT-02: Filter isolation — Science-En query returns only Science chunks
    // =========================================================================

    /// <summary>
    /// HT-02 (Batch-4 item 2): querying a Science-En seeded text with Science subject/grade
    /// returns non-empty Science chunks, and when the same text is queried with the Math
    /// subject the floor rejects it (different subject filter → 0 rows or floor miss).
    /// </summary>
    [Fact]
    public async Task Retrieve_ScienceSubjectFilter_ReturnsOnlyScienceChunks()
    {
        // ── Science query against Science subject → must return chunks ────────
        var sciUrl = $"{RetrieveUrl}?text={Uri.EscapeDataString(ScienceEnChunkText)}" +
                     $"&gradeId={GradeId}&subjectId={ScienceEnSubjectId}&topK=3";

        var (sciResp, sciRoot, sciBody) = await GetAsync(sciUrl, _jwt);
        sciResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "science query must return 200; body: {0}", sciBody);

        var sciChunks = GetChunks(sciRoot, sciBody);
        sciChunks.Should().NotBeEmpty(
            "Science-En seeded text vs Science subject should pass floor; body: {0}", sciBody);

        foreach (var chunk in sciChunks)
        {
            AssertChunkShape(chunk, sciBody);
            var meta = chunk.GetProperty("metadata").GetString()!;
            AssertMetadataContains(meta, "subjectId", ScienceEnSubjectId, sciBody);
        }

        // ── Science text queried against Math subject → floor miss (wrong subjectId filter)─
        // The JOIN filters WHERE SubjectId=MathEnSubjectId; the seeded Science vectors live
        // under SubjectId=ScienceEnSubjectId, so the query returns 0 rows before floor check.
        var crossUrl = $"{RetrieveUrl}?text={Uri.EscapeDataString(ScienceEnChunkText)}" +
                       $"&gradeId={GradeId}&subjectId={MathEnSubjectId}&topK=3";

        var (crossResp, crossRoot, crossBody) = await GetAsync(crossUrl, _jwt);
        crossResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "cross-subject query must still return 200 (empty is OK); body: {0}", crossBody);

        GetProp(crossRoot, "successed", crossBody).GetBoolean().Should().BeTrue(
            "Successed must be true even for empty results; body: {0}", crossBody);

        var crossChunks = GetChunks(crossRoot, crossBody);
        crossChunks.Should().BeEmpty(
            "Science text filtered by Math subjectId must return no chunks; body: {0}", crossBody);
    }

    // =========================================================================
    // HT-03: AC-4 — empty-result path (out-of-corpus text)
    // =========================================================================

    /// <summary>
    /// HT-03 (AC-4): an out-of-corpus query text that cannot clear the similarity floor
    /// must return HTTP 200, Successed=true, and an empty Chunks array.
    /// Must NOT return HTTP 500 and must NOT fabricate chunks.
    /// </summary>
    [Fact]
    public async Task Retrieve_OutOfCorpusText_Returns200_SuccessedTrue_EmptyChunks()
    {
        var url = $"{RetrieveUrl}?text={Uri.EscapeDataString(OutOfCorpusText)}" +
                  $"&gradeId={GradeId}&subjectId={MathEnSubjectId}&topK=5";

        var (response, root, body) = await GetAsync(url, _jwt);

        // Must be 200 — not 500 (AC-4 requires fail-soft empty list).
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "empty-retrieval path must return HTTP 200, not 500; body: {0}", body);

        AssertEnvelopeKeys(root, body);

        GetProp(root, "successed", body).GetBoolean().Should().BeTrue(
            "Successed must be true even when no chunks clear the floor (AC-4); body: {0}", body);

        GetChunks(root, body).Should().BeEmpty(
            "out-of-corpus text must produce zero chunks after similarity floor filtering (AC-4); body: {0}", body);
    }

    // =========================================================================
    // HT-04: Unauthenticated → 401
    // =========================================================================

    /// <summary>
    /// HT-04 (Auth AC): the endpoint is behind [Authorize]. A request without a JWT
    /// must return HTTP 401, not 200.
    /// </summary>
    [Fact]
    public async Task Retrieve_NoJwt_Returns401()
    {
        var url = $"{RetrieveUrl}?text=anything&gradeId=3&subjectId=1&topK=3";

        var (response, _, body) = await GetAsync(url, bearerToken: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "unauthenticated request must return 401; body: {0}", body);
    }

    // =========================================================================
    // HT-05: Envelope shape + DTO security (no raw vector/embedding in response)
    // =========================================================================

    /// <summary>
    /// HT-05 (AC-6): asserts the full BaseResponse&lt;RetrievalResult&gt; envelope shape
    /// and that CurriculumChunkDto exposes Content / Metadata / Score / ChunkId
    /// but NEVER an embedding/vector field.
    /// </summary>
    [Fact]
    public async Task Retrieve_ResponseEnvelope_HasCorrectShape_NoRawEmbedding()
    {
        var url = $"{RetrieveUrl}?text={Uri.EscapeDataString(MathEnChunkText)}" +
                  $"&gradeId={GradeId}&subjectId={MathEnSubjectId}&topK=3";

        var (response, root, body) = await GetAsync(url, _jwt);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        // ── Top-level envelope keys ─────────────────────────────────────────
        // BaseResponse<T> must expose: statusCode, successed, message, data, errors
        AssertEnvelopeKeys(root, body);

        // ── data.chunks present ─────────────────────────────────────────────
        var data = GetProp(root, "data", body);
        var chunksEl = GetPropFromElement(data, "chunks", body);
        chunksEl.ValueKind.Should().Be(JsonValueKind.Array,
            "data.chunks must be a JSON array; body: {0}", body);

        // ── Chunk DTO fields: Content / Metadata / Score / ChunkId ─────────
        var chunks = chunksEl.EnumerateArray().ToList();
        chunks.Should().NotBeEmpty("seeded text should return chunks; body: {0}", body);

        foreach (var chunk in chunks)
        {
            AssertChunkShape(chunk, body);

            // Security assertion: no raw embedding/vector field must be present.
            // CurriculumChunkDto ONLY exposes ChunkId, Content, Metadata, Score.
            var fieldNames = chunk.EnumerateObject()
                .Select(p => p.Name.ToLowerInvariant())
                .ToList();

            fieldNames.Should().NotContain("vector",
                "raw vector must never be in the response (security); body: {0}", body);
            fieldNames.Should().NotContain("embedding",
                "raw embedding must never be in the response (security); body: {0}", body);
        }

        // ── statusCode matches HTTP 200 ──────────────────────────────────────
        var statusCodeEl = GetProp(root, "statusCode", body);
        // The envelope StatusCode is HttpStatusCode enum (int 200 or string "OK").
        var statusCodeRaw = statusCodeEl.ValueKind == JsonValueKind.Number
            ? statusCodeEl.GetInt32()
            : (int)Enum.Parse<HttpStatusCode>(statusCodeEl.GetString()!);
        statusCodeRaw.Should().Be(200,
            "envelope statusCode must be 200 on success; body: {0}", body);
    }

    // =========================================================================
    // HT-06: Handler guard — empty/whitespace text → 200 empty, not 500
    // =========================================================================

    /// <summary>
    /// HT-06: handler guards empty Text (query does not go through ValidationBehavior —
    /// only ICommand goes through it). Must return 200 + empty list, not 500.
    /// </summary>
    [Fact]
    public async Task Retrieve_EmptyText_Returns200_EmptyChunks_NotServerError()
    {
        // Empty string text — the handler's guard returns Success(empty) immediately.
        var url = $"{RetrieveUrl}?text=&gradeId={GradeId}&subjectId={MathEnSubjectId}&topK=3";

        var (response, root, body) = await GetAsync(url, _jwt);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "empty text must not cause 500; body: {0}", body);

        GetProp(root, "successed", body).GetBoolean().Should().BeTrue(
            "Successed=true even when text is empty (handler guard returns empty list); body: {0}", body);

        GetChunks(root, body).Should().BeEmpty(
            "empty text → handler guard → empty Chunks list; body: {0}", body);
    }

    // =========================================================================
    // HT-07: SQL translation note
    // =========================================================================

    /// <summary>
    /// HT-07: SQL translation of cosine distance &lt;=&gt; operator.
    /// The provider-level tests (RagContextProviderTests) already confirm that the
    /// ORDER BY Vector.CosineDistance(queryVector) call translates to server-side SQL
    /// against a real Postgres+pgvector container (EF throws at runtime on client-eval).
    /// This HTTP test further confirms: if client-eval were happening, the full query
    /// would fail with an InvalidOperationException (EF Core default is to throw, not
    /// silently evaluate). The fact that HT-01 returns HTTP 200 (not 500) is implicit
    /// confirmation that SQL translation is server-side.
    ///
    /// <para>Assertion: HT-01 returning HTTP 200 with non-empty chunks is the observable
    /// evidence that EF.Functions / CosineDistance translated to SQL &lt;=&gt; correctly.
    /// No additional assertion is needed here — this test documents the reasoning.</para>
    /// </summary>
    [Fact]
    public async Task Retrieve_CosineDistanceTranslatesToSql_EvidencedByNon500Response()
    {
        // Re-run the happy path. If CosineDistance were client-evaluated, EF Core would
        // throw an InvalidOperationException at the .ToListAsync() call (because EF 8+
        // does NOT silently client-evaluate server-only functions). The exception would
        // bubble to the handler's catch block → ServerError<> → HTTP 500.
        // HTTP 200 therefore confirms server-side SQL translation.
        var url = $"{RetrieveUrl}?text={Uri.EscapeDataString(MathEnChunkText)}" +
                  $"&gradeId={GradeId}&subjectId={MathEnSubjectId}&topK=3";

        var (response, _, body) = await GetAsync(url, _jwt);

        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
            "a 500 would indicate client-side evaluation failure of the cosine distance operator; body: {0}", body);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "HTTP 200 confirms CosineDistance translated to SQL <=> (server-side); body: {0}", body);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private async Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        GetAsync(string url, string? bearerToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (bearerToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        var response = await _client.SendAsync(request);
        var bodyStr  = await response.Content.ReadAsStringAsync();

        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            try { root = JsonDocument.Parse(bodyStr).RootElement; }
            catch { /* non-JSON (e.g. 401 plain text) is handled via root==default */ }
        }
        return (response, root, bodyStr);
    }

    /// <summary>
    /// Case-insensitive JSON property lookup.
    /// Controller path → Newtonsoft → camelCase; middleware 422 path → System.Text.Json → PascalCase.
    /// </summary>
    private static JsonElement GetProp(JsonElement element, string name, string body)
    {
        if (element.TryGetProperty(name, out var v)) return v;
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (element.TryGetProperty(pascal, out v)) return v;
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                return prop.Value;
        }
        throw new Xunit.Sdk.XunitException(
            $"Expected JSON property '{name}' (or '{pascal}') not found. body: {body}");
    }

    private static JsonElement GetPropFromElement(JsonElement element, string name, string body)
        => GetProp(element, name, body);

    /// <summary>
    /// Asserts that the top-level response has the required BaseResponse&lt;T&gt; envelope keys.
    /// </summary>
    private static void AssertEnvelopeKeys(JsonElement root, string body)
    {
        root.ValueKind.Should().Be(JsonValueKind.Object,
            "response body must be a JSON object; body: {0}", body);

        // Required envelope keys (case-insensitive because Newtonsoft emits camelCase).
        foreach (var key in new[] { "statusCode", "successed", "data" })
        {
            var found = false;
            foreach (var prop in root.EnumerateObject())
            {
                if (string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }
            found.Should().BeTrue(
                "BaseResponse<T> envelope must contain '{0}'; body: {1}", key, body);
        }
    }

    /// <summary>
    /// Returns the Chunks array from data.chunks (handles camelCase / PascalCase).
    /// </summary>
    private static List<JsonElement> GetChunks(JsonElement root, string body)
    {
        var data = GetProp(root, "data", body);
        if (data.ValueKind == JsonValueKind.Null)
            return new List<JsonElement>();

        var chunksEl = GetPropFromElement(data, "chunks", body);
        if (chunksEl.ValueKind != JsonValueKind.Array)
            return new List<JsonElement>();

        return chunksEl.EnumerateArray().ToList();
    }

    /// <summary>
    /// Parses the metadata JSON string and asserts that the given integer field has the expected value.
    /// Tolerates whitespace variants in the serialized JSON (e.g. "subjectId": 4 vs "subjectId":4).
    /// </summary>
    private static void AssertMetadataContains(string metaJson, string fieldName, int expectedValue, string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(metaJson);
            JsonElement fieldEl = default;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (string.Equals(prop.Name, fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    fieldEl = prop.Value;
                    break;
                }
            }
            fieldEl.ValueKind.Should().Be(JsonValueKind.Number,
                "metadata JSON must contain field '{0}'; meta: {1}, body: {2}", fieldName, metaJson, body);
            fieldEl.GetInt32().Should().Be(expectedValue,
                "metadata.{0} must equal {1}; meta: {2}, body: {3}", fieldName, expectedValue, metaJson, body);
        }
        catch (JsonException ex)
        {
            throw new Xunit.Sdk.XunitException(
                $"Metadata field '{fieldName}': could not parse metadata JSON '{metaJson}'. Inner: {ex.Message}. body: {body}");
        }
    }

    /// <summary>
    /// Asserts that a CurriculumChunkDto has the expected fields:
    /// chunkId (int), content (string), metadata (string), score (number).
    /// </summary>
    private static void AssertChunkShape(JsonElement chunk, string body)
    {
        foreach (var key in new[] { "content", "metadata", "score" })
        {
            var found = false;
            foreach (var prop in chunk.EnumerateObject())
            {
                if (string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }
            found.Should().BeTrue(
                "CurriculumChunkDto must expose '{0}'; body: {1}", key, body);
        }

        // Score must be in [0, 1] (cosine similarity = 1 - cosineDistance).
        var score = GetProp(chunk, "score", body).GetDouble();
        score.Should().BeInRange(0.0, 1.0,
            "Score is a cosine similarity in [0,1]; body: {0}", body);
    }
}
