using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

// ---------------------------------------------------------------------------
// P6-05 — Observability: OTel wiring + AI-Gateway + MinIO health checks.
//
// Uses the EXISTING HealthCheckWebAppFactory (Testcontainers Postgres, no Redis,
// env "Testing", no OpenTelemetry:Otlp:Endpoint) defined in P1_07_HealthEndpointTests.cs.
// All tests share the same factory / container via [Collection("HealthCheckTests")].
//
// The factory already sets:
//   ConnectionStrings:Default  → Testcontainers Postgres
//   ConnectionStrings:Redis    → "" (absent, validates Degraded-tolerant path)
//   (no OpenTelemetry:Otlp:Endpoint — tests the no-op / no-collector path)
//   (no Ai:Providers:* keys    — tests the ai-gateway Degraded path)
//
// Coverage map (story AC/scenarios):
//   Scenario 1: OTel no-op path        → P6_05_OTelNoOp_AppStartsAndHealthLiveReturns200
//   Scenario 2: /health/live → 200     → P6_05_HealthLive_AlwaysReturns200
//   Scenario 3: /health JSON body      → P6_05_Health_BodyIsStatusOnlyJson_WithRequiredEntries
//   Scenario 4: ai-gateway Degraded    → P6_05_AiGatewayUnconfigured_HealthReturns200_NotServiceUnavailable
//                                      → P6_05_AiGatewayUnconfigured_EntryIsDegraded_Not503
//   Scenario 5: security               → P6_05_Health_Body_DoesNotLeakSensitiveData
//   Scenario 6 (opt): ai-key flip      → P6_05_AiGatewayConfiguredViaDummyKey_EntryIsHealthy
//   Regression (P1-07 suite re-run):   HealthCheckTests collection runs together — see P1_07_HealthEndpointTests.cs
// ---------------------------------------------------------------------------

/// <summary>
/// P6-05 Observability integration tests — shares the existing HealthCheckWebAppFactory
/// (and therefore the existing Testcontainers Postgres container) via
/// [Collection("HealthCheckTests")].
/// </summary>
[Collection("HealthCheckTests")]
public sealed class P6_05_Observability_Tests : IAsyncLifetime
{
    private readonly HealthCheckWebAppFactory _factory;
    private readonly HttpClient _client;

    public P6_05_Observability_Tests(HealthCheckWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    public async Task InitializeAsync()
    {
        // Migrations are idempotent; if another test class already ran them we just re-confirm.
        await _factory.ApplyMigrationsAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // Scenario 1 — OTel wired, no OTLP endpoint (no-op): app starts cleanly.
    // Proof: if the WebApplicationFactory booted (CreateClient() succeeded) and
    // /health/live returns 200, the OTel registration did NOT break startup.
    // The test environment sets NO OpenTelemetry:Otlp:Endpoint, exercising the
    // config-gated no-op path in Program.cs.
    // =========================================================================

    [Fact(DisplayName = "Scenario 1 — OTel no-op: app starts with OTel wired but no OTLP endpoint, /health/live is 200")]
    public async Task P6_05_OTelNoOp_AppStartsAndHealthLiveReturns200()
    {
        // If OTel mis-wiring had caused a startup exception, CreateClient() (or
        // the factory InitializeAsync) would have thrown before we reach this line.
        // The explicit /health/live call confirms a working in-process request.
        var response = await _client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the app must start and serve requests cleanly when OpenTelemetry:Otlp:Endpoint " +
            "is absent (the no-op path). OTel is wired in-process but exports nowhere. " +
            "A startup failure here indicates OTel mis-wiring in Program.cs. " +
            "Got HTTP {0}.", (int)response.StatusCode);
    }

    // =========================================================================
    // Scenario 2 — /health/live always returns 200 (P6-05 regression on P1-07 AC-2a)
    // =========================================================================

    [Fact(DisplayName = "Scenario 2 — /health/live returns HTTP 200 (liveness, no dependency checks)")]
    public async Task P6_05_HealthLive_AlwaysReturns200()
    {
        var response = await _client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "/health/live is a liveness probe (Predicate = _ => false — no dependency checks). " +
            "It must always return 200 while the process is up. " +
            "A non-200 here is a regression introduced by P6-05 changes. Got {0}.", (int)response.StatusCode);
    }

    // =========================================================================
    // Scenario 3 — /health returns 200 and its body is status-only JSON
    // with entries containing at minimum: database, ai-gateway, minio.
    // Redis is absent in the test factory so is NOT expected as an entry.
    // =========================================================================

    [Fact(DisplayName = "Scenario 3 — /health returns HTTP 200 when DB is up (ai-gateway and minio are Degraded-tolerant)")]
    public async Task P6_05_Health_Returns200_WhenDbUp()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /health must return 200 when Postgres is reachable. " +
            "ai-gateway (no keys in test env) and minio (not reachable in test env) are both " +
            "Degraded-tolerant (failureStatus = HealthStatus.Degraded) and must not cause a 503. " +
            "Only 'database' is a hard-fail. Got {0}.", (int)response.StatusCode);
    }

    [Fact(DisplayName = "Scenario 3 — /health body is status-only JSON with 'status' and 'entries' keys")]
    public async Task P6_05_Health_BodyIsStatusOnlyJson_WithRequiredEntries()
    {
        var response = await _client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotBeNullOrWhiteSpace(
            "/health body must not be empty. Got HTTP {0}.", (int)response.StatusCode);

        // ── Deserialise and validate JSON shape ──────────────────────────────
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException ex)
        {
            throw new Xunit.Sdk.XunitException(
                $"/health body must be valid JSON. Parse error: {ex.Message}. Body: {body}");
        }

        var root = doc.RootElement;

        root.TryGetProperty("status", out var statusProp).Should().BeTrue(
            "/health JSON body must have a top-level 'status' key. " +
            "P6-05 added a minimal ResponseWriter that emits {{status, entries}}. Body: {0}", body);

        root.TryGetProperty("entries", out var entriesProp).Should().BeTrue(
            "/health JSON body must have a top-level 'entries' key. Body: {0}", body);

        entriesProp.ValueKind.Should().Be(JsonValueKind.Object,
            "'entries' must be a JSON object. Body: {0}", body);

        // ── Required entries ────────────────────────────────────────────────
        entriesProp.TryGetProperty("database", out _).Should().BeTrue(
            "'entries' must contain a 'database' key (Npgsql hard-fail check). Body: {0}", body);

        entriesProp.TryGetProperty("ai-gateway", out _).Should().BeTrue(
            "'entries' must contain an 'ai-gateway' key (P6-05 AC-2 new check). Body: {0}", body);

        entriesProp.TryGetProperty("minio", out _).Should().BeTrue(
            "'entries' must contain a 'minio' key (P6-05 Locked Decision). Body: {0}", body);

        // ── 'status' field value must be a non-empty string ─────────────────
        statusProp.ValueKind.Should().Be(JsonValueKind.String,
            "'status' must be a JSON string. Body: {0}", body);

        statusProp.GetString().Should().NotBeNullOrWhiteSpace(
            "'status' string must not be empty. Body: {0}", body);

        doc.Dispose();
    }

    [Fact(DisplayName = "Scenario 3 — /health body contains no exception detail, description text, or data beyond status")]
    public async Task P6_05_Health_Body_IsStatusOnly_NoExtraFields()
    {
        var response = await _client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        // The P6-05 ResponseWriter emits {status, entries:{name:status}} — nothing more.
        // Assert no high-cardinality / leak-prone keys appear.
        body.Should().NotContain("\"description\"",
            "the status-only JSON writer must not emit 'description' fields (which could carry " +
            "exception messages or connection string fragments). Body: {0}", body);

        body.Should().NotContain("\"exception\"",
            "the status-only JSON writer must not emit 'exception' fields. Body: {0}", body);

        body.Should().NotContain("\"data\"",
            "the status-only JSON writer must not emit 'data' blobs (could carry secrets). Body: {0}", body);

        body.Should().NotContain("\"tags\"",
            "the status-only JSON writer must not emit 'tags'. Body: {0}", body);

        body.Should().NotContain("\"duration\"",
            "the status-only JSON writer must not emit timing durations. Body: {0}", body);
    }

    // =========================================================================
    // Scenario 4 — ai-gateway misconfigured (no keys in test env) → Degraded,
    // NOT 503. Critical: an unconfigured AI Tutor must never hard-fail readiness.
    // =========================================================================

    [Fact(DisplayName = "Scenario 4 — ai-gateway unconfigured: /health returns HTTP 200, not 503")]
    public async Task P6_05_AiGatewayUnconfigured_HealthReturns200_NotServiceUnavailable()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().NotBe(HttpStatusCode.ServiceUnavailable,
            "503 would mean ai-gateway absence is treated as Unhealthy (hard-fail). " +
            "The check must be registered with failureStatus: HealthStatus.Degraded so that " +
            "an unconfigured AI Tutor never takes down the readiness probe. " +
            "This is the critical Degraded-tolerant contract from P6-05 AC-2.");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "when the DB is reachable, /health must return 200 even though ai-gateway " +
            "has no provider key (test env has none). Got {0}.", (int)response.StatusCode);
    }

    [Fact(DisplayName = "Scenario 4 — ai-gateway entry in JSON body is Degraded (not Healthy) when no keys are set")]
    public async Task P6_05_AiGatewayUnconfigured_EntryIsDegraded_Not503()
    {
        var response = await _client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException ex)
        {
            throw new Xunit.Sdk.XunitException(
                $"/health body must be valid JSON. Parse error: {ex.Message}. Body: {body}");
        }

        doc.RootElement.TryGetProperty("entries", out var entriesProp).Should().BeTrue(
            "/health body must have 'entries'. Body: {0}", body);

        entriesProp.TryGetProperty("ai-gateway", out var aiGatewayEntry).Should().BeTrue(
            "'entries' must contain 'ai-gateway'. Body: {0}", body);

        var aiGatewayStatus = aiGatewayEntry.GetString();
        aiGatewayStatus.Should().NotBeNullOrWhiteSpace(
            "ai-gateway entry must be a non-empty status string. Body: {0}", body);

        // In the test environment, no Ai:Providers:Claude:ApiKey or OpenAi:ApiKey is set,
        // so IAiReadinessProbe.GetStatus().ProviderConfigured = false → AiGatewayHealthCheck
        // returns Degraded. Assert NOT Healthy (the Healthy path requires a real/dummy key).
        aiGatewayStatus.Should().NotBeEquivalentTo("Healthy",
            "ai-gateway must not report Healthy when no provider API key is set in the test env. " +
            "If it is Healthy, the probe is not reading configuration correctly. Body: {0}", body);

        aiGatewayStatus.Should().BeEquivalentTo("Degraded",
            "ai-gateway must report Degraded when ProviderConfigured = false (no key in test env). " +
            "The check maps no-key → HealthCheckResult.Degraded. Body: {0}", body);

        doc.Dispose();
    }

    // =========================================================================
    // Scenario 5 — Security: /health body must not leak passwords, API keys,
    // stack traces, or exception class names.
    // Extends the existing P1-07 "Health_Body_DoesNotLeakSensitiveInfo" test
    // to also cover API-key patterns introduced by P6-05.
    // =========================================================================

    [Fact(DisplayName = "Scenario 5 — /health body does not leak passwords, API keys, or stack traces")]
    public async Task P6_05_Health_Body_DoesNotLeakSensitiveData()
    {
        var response = await _client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        // ── Passwords / connection strings ───────────────────────────────────
        body.Should().NotContainEquivalentOf("password",
            "/health body must not expose the DB password from the connection string. Body: {0}", body);

        body.Should().NotContainEquivalentOf("pwd=",
            "/health body must not expose password= or pwd= fragments from a connection string. Body: {0}", body);

        // ── Stack traces / exception types ──────────────────────────────────
        body.Should().NotContainEquivalentOf("StackTrace",
            "/health body must not expose stack traces (status-only writer must suppress them). Body: {0}", body);

        body.Should().NotContainEquivalentOf("Exception",
            "/health body must not expose exception class names. Body: {0}", body);

        body.Should().NotContainEquivalentOf(" at ",
            "/health body must not contain stack frame lines ('   at <frame>'). Body: {0}", body);

        // ── API key / secret patterns ────────────────────────────────────────
        // A real Claude API key starts with "sk-ant-"; OpenAI starts with "sk-".
        // Even with dummy values these prefixes should never appear in the health body
        // (the IAiReadinessProbe exposes booleans only, never key characters).
        body.Should().NotContain("sk-ant-",
            "/health body must not expose Claude API key prefix ('sk-ant-'). Body: {0}", body);

        body.Should().NotContain("sk-",
            "/health body must not expose OpenAI API key prefix ('sk-'). Body: {0}", body);

        // ── Connection string host/port leakage ─────────────────────────────
        // The Testcontainers connection string embeds a random localhost port.
        // The status-only JSON writer must not echo raw connection-string fragments.
        body.Should().NotContain("Host=",
            "/health body must not contain 'Host=' from a Npgsql connection string. Body: {0}", body);
    }

    // =========================================================================
    // Scenario 6 (optional) — ai-gateway flips to Healthy when a dummy key
    // is injected via ConfigureAppConfiguration (config-only probe: no real call).
    // Uses a one-shot factory override rather than the shared factory to avoid
    // polluting the shared container with dummy config.
    // =========================================================================

    [Fact(DisplayName = "Scenario 6 (optional) — ai-gateway flips to Healthy when dummy provider key is injected")]
    public async Task P6_05_AiGatewayConfiguredViaDummyKey_EntryIsHealthy()
    {
        // Spin up a short-lived factory variant with a dummy Ai provider key so the
        // IAiReadinessProbe reports ProviderConfigured = true → AiGatewayHealthCheck = Healthy.
        // The factory STILL uses the shared Postgres container connection string from the
        // outer _factory — we just borrow it.
        await using var localFactory = new HealthCheckWebAppFactoryWithAiKey(
            _factory.PostgresConnectionString);
        using var client = localFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        await localFactory.ApplyMigrationsAsync();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "with a dummy AI key injected, /health must still return 200. Body: {0}", body);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException ex)
        {
            throw new Xunit.Sdk.XunitException(
                $"/health body must be valid JSON. Parse error: {ex.Message}. Body: {body}");
        }

        doc.RootElement.TryGetProperty("entries", out var entriesProp).Should().BeTrue(
            "/health body must have 'entries'. Body: {0}", body);

        entriesProp.TryGetProperty("ai-gateway", out var aiGatewayEntry).Should().BeTrue(
            "'entries' must contain 'ai-gateway'. Body: {0}", body);

        var aiGatewayStatus = aiGatewayEntry.GetString();

        aiGatewayStatus.Should().BeEquivalentTo("Healthy",
            "when a dummy (non-empty) Ai:Providers:Claude:ApiKey is injected, " +
            "IAiReadinessProbe.GetStatus().ProviderConfigured should be true, " +
            "mapping to HealthCheckResult.Healthy. " +
            "If still Degraded, the probe is not reading the config key correctly. Body: {0}", body);

        doc.Dispose();
    }
}

// ---------------------------------------------------------------------------
// Helper: one-shot factory variant with a dummy AI provider key injected.
// Used only by Scenario 6 — never shared across the collection.
// Extends HealthCheckWebAppFactory configuration (same Postgres container string).
// ---------------------------------------------------------------------------

/// <summary>
/// A one-shot WebApplicationFactory that injects a non-empty dummy
/// <c>Ai:Providers:Claude:ApiKey</c> so <see cref="IAiReadinessProbe"/>
/// reports <c>ProviderConfigured = true</c>, flipping ai-gateway to Healthy.
/// No real HTTP call is made to Claude — the health check is config-inspection only.
/// </summary>
internal sealed class HealthCheckWebAppFactoryWithAiKey : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _postgresConnectionString;

    public HealthCheckWebAppFactoryWithAiKey(string postgresConnectionString)
    {
        _postgresConnectionString = postgresConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Point at the shared Testcontainers Postgres instance.
                ["ConnectionStrings:Default"] = _postgresConnectionString,
                ["ConnectionStrings:Redis"] = "",   // no Redis — keeps Degraded-tolerant path

                // Inject a dummy (non-empty) Claude API key so ProviderConfigured = true.
                // This is a fake value — the probe only checks key presence, never calls Claude.
                ["Ai:Providers:Claude:ApiKey"] = "dummy-key-for-health-check-test-only",

                // No OpenTelemetry:Otlp:Endpoint — keeps the no-op OTel path.
            });
        });
    }

    // Migrations are applied against the shared container. The shared container
    // may already have them applied; MigrateAsync is idempotent.
    public async Task ApplyMigrationsAsync()
    {
        using var scope = Services.CreateScope();
        var sp = scope.ServiceProvider;

        var identityDb = sp.GetRequiredService<Learnexia.Modules.Identity.Infrastructure.Persistence.IdentityModuleDbContext>();
        var notificationsDb = sp.GetRequiredService<Learnexia.Modules.Notifications.Infrastructure.Persistence.NotificationsDbContext>();

        await identityDb.Database.MigrateAsync();
        await notificationsDb.Database.MigrateAsync();
        await Learnexia.Modules.Identity.Api.IdentityModule.SeedAsync(sp);
    }

    public Task InitializeAsync() => Task.CompletedTask;   // no container to start (reuses shared Postgres)
    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();
}
