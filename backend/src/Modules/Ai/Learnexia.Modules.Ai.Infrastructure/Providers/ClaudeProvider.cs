using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Learnexia.Modules.Ai.Infrastructure.Providers;

/// <summary>
/// Typed <c>HttpClient</c> adapter for the Anthropic Messages API.
/// Implements <see cref="IAiProvider"/> using a named HttpClient ("claude") registered
/// in DI via <c>IHttpClientFactory</c>.
///
/// API key sourced from <c>IConfiguration["Ai:Providers:Claude:ApiKey"]</c> — env / secret store only.
/// The key is NEVER logged, never included in error messages, and never stored in options objects.
/// </summary>
public sealed class ClaudeProvider : IAiProvider
{
    // --- Named client key (used in DI registration and factory resolution) ---
    internal const string HttpClientName = "claude";

    // --- Anthropic API constants (non-user-facing technical identifiers) ---
    private const string AnthropicVersionHeader = "anthropic-version";
    private const string AnthropicVersionValue  = "2023-06-01";
    private const string ApiKeyHeader           = "x-api-key";
    private const string MessagesEndpoint       = "v1/messages";
    private const int    DefaultMaxTokens       = 1024;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration     _configuration;
    private readonly ILoggerManager     _logger;

    public string ProviderName => "Claude";

    public ClaudeProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILoggerManager logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration     = configuration;
        _logger            = logger;
    }

    public async Task<AiResult> CompleteAsync(
        AiRequest request,
        string modelId,
        CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;

        try
        {
            var client  = CreateHttpClient();
            var body    = BuildRequestBody(request, modelId, stream: false);
            var content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync(MessagesEndpoint, content, cancellationToken);
            var latencyMs = (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds;

            return await ParseResponseAsync(response, modelId, latencyMs, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient own timeout fired (not the gateway CTS) — treat as timeout.
            var latencyMs = (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds;
            _logger.LogWarn($"ClaudeProvider: HttpClient timeout after {latencyMs}ms for model {modelId}");
            return AiResult.Fail(
                new AiError(AiErrorKind.Timeout, "AI service timed out"),
                BuildPartialUsage(modelId, latencyMs));
        }
        catch (HttpRequestException)
        {
            var latencyMs = (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds;
            _logger.LogWarn($"ClaudeProvider: HttpRequestException for model {modelId}");
            return AiResult.Fail(
                new AiError(AiErrorKind.Unavailable, "AI service temporarily unavailable"),
                BuildPartialUsage(modelId, latencyMs));
        }
    }

    public async IAsyncEnumerable<AiChunk> StreamAsync(
        AiRequest request,
        string modelId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Streaming implementation is owned by P3-04 (the SSE endpoint story).
        // The signature is frozen here; the real SSE fan-out will be added in P3-04.
        // For now, fall back to a single-shot non-streaming call and yield one chunk.
        var result = await CompleteAsync(request, modelId, cancellationToken);

        if (result.Successed && result.Content is not null)
        {
            yield return new AiChunk
            {
                Delta  = result.Content,
                IsLast = true,
                Usage  = result.Usage,
            };
        }
        else
        {
            // Propagate error as a terminal chunk with empty delta.
            yield return new AiChunk
            {
                Delta  = string.Empty,
                IsLast = true,
                Usage  = result.Usage,
            };
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private HttpClient CreateHttpClient()
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        // Inject API key at call time — never stored in a field so it is not leaked in logs.
        var apiKey = _configuration["Ai:Providers:Claude:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            // Remove any existing key header before adding (idempotent across calls).
            client.DefaultRequestHeaders.Remove(ApiKeyHeader);
            client.DefaultRequestHeaders.Add(ApiKeyHeader, apiKey);
        }

        return client;
    }

    private static object BuildRequestBody(AiRequest request, string modelId, bool stream)
    {
        var messages = new List<object>();

        // Prepend conversation history if present.
        if (request.History is { Count: > 0 })
        {
            foreach (var msg in request.History)
            {
                messages.Add(new { role = MapRole(msg.Role), content = msg.Content });
            }
        }

        // Add the current user prompt.
        messages.Add(new { role = "user", content = request.Prompt });

        var body = new Dictionary<string, object>
        {
            ["model"]      = modelId,
            ["max_tokens"] = request.MaxTokens ?? DefaultMaxTokens,
            ["messages"]   = messages,
            ["stream"]     = stream,
        };

        // Attach cacheable system prompt with cache_control if provided.
        if (!string.IsNullOrWhiteSpace(request.CacheableSystemPrompt))
        {
            body["system"] = new[]
            {
                new
                {
                    type  = "text",
                    text  = request.CacheableSystemPrompt,
                    cache_control = new { type = "ephemeral" },
                },
            };
        }

        return body;
    }

    private async Task<AiResult> ParseResponseAsync(
        HttpResponseMessage response,
        string modelId,
        long latencyMs,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            return MapHttpError(response.StatusCode, modelId, latencyMs);
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        ClaudeMessagesResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<ClaudeMessagesResponse>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"ClaudeProvider: failed to parse response JSON for model {modelId}");
            return AiResult.Fail(
                new AiError(AiErrorKind.InternalError, "AI response could not be parsed"),
                BuildPartialUsage(modelId, latencyMs));
        }

        if (parsed is null || parsed.Content is not { Length: > 0 })
        {
            return AiResult.Fail(
                new AiError(AiErrorKind.InternalError, "AI provider returned empty content"),
                BuildPartialUsage(modelId, latencyMs));
        }

        var text = parsed.Content[0].Text ?? string.Empty;

        var usage = new AiUsage
        {
            Provider              = ProviderName,
            ModelId               = modelId,
            PromptTokens          = parsed.Usage?.InputTokens  ?? 0,
            CompletionTokens      = parsed.Usage?.OutputTokens ?? 0,
            LatencyMs             = latencyMs,
            // Cost estimation: caller (AiGateway) computes this from ModelConfig pricing.
            EstimatedCostUsd      = 0m,
            WasCacheHit           = (parsed.Usage?.CacheReadInputTokens ?? 0) > 0,
        };

        return AiResult.Ok(text, usage);
    }

    private AiResult MapHttpError(HttpStatusCode statusCode, string modelId, long latencyMs)
    {
        // Log at warn-level (not the full body — no PII / API key leakage).
        _logger.LogWarn($"ClaudeProvider: HTTP {(int)statusCode} for model {modelId}");

        var (kind, message) = (int)statusCode switch
        {
            429                           => (AiErrorKind.RateLimited,  "AI service rate-limited; retry later"),
            >= 500                        => (AiErrorKind.Unavailable,  "AI service temporarily unavailable"),
            401 or 403                    => (AiErrorKind.BadRequest,   "AI request authentication failed"),
            _                             => (AiErrorKind.BadRequest,   "AI request was rejected"),
        };

        return AiResult.Fail(
            new AiError(kind, message),
            BuildPartialUsage(modelId, latencyMs));
    }

    private AiUsage BuildPartialUsage(string modelId, long latencyMs) =>
        new()
        {
            Provider         = ProviderName,
            ModelId          = modelId,
            PromptTokens     = 0,
            CompletionTokens = 0,
            LatencyMs        = latencyMs,
            EstimatedCostUsd = 0m,
        };

    private static string MapRole(AiMessageRole role) => role switch
    {
        AiMessageRole.User      => "user",
        AiMessageRole.Assistant => "assistant",
        AiMessageRole.System    => "user", // Claude v3 folds system turns into user messages for history
        _                       => "user",
    };

    // -------------------------------------------------------------------------
    // Anthropic API response shapes (internal — never exposed outside this class)
    // -------------------------------------------------------------------------

    private sealed class ClaudeMessagesResponse
    {
        [JsonPropertyName("content")]
        public ClaudeContentBlock[]? Content { get; init; }

        [JsonPropertyName("usage")]
        public ClaudeUsage? Usage { get; init; }
    }

    private sealed class ClaudeContentBlock
    {
        [JsonPropertyName("text")]
        public string? Text { get; init; }
    }

    private sealed class ClaudeUsage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; init; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; init; }

        [JsonPropertyName("cache_read_input_tokens")]
        public int CacheReadInputTokens { get; init; }
    }
}
