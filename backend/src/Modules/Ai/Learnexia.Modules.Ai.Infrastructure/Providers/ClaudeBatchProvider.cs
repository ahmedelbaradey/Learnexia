using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Learnexia.Modules.Ai.Infrastructure.Providers;

/// <summary>
/// Typed <c>HttpClient</c> adapter for the Anthropic Message Batches API.
/// Implements <see cref="IAiBatchGateway"/> using a named HttpClient ("claude-batch")
/// registered in DI via <c>IHttpClientFactory</c>.
///
/// <para><strong>API:</strong> Anthropic Message Batches API
/// (<c>POST v1/messages/batches</c>, <c>GET v1/messages/batches/{id}</c>,
/// <c>GET v1/messages/batches/{id}/results</c>).</para>
///
/// <para><strong>Key handling:</strong> API key sourced from
/// <c>IConfiguration["Ai:Providers:Claude:ApiKey"]</c> — env / secret store only.
/// The key is NEVER logged, never included in error messages, never stored in options objects.
/// Mirrors <see cref="ClaudeProvider"/> key-injection pattern exactly.</para>
///
/// <para><strong>Dormant-when-unconfigured:</strong> when the API key is absent the provider
/// does NOT crash at startup or at call time — it returns a typed
/// <see cref="AiErrorKind.Unavailable"/> result (mirrors the runtime gateway degrade behavior).</para>
///
/// <para><strong>Never throws:</strong> all failures are encoded in the result types.</para>
/// </summary>
public sealed class ClaudeBatchProvider : IAiBatchGateway
{
    // --- Named client key ---
    internal const string HttpClientName = "claude-batch";

    // --- Anthropic API constants (non-user-facing technical identifiers) ---
    private const string AnthropicVersionHeader  = "anthropic-version";
    private const string AnthropicVersionValue   = "2023-06-01";
    private const string AnthropicBetaHeader   = "anthropic-beta";
    private const string AnthropicBetaValue    = "message-batches-2024-09-24";
    private const string ApiKeyHeader            = "x-api-key";
    private const string BatchesEndpoint         = "v1/messages/batches";
    private const int    DefaultMaxTokens        = 1024;

    // --- Anthropic batch job status values (non-user-facing technical identifiers) ---
    private const string BatchStatusProcessing  = "in_progress";
    private const string BatchStatusEnded       = "ended";

    // --- Anthropic batch item result types (non-user-facing technical identifiers) ---
    private const string ItemResultSucceeded    = "succeeded";
    private const string ItemResultErrored      = "errored";
    private const string ItemResultExpired      = "expired";
    private const string ItemResultCanceled     = "canceled";

    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration     _configuration;
    private readonly ILoggerManager     _logger;

    public ClaudeBatchProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILoggerManager logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration     = configuration;
        _logger            = logger;
    }

    // -----------------------------------------------------------------------
    // IAiBatchGateway — SubmitBatchAsync
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<BatchSubmissionResult> SubmitBatchAsync(
        IReadOnlyList<AiRequest> requests,
        CancellationToken cancellationToken = default)
    {
        if (requests.Count == 0)
        {
            _logger.LogWarn("ClaudeBatchProvider.SubmitBatchAsync: empty request list — nothing to submit.");
            return BatchSubmissionResult.Fail(
                new AiError(AiErrorKind.BadRequest, "Batch request list must not be empty"));
        }

        var apiKey = _configuration["Ai:Providers:Claude:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarn("ClaudeBatchProvider: Ai:Providers:Claude:ApiKey not configured — batch submission dormant.");
            return BatchSubmissionResult.Fail(
                new AiError(AiErrorKind.Unavailable, "AI batch service is not configured"));
        }

        try
        {
            var client = CreateHttpClient(apiKey);
            var body   = BuildSubmitBody(requests);
            var json   = JsonSerializer.Serialize(body, JsonOpts);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(BatchesEndpoint, content, cancellationToken);
            return await ParseSubmitResponseAsync(response, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarn("ClaudeBatchProvider.SubmitBatchAsync: HttpClient timeout.");
            return BatchSubmissionResult.Fail(
                new AiError(AiErrorKind.Timeout, "AI batch service timed out"));
        }
        catch (HttpRequestException)
        {
            _logger.LogWarn("ClaudeBatchProvider.SubmitBatchAsync: HttpRequestException.");
            return BatchSubmissionResult.Fail(
                new AiError(AiErrorKind.Unavailable, "AI batch service temporarily unavailable"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClaudeBatchProvider.SubmitBatchAsync: unexpected error.");
            return BatchSubmissionResult.Fail(
                new AiError(AiErrorKind.InternalError, "AI batch submission failed unexpectedly"));
        }
    }

    // -----------------------------------------------------------------------
    // IAiBatchGateway — PollBatchAsync
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<BatchPollResult> PollBatchAsync(
        string batchJobId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(batchJobId))
        {
            return BatchPollResult.Fail(
                new AiError(AiErrorKind.BadRequest, "BatchJobId must not be empty"));
        }

        var apiKey = _configuration["Ai:Providers:Claude:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarn("ClaudeBatchProvider: Ai:Providers:Claude:ApiKey not configured — batch poll dormant.");
            return BatchPollResult.Fail(
                new AiError(AiErrorKind.Unavailable, "AI batch service is not configured"));
        }

        try
        {
            var client       = CreateHttpClient(apiKey);
            var statusUrl    = $"{BatchesEndpoint}/{batchJobId}";
            var statusResp   = await client.GetAsync(statusUrl, cancellationToken);

            if (!statusResp.IsSuccessStatusCode)
                return MapHttpPollError(statusResp.StatusCode);

            var statusJson   = await statusResp.Content.ReadAsStringAsync(cancellationToken);
            var statusParsed = JsonSerializer.Deserialize<ClaudeBatchStatusResponse>(statusJson, JsonOpts);

            if (statusParsed is null)
            {
                return BatchPollResult.Fail(
                    new AiError(AiErrorKind.InternalError, "AI batch status response could not be parsed"));
            }

            // Not yet finished — caller should poll again later.
            if (!string.Equals(statusParsed.ProcessingStatus, BatchStatusEnded, StringComparison.OrdinalIgnoreCase))
                return BatchPollResult.Processing();

            // Batch ended — fetch results from the results stream endpoint.
            var resultsUrl  = $"{BatchesEndpoint}/{batchJobId}/results";
            var resultsResp = await client.GetAsync(resultsUrl, cancellationToken);

            if (!resultsResp.IsSuccessStatusCode)
                return MapHttpPollError(resultsResp.StatusCode);

            var resultsBody = await resultsResp.Content.ReadAsStringAsync(cancellationToken);
            var items       = ParseResultsNdjson(resultsBody, batchJobId);
            return BatchPollResult.Ended(items);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarn($"ClaudeBatchProvider.PollBatchAsync: HttpClient timeout for batchJobId={batchJobId}.");
            return BatchPollResult.Fail(
                new AiError(AiErrorKind.Timeout, "AI batch poll timed out"));
        }
        catch (HttpRequestException)
        {
            _logger.LogWarn($"ClaudeBatchProvider.PollBatchAsync: HttpRequestException for batchJobId={batchJobId}.");
            return BatchPollResult.Fail(
                new AiError(AiErrorKind.Unavailable, "AI batch service temporarily unavailable"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"ClaudeBatchProvider.PollBatchAsync: unexpected error for batchJobId={batchJobId}.");
            return BatchPollResult.Fail(
                new AiError(AiErrorKind.InternalError, "AI batch poll failed unexpectedly"));
        }
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private HttpClient CreateHttpClient(string apiKey)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        // Inject API key at call time — never stored in a field so it is not leaked in logs.
        client.DefaultRequestHeaders.Remove(ApiKeyHeader);
        client.DefaultRequestHeaders.Add(ApiKeyHeader, apiKey);

        return client;
    }

    private static object BuildSubmitBody(IReadOnlyList<AiRequest> requests)
    {
        var batchRequests = new List<object>(requests.Count);

        for (var i = 0; i < requests.Count; i++)
        {
            var req      = requests[i];
            var messages = new List<object>();

            if (req.History is { Count: > 0 })
            {
                foreach (var msg in req.History)
                    messages.Add(new { role = MapRole(msg.Role), content = msg.Content });
            }

            messages.Add(new { role = "user", content = req.Prompt });

            var msgBody = new Dictionary<string, object>
            {
                // Use a deterministic custom_id based on position so results can be re-ordered.
                ["model"]      = "claude-sonnet-4-6",  // default offline model (overridden per request if needed)
                ["max_tokens"] = req.MaxTokens ?? DefaultMaxTokens,
                ["messages"]   = messages,
            };

            if (!string.IsNullOrWhiteSpace(req.CacheableSystemPrompt))
            {
                msgBody["system"] = new[]
                {
                    new
                    {
                        type          = "text",
                        text          = req.CacheableSystemPrompt,
                        cache_control = new { type = "ephemeral" },
                    },
                };
            }

            batchRequests.Add(new
            {
                custom_id = $"req-{i:D6}",
                @params   = msgBody,
            });
        }

        return new { requests = batchRequests };
    }

    private async Task<BatchSubmissionResult> ParseSubmitResponseAsync(
        HttpResponseMessage response,
        CancellationToken   cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarn($"ClaudeBatchProvider.SubmitBatchAsync: HTTP {(int)response.StatusCode}.");
            return BatchSubmissionResult.Fail(MapHttpError(response.StatusCode));
        }

        var json   = await response.Content.ReadAsStringAsync(cancellationToken);
        ClaudeBatchCreateResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<ClaudeBatchCreateResponse>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClaudeBatchProvider: failed to parse batch-create response JSON.");
            return BatchSubmissionResult.Fail(
                new AiError(AiErrorKind.InternalError, "AI batch response could not be parsed"));
        }

        if (parsed is null || string.IsNullOrWhiteSpace(parsed.Id))
        {
            return BatchSubmissionResult.Fail(
                new AiError(AiErrorKind.InternalError, "AI batch provider returned empty batch ID"));
        }

        _logger.LogInfo($"ClaudeBatchProvider: batch submitted successfully, batchJobId={parsed.Id}.");
        return BatchSubmissionResult.Ok(parsed.Id);
    }

    /// <summary>
    /// Parses NDJSON (newline-delimited JSON) result stream from the Anthropic results endpoint.
    /// Each line is a <c>ClaudeBatchResultLine</c>.  Lines that cannot be parsed are mapped to
    /// individual failed <see cref="BatchItemResult"/> entries (fail-soft).
    /// </summary>
    private IReadOnlyList<BatchItemResult> ParseResultsNdjson(string ndjson, string batchJobId)
    {
        var lines   = ndjson.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var results = new List<BatchItemResult>(lines.Length);
        var lineIdx = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            ClaudeBatchResultLine? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<ClaudeBatchResultLine>(trimmed, JsonOpts);
            }
            catch (Exception ex)
            {
                _logger.LogWarn($"ClaudeBatchProvider: failed to parse result line {lineIdx} for batchJobId={batchJobId}. {ex.GetType().Name}");
                results.Add(BatchItemResult.Fail(lineIdx,
                    new AiError(AiErrorKind.InternalError, "AI batch result line could not be parsed")));
                lineIdx++;
                continue;
            }

            if (parsed is null)
            {
                results.Add(BatchItemResult.Fail(lineIdx,
                    new AiError(AiErrorKind.InternalError, "AI batch result line was null")));
                lineIdx++;
                continue;
            }

            // Parse the custom_id back to an index ("req-000001" → 1).
            var index = ParseIndex(parsed.CustomId, lineIdx);
            results.Add(MapResultLine(index, parsed));
            lineIdx++;
        }

        // Sort by index so callers get back results in the same order as the original requests.
        results.Sort((a, b) => a.Index.CompareTo(b.Index));
        return results;
    }

    private static BatchItemResult MapResultLine(int index, ClaudeBatchResultLine line)
    {
        var resultType = line.Result?.Type ?? string.Empty;

        if (string.Equals(resultType, ItemResultSucceeded, StringComparison.OrdinalIgnoreCase))
        {
            var text    = line.Result?.Message?.Content?[0]?.Text ?? string.Empty;
            var inTok   = line.Result?.Message?.Usage?.InputTokens ?? 0;
            var outTok  = line.Result?.Message?.Usage?.OutputTokens ?? 0;
            var usage   = new AiUsage
            {
                Provider         = "Claude",
                ModelId          = line.Result?.Message?.Model ?? "claude-batch",
                PromptTokens     = inTok,
                CompletionTokens = outTok,
                LatencyMs        = 0,     // Batch API does not report per-item latency.
                EstimatedCostUsd = 0m,
                WasCacheHit      = (line.Result?.Message?.Usage?.CacheReadInputTokens ?? 0) > 0,
            };
            return BatchItemResult.Ok(index, text, usage);
        }

        var reason = resultType switch
        {
            ItemResultErrored  => "AI batch item errored",
            ItemResultExpired  => "AI batch item expired",
            ItemResultCanceled => "AI batch item was canceled",
            _                  => "AI batch item failed with unknown result type",
        };

        return BatchItemResult.Fail(index, new AiError(AiErrorKind.InternalError, reason));
    }

    private static int ParseIndex(string? customId, int fallback)
    {
        // Format: "req-000001" — strip the "req-" prefix and parse the integer.
        if (string.IsNullOrWhiteSpace(customId)) return fallback;
        var prefix = "req-";
        if (!customId.StartsWith(prefix, StringComparison.Ordinal)) return fallback;
        return int.TryParse(customId.AsSpan(prefix.Length), out var idx) ? idx : fallback;
    }

    private static AiError MapHttpError(HttpStatusCode code)
    {
        var (kind, message) = (int)code switch
        {
            429       => (AiErrorKind.RateLimited, "AI batch service rate-limited; retry later"),
            >= 500    => (AiErrorKind.Unavailable, "AI batch service temporarily unavailable"),
            401 or 403 => (AiErrorKind.BadRequest, "AI batch request authentication failed"),
            _         => (AiErrorKind.BadRequest,  "AI batch request was rejected"),
        };
        return new AiError(kind, message);
    }

    private static BatchPollResult MapHttpPollError(HttpStatusCode code)
        => BatchPollResult.Fail(MapHttpError(code));

    private static string MapRole(AiMessageRole role) => role switch
    {
        AiMessageRole.User      => "user",
        AiMessageRole.Assistant => "assistant",
        AiMessageRole.System    => "user",
        _                       => "user",
    };

    // -----------------------------------------------------------------------
    // Anthropic Batch API response shapes (internal — not exposed outside this class)
    // -----------------------------------------------------------------------

    private sealed class ClaudeBatchCreateResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }
    }

    private sealed class ClaudeBatchStatusResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("processing_status")]
        public string? ProcessingStatus { get; init; }
    }

    private sealed class ClaudeBatchResultLine
    {
        [JsonPropertyName("custom_id")]
        public string? CustomId { get; init; }

        [JsonPropertyName("result")]
        public ClaudeBatchResultBody? Result { get; init; }
    }

    private sealed class ClaudeBatchResultBody
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("message")]
        public ClaudeBatchMessage? Message { get; init; }
    }

    private sealed class ClaudeBatchMessage
    {
        [JsonPropertyName("model")]
        public string? Model { get; init; }

        [JsonPropertyName("content")]
        public ClaudeContentBlock[]? Content { get; init; }

        [JsonPropertyName("usage")]
        public ClaudeBatchUsage? Usage { get; init; }
    }

    private sealed class ClaudeContentBlock
    {
        [JsonPropertyName("text")]
        public string? Text { get; init; }
    }

    private sealed class ClaudeBatchUsage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; init; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; init; }

        [JsonPropertyName("cache_read_input_tokens")]
        public int CacheReadInputTokens { get; init; }
    }
}
