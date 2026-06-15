using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Learnexia.Modules.Curriculum.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Options;
using Pgvector;

namespace Learnexia.Modules.Curriculum.Infrastructure.Services;

/// <summary>
/// BGE-M3 TEI (Text-Embeddings-Inference) adapter.
/// Converts query text to a 1024-dimensional vector by calling the self-hosted TEI endpoint
/// on Hetzner (synchronous HTTP; CPU-only at MVP, GPU when latency demands it).
///
/// Mirrors <c>ExpoPushSender</c> typed-HttpClient pattern:
/// - Injected <see cref="HttpClient"/> (base URL set at registration time).
/// - <see cref="IOptions{EmbeddingSettings}"/> for Model/ModelVersion/AuthToken.
/// - <see cref="ILoggerManager"/> — structured dev-only log text; never logs secrets.
/// - Fail-soft on missing config: absent <c>BaseUrl</c> → return <c>null</c> (never crash startup).
///
/// <para><strong>Parity guard (WI-A1 — fail-fast):</strong>
/// When <c>EmbeddingSettings.ModelVersion</c> is empty or absent, <see cref="EmbedAsync"/> returns
/// <c>null</c> immediately with a structured error log — it does NOT proceed to the TEI endpoint.
/// Calling the real endpoint with an unconfigured model version would stamp unknown model metadata
/// on newly embedded rows, silently creating a parity mismatch that corrupts cosine search.
/// The constructor logs a clear warning so the lead can identify the misconfiguration at startup.
/// The production fix is to set <c>Curriculum:Embedding:ModelVersion</c> to the pinned TEI version.</para>
///
/// <para><strong>Security:</strong> <see cref="EmbeddingSettings.AuthToken"/> is a secret;
/// this class never logs it. The Bearer header is added per-request on the HttpRequestMessage
/// (not on DefaultRequestHeaders) to avoid thread-safety issues on the shared HttpClient.</para>
/// </summary>
public sealed class BgeM3EmbeddingProvider : IEmbeddingProvider
{
    private const string EmbedEndpoint = "/embed";

    private readonly HttpClient _http;
    private readonly EmbeddingSettings _settings;
    private readonly ILoggerManager _logger;

    /// <summary>
    /// Whether the provider is fully configured (BaseUrl + ModelVersion both non-empty).
    /// Exposed internally so the re-embed job can surface a clear "not configured" error
    /// without attempting a network call.
    /// </summary>
    internal bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_settings.BaseUrl) &&
        !string.IsNullOrWhiteSpace(_settings.ModelVersion);

    /// <summary>
    /// The configured Model slug (e.g. "bge-m3").
    /// Used by <c>ReEmbedCurriculumJob</c> to stamp the <c>Model</c> column on updated rows.
    /// </summary>
    internal string ActiveModel => _settings.Model;

    /// <summary>
    /// The configured ModelVersion stamp (e.g. "1.0").
    /// Used by <c>ReEmbedCurriculumJob</c> to identify which rows need re-embedding
    /// (stored ModelVersion != this value) and to stamp updated rows.
    /// </summary>
    internal string ActiveModelVersion => _settings.ModelVersion;

    public BgeM3EmbeddingProvider(
        HttpClient http,
        IOptions<EmbeddingSettings> settings,
        ILoggerManager logger)
    {
        _http     = http;
        _settings = settings.Value;
        _logger   = logger;

        // WI-A1: startup log of configured status so the lead can see at a glance whether RAG is
        // dormant (no BaseUrl/ModelVersion) or active. Never log the AuthToken secret.
        if (string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            _logger.LogWarn(
                "P3-07 BgeM3EmbeddingProvider [RAG DORMANT]: Curriculum:Embedding:BaseUrl is not configured. " +
                "EmbedAsync will return null. Set BaseUrl + ModelVersion (and AuthToken via env/secrets) once " +
                "the TEI endpoint (BE-0) is provisioned to activate real BGE-M3 embeddings.");
        }
        else if (string.IsNullOrWhiteSpace(_settings.ModelVersion))
        {
            // WI-A1 FAIL-FAST parity guard: BaseUrl is set but ModelVersion is missing.
            // Proceeding without ModelVersion would stamp an empty string on embedded rows,
            // making parity detection impossible. Block at startup with a clear error.
            _logger.LogError(
                new InvalidOperationException("Curriculum:Embedding:ModelVersion is required when BaseUrl is set."),
                "P3-07 BgeM3EmbeddingProvider [PARITY GUARD]: Curriculum:Embedding:ModelVersion is not configured " +
                "but BaseUrl is set. EmbedAsync will return null to prevent stamping rows with an empty " +
                "ModelVersion (which would corrupt parity detection). Fix: set Curriculum:Embedding:ModelVersion " +
                "to the pinned TEI model version (e.g. '1.0').");
        }
        else
        {
            _logger.LogInfo(
                $"P3-07 BgeM3EmbeddingProvider [CONFIGURED]: BaseUrl=<set> Model={_settings.Model} " +
                $"ModelVersion={_settings.ModelVersion}. Auth={(!string.IsNullOrWhiteSpace(_settings.AuthToken) ? "present" : "absent")}.");
        }
    }

    /// <inheritdoc/>
    public async Task<Vector?> EmbedAsync(string text, CancellationToken ct = default)
    {
        // WI-A1 FAIL-FAST: absent BaseUrl → RAG dormant (expected in dev/test without BE-0).
        if (string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            _logger.LogWarn(
                "P3-07 BgeM3EmbeddingProvider: BaseUrl is not configured. " +
                "Returning null. Set Curriculum:Embedding:BaseUrl once BE-0 (TEI) is live.");
            return null;
        }

        // WI-A1 FAIL-FAST parity guard: BaseUrl set but ModelVersion missing → refuse to embed.
        // Proceeding would call TEI and stamp an empty ModelVersion on the resulting row, making
        // parity detection impossible. Return null so retrieval returns empty (no garbage results).
        if (string.IsNullOrWhiteSpace(_settings.ModelVersion))
        {
            _logger.LogError(
                new InvalidOperationException("Parity guard: ModelVersion not configured."),
                "P3-07 BgeM3EmbeddingProvider [PARITY GUARD FAIL-FAST]: ModelVersion is not configured. " +
                "Refusing to embed — stamping rows with an empty ModelVersion would corrupt parity detection. " +
                "Set Curriculum:Embedding:ModelVersion to the pinned TEI version.");
            return null;
        }

        var payload = JsonSerializer.Serialize(new { inputs = text, normalize = true });

        // Per-request HttpRequestMessage so the Authorization header is never mutated on
        // the shared HttpClient instance (DefaultRequestHeaders is not thread-safe; mirrors ExpoPushSender).
        using var request = new HttpRequestMessage(HttpMethod.Post, EmbedEndpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };

        if (!string.IsNullOrWhiteSpace(_settings.AuthToken))
        {
            // Secret is in the header only — never logged.
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _settings.AuthToken);
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "P3-07 BgeM3EmbeddingProvider: HTTP call to TEI endpoint failed.");
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarn(
                $"P3-07 BgeM3EmbeddingProvider: TEI returned non-2xx status {(int)response.StatusCode}. Returning null.");
            return null;
        }

        string responseBody;
        try
        {
            responseBody = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "P3-07 BgeM3EmbeddingProvider: failed to read TEI response body.");
            return null;
        }

        return ParseEmbedding(responseBody);
    }

    // -------------------------------------------------------------------------
    // Response parsing — TEI returns [[f32, f32, …]] (array of arrays, one per input)
    // -------------------------------------------------------------------------

    private Vector? ParseEmbedding(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            // TEI /embed returns either [[…]] (batch) or [f32, f32, …] (single).
            float[] floats;
            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var first = root[0];
                if (first.ValueKind == JsonValueKind.Array)
                {
                    // Batch shape: [[dim0, dim1, …]]
                    floats = first.EnumerateArray()
                        .Select(e => e.GetSingle())
                        .ToArray();
                }
                else
                {
                    // Flat shape: [dim0, dim1, …]
                    floats = root.EnumerateArray()
                        .Select(e => e.GetSingle())
                        .ToArray();
                }
            }
            else
            {
                _logger.LogWarn("P3-07 BgeM3EmbeddingProvider: unexpected TEI response shape; returning null.");
                return null;
            }

            if (floats.Length != DeterministicEmbedding.Dimension)
            {
                // Parity guard: dimension mismatch — incompatible model or configuration error.
                _logger.LogWarn(
                    $"P3-07 BgeM3EmbeddingProvider: TEI returned {floats.Length} dimensions " +
                    $"(expected {DeterministicEmbedding.Dimension}). ModelVersion parity violation. Returning null.");
                return null;
            }

            return new Vector(floats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "P3-07 BgeM3EmbeddingProvider: failed to parse TEI embedding response.");
            return null;
        }
    }
}
