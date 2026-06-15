using System.Net;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Curriculum.Application.Abstractions;
using Learnexia.Modules.Curriculum.Infrastructure.Jobs;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence.Seed;
using Learnexia.Modules.Curriculum.Infrastructure.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Curriculum.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="ReEmbedCurriculumJob"/> (WI-A2) and
/// <see cref="BgeM3EmbeddingProvider"/> parity guard (WI-A1).
///
/// <para>All tests run WITHOUT a live TEI endpoint. A <see cref="FakeHttpMessageHandler"/>
/// returns deterministic real-shaped 1024-dim vectors as JSON so <see cref="BgeM3EmbeddingProvider"/>
/// is exercised end-to-end (including its parse path) without network calls.</para>
///
/// <para>Uses Testcontainers with pgvector/pgvector:pg16 to validate that EF writes real
/// vector values and the parity stamp is updated in the DB.</para>
/// </summary>
[Collection("ReEmbedIntegration")]
public sealed class ReEmbedJobTests : IAsyncLifetime
{
    // ── Container ─────────────────────────────────────────────────────────────

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg16")
        .WithDatabase("curriculum_test_reembed")
        .WithUsername("postgres")
        .WithPassword("testpwd")
        .Build();

    private ServiceProvider _serviceProvider = null!;

    // Active version injected into the fake BgeM3EmbeddingProvider settings.
    private const string FakeActiveModelVersion = "test-v1";

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        await _postgres.StartAsync();

        // Build a service provider with a real BgeM3EmbeddingProvider backed by
        // FakeHttpMessageHandler so IsConfigured=true and EmbedAsync returns real vectors.
        _serviceProvider = BuildServiceProvider(
            _postgres.GetConnectionString(),
            baseUrl: "http://fake-tei-host:8080",
            modelVersion: FakeActiveModelVersion);

        // Migrate + seed placeholder rows.
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CurriculumDbContext>();
        await db.Database.MigrateAsync();
        await CurriculumChunkSeeder.SeedAsync(scope.ServiceProvider);
    }

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _postgres.StopAsync();
    }

    // ── WI-A2 Test 1: Re-embed replaces all placeholder rows ─────────────────

    /// <summary>
    /// AC-A1: With a faked HTTP handler returning deterministic 1024-dim vectors, the job
    /// processes ALL placeholder rows and stamps them with the active ModelVersion.
    /// Placeholder row count drops to 0.
    /// </summary>
    [Fact]
    public async Task RunAsync_ProcessesAllPlaceholderRows_PlaceholderCountDropsToZero()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CurriculumDbContext>();

        var placeholderCountBefore = await db.ChunkEmbeddingsBgeM3
            .AsNoTracking()
            .CountAsync(e => e.ModelVersion == DeterministicEmbedding.PlaceholderModelVersion);
        placeholderCountBefore.Should().BeGreaterThan(0,
            "the seeder must have written placeholder rows");

        var job = _serviceProvider.GetRequiredService<ReEmbedCurriculumJob>();
        await job.RunAsync(CancellationToken.None);

        // After job: no rows should remain on the placeholder version.
        var placeholderCountAfter = await db.ChunkEmbeddingsBgeM3
            .AsNoTracking()
            .CountAsync(e => e.ModelVersion == DeterministicEmbedding.PlaceholderModelVersion);
        placeholderCountAfter.Should().Be(0,
            "re-embed must replace all placeholder rows with the active model version");
    }

    // ── WI-A2 Test 2: Idempotency ─────────────────────────────────────────────

    /// <summary>
    /// AC-A1 (idempotency): Running the job a second time after all rows are stamped with
    /// the active version is a safe no-op.
    /// </summary>
    [Fact]
    public async Task RunAsync_SecondRun_IsIdempotentNoOp()
    {
        var job = _serviceProvider.GetRequiredService<ReEmbedCurriculumJob>();

        // First run — processes all placeholder rows.
        await job.RunAsync(CancellationToken.None);

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CurriculumDbContext>();

        var countAfterFirstRun = await db.ChunkEmbeddingsBgeM3
            .AsNoTracking()
            .CountAsync(e => e.ModelVersion == DeterministicEmbedding.PlaceholderModelVersion);
        countAfterFirstRun.Should().Be(0, "first run must process all placeholders");

        // Second run — idempotent no-op, no rows to process.
        await job.RunAsync(CancellationToken.None);

        var countAfterSecondRun = await db.ChunkEmbeddingsBgeM3
            .AsNoTracking()
            .CountAsync(e => e.ModelVersion == DeterministicEmbedding.PlaceholderModelVersion);
        countAfterSecondRun.Should().Be(0,
            "second run is idempotent — row count must remain 0");
    }

    // ── WI-A2 Test 3: Re-embedded rows have 1024-dim real-shaped vectors ──────

    /// <summary>
    /// AC-A1: After re-embed, every row carries a 1024-dimensional vector and is stamped
    /// with the active provider, model, and model version (no longer placeholder values).
    /// </summary>
    [Fact]
    public async Task RunAsync_ReembeddedRows_Have1024DimVectorsAndCorrectStamps()
    {
        var job = _serviceProvider.GetRequiredService<ReEmbedCurriculumJob>();
        await job.RunAsync(CancellationToken.None);

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CurriculumDbContext>();

        var rows = await db.ChunkEmbeddingsBgeM3
            .AsNoTracking()
            .ToListAsync();

        rows.Should().NotBeEmpty("re-embed must have produced rows");
        foreach (var row in rows)
        {
            row.Vector.Should().NotBeNull($"row {row.Id} must have a vector after re-embed");
            row.Vector.ToArray().Should().HaveCount(DeterministicEmbedding.Dimension,
                $"row {row.Id} must have exactly {DeterministicEmbedding.Dimension} dimensions");
            row.ModelVersion.Should().Be(FakeActiveModelVersion,
                $"row {row.Id} must be stamped with the active model version");
            row.Provider.Should().NotBe(DeterministicEmbedding.PlaceholderProvider,
                $"row {row.Id} must no longer carry the placeholder provider stamp");
        }
    }

    // ── WI-A1 Test: Parity guard — BaseUrl set, ModelVersion missing ──────────

    /// <summary>
    /// AC-A4 (parity guard fail-fast):
    /// When <see cref="BgeM3EmbeddingProvider"/> is constructed with <c>BaseUrl</c> set but
    /// <c>ModelVersion</c> empty, <see cref="BgeM3EmbeddingProvider.EmbedAsync"/> returns
    /// <c>null</c> immediately — no HTTP call is made to TEI.
    /// </summary>
    [Fact]
    public async Task BgeM3Provider_EmptyModelVersion_EmbedAsyncReturnsNull()
    {
        var fakeHandler = new FakeHttpMessageHandler(
            responseFactory: _ => throw new InvalidOperationException(
                "HTTP must not be called when ModelVersion is missing (parity guard)."));

        using var httpClient = new HttpClient(fakeHandler)
        {
            BaseAddress = new Uri("http://fake-tei-host:8080"),
        };

        var settings = Options.Create(new EmbeddingSettings
        {
            BaseUrl      = "http://fake-tei-host:8080",
            AuthToken    = string.Empty,
            Model        = "bge-m3",
            ModelVersion = string.Empty,                // MISSING — parity guard must fire
        });

        var provider = new BgeM3EmbeddingProvider(httpClient, settings, new NoOpReEmbedLogger());

        var result = await provider.EmbedAsync("test text", CancellationToken.None);

        result.Should().BeNull(
            "empty ModelVersion must trigger parity guard — null returned, no HTTP call made");
    }

    // ── WI-A1 / AC-A5 Test: Graceful degrade — BaseUrl absent ────────────────

    /// <summary>
    /// AC-A5 (graceful degrade):
    /// When <c>BaseUrl</c> is empty, <see cref="BgeM3EmbeddingProvider.EmbedAsync"/>
    /// returns <c>null</c> immediately — RAG remains dormant, no crash.
    /// </summary>
    [Fact]
    public async Task BgeM3Provider_EmptyBaseUrl_EmbedAsyncReturnsNull_RagDormant()
    {
        var fakeHandler = new FakeHttpMessageHandler(
            responseFactory: _ => throw new InvalidOperationException(
                "HTTP must not be called when BaseUrl is empty (RAG dormant)."));

        using var httpClient = new HttpClient(fakeHandler);

        var settings = Options.Create(new EmbeddingSettings
        {
            BaseUrl      = string.Empty,  // RAG dormant
            AuthToken    = string.Empty,
            Model        = "bge-m3",
            ModelVersion = "1.0",
        });

        var provider = new BgeM3EmbeddingProvider(httpClient, settings, new NoOpReEmbedLogger());

        var result = await provider.EmbedAsync("test text", CancellationToken.None);

        result.Should().BeNull("absent BaseUrl → RAG dormant → null returned, no exception");
    }

    // ── WI-A2 / AC-A5 Test: Graceful degrade — job with unconfigured provider ─

    /// <summary>
    /// AC-A5 (graceful degrade — re-embed job):
    /// When the <see cref="BgeM3EmbeddingProvider"/> has <c>IsConfigured = false</c>
    /// (empty ModelVersion), the job logs a warning and exits without modifying any rows.
    /// </summary>
    [Fact]
    public async Task ReEmbedJob_ProviderNotConfigured_GracefullyDegradesDoeNotThrow()
    {
        // Build with empty ModelVersion → IsConfigured = false → job must abort early.
        var sp = BuildServiceProvider(
            _postgres.GetConnectionString(),
            baseUrl: "http://fake-tei-host:8080",
            modelVersion: string.Empty);

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CurriculumDbContext>();
        var countBefore = await db.ChunkEmbeddingsBgeM3.AsNoTracking().CountAsync();

        var job = sp.GetRequiredService<ReEmbedCurriculumJob>();

        // Must not throw.
        var act = async () => await job.RunAsync(CancellationToken.None);
        await act.Should().NotThrowAsync(
            "job must degrade gracefully when provider IsConfigured=false");

        // Rows unchanged — job aborted before any DB writes.
        var countAfter = await db.ChunkEmbeddingsBgeM3.AsNoTracking().CountAsync();
        countAfter.Should().Be(countBefore,
            "unconfigured provider → job aborts early → no rows updated");

        await sp.DisposeAsync();
    }

    // ── Service provider factory ──────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="ServiceProvider"/> wiring:
    /// <list type="bullet">
    ///   <item><see cref="CurriculumDbContext"/> against <paramref name="connectionString"/>.</item>
    ///   <item>
    ///     A real <see cref="BgeM3EmbeddingProvider"/> backed by <see cref="FakeHttpMessageHandler"/>
    ///     (returns deterministic 1024-dim JSON) so <c>IsConfigured</c> reflects the provided settings
    ///     and the job's <c>is BgeM3EmbeddingProvider</c> check passes.
    ///   </item>
    ///   <item><see cref="ReEmbedCurriculumJob"/> with <see cref="IServiceScopeFactory"/>.</item>
    /// </list>
    /// </summary>
    private static ServiceProvider BuildServiceProvider(
        string connectionString,
        string baseUrl,
        string modelVersion)
    {
        var services = new ServiceCollection();

        services.AddDbContext<CurriculumDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                b => b.UseVector()
                      .MigrationsHistoryTable("__EFMigrationsHistory", CurriculumDbContext.Schema)
                      .MigrationsAssembly(typeof(CurriculumDbContext).Assembly.FullName)));

        // EmbeddingSettings for injection into BgeM3EmbeddingProvider.
        var embeddingSettings = new EmbeddingSettings
        {
            BaseUrl      = baseUrl,
            AuthToken    = string.Empty,
            Model        = "bge-m3",
            ModelVersion = modelVersion,
        };
        services.AddSingleton(Options.Create(embeddingSettings));
        services.AddSingleton<ILoggerManager, NoOpReEmbedLogger>();

        // Register a real BgeM3EmbeddingProvider with a faked HttpClient.
        // The fake handler returns deterministic 1024-dim JSON so EmbedAsync succeeds.
        services.AddScoped<IEmbeddingProvider>(sp =>
        {
            var opts   = sp.GetRequiredService<IOptions<EmbeddingSettings>>();
            var logger = sp.GetRequiredService<ILoggerManager>();

            var fakeHandler = new FakeHttpMessageHandler(request =>
            {
                // Return batch-shape JSON: [[f0, f1, ..., f1023]]
                var floats = DeterministicFloats(request.RequestUri?.ToString() ?? string.Empty);
                var json   = JsonSerializer.Serialize(new[] { floats });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
                };
            });

            var httpClient = new HttpClient(fakeHandler);
            if (!string.IsNullOrWhiteSpace(embeddingSettings.BaseUrl))
                httpClient.BaseAddress = new Uri(embeddingSettings.BaseUrl);

            return new BgeM3EmbeddingProvider(httpClient, opts, logger);
        });

        // ReEmbedCurriculumJob requires IServiceScopeFactory (provided by BuildServiceProvider).
        services.AddTransient<ReEmbedCurriculumJob>();

        return services.BuildServiceProvider();
    }

    /// <summary>Deterministic L2-normalised 1024-dim float array seeded from a string's hash.</summary>
    private static float[] DeterministicFloats(string seed)
    {
        var rng    = new Random(seed.GetHashCode());
        var floats = new float[DeterministicEmbedding.Dimension];
        for (var i = 0; i < floats.Length; i++)
            floats[i] = (float)(rng.NextDouble() * 2 - 1);

        // L2-normalise so the vector is geometrically similar to real BGE-M3 output.
        var norm = MathF.Sqrt(floats.Sum(f => f * f));
        if (norm > 0)
            for (var i = 0; i < floats.Length; i++)
                floats[i] /= norm;

        return floats;
    }
}

// ── Supporting types ──────────────────────────────────────────────────────────

/// <summary>
/// In-memory <see cref="HttpMessageHandler"/> that delegates to a user-supplied
/// <paramref name="responseFactory"/> instead of making network calls.
/// Used to exercise <see cref="BgeM3EmbeddingProvider"/> without a live TEI endpoint.
/// </summary>
file sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
        => Task.FromResult(responseFactory(request));
}

/// <summary>No-op <see cref="ILoggerManager"/> for the re-embed tests.</summary>
file sealed class NoOpReEmbedLogger : ILoggerManager
{
    public void LogInfo(string message) { }
    public void LogWarn(string message) { }
    public void LogDebug(string message) { }
    public void LogError(Exception? ex, string message) { }
}

/// <summary>Collection fixture for re-embed integration tests (isolated Postgres container).</summary>
[CollectionDefinition("ReEmbedIntegration")]
public sealed class ReEmbedIntegrationCollection { }
