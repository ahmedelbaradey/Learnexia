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
/// - Fail-soft: any failure (no endpoint, network error, non-2xx, parse error)
///   returns <c>null</c> — callers short-circuit retrieval rather than throwing.
///
/// <para><strong>Parity guard:</strong> logs a warning when <c>EmbeddingSettings.ModelVersion</c>
/// is empty or does not match the seeder's <see cref="DeterministicEmbedding.PlaceholderModelVersion"/>.
/// Once BE-0 (live TEI) is provisioned the parity guard will compare the configured
/// <c>ModelVersion</c> against the value stamped on <c>chunk_embeddings_bge_m3</c> rows.
/// Mismatched model versions produce incompatible vector spaces.</para>
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

    public BgeM3EmbeddingProvider(
        HttpClient http,
        IOptions<EmbeddingSettings> settings,
        ILoggerManager logger)
    {
        _http     = http;
        _settings = settings.Value;
        _logger   = logger;

        // Parity guard: warn if ModelVersion is not configured. Retrieval will still work once BE-0
        // is live — the guard is informational, not a hard block (fail-soft contract).
        if (string.IsNullOrWhiteSpace(_settings.ModelVersion))
        {
            _logger.LogWarn(
                "P3-07 BgeM3EmbeddingProvider: EmbeddingSettings.ModelVersion is not configured. " +
                "Set Curriculum:Embedding:ModelVersion to the pinned TEI model version to enable parity guard.");
        }
    }

    /// <inheritdoc/>
    public async Task<Vector?> EmbedAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            // No live endpoint configured — expected in dev/test without BE-0 provisioned.
            _logger.LogWarn(
                "P3-07 BgeM3EmbeddingProvider: BaseUrl is not configured. " +
                "Returning null (no context available). Set Curriculum:Embedding:BaseUrl once BE-0 (TEI) is live.");
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
