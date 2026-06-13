using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Learnexia.Modules.Ai.Infrastructure.Providers;

/// <summary>
/// Typed <c>HttpClient</c> adapter for the OpenAI Chat Completions API.
/// Proves the <see cref="IAiProvider"/> abstraction is complete and swappable.
///
/// API key sourced from <c>IConfiguration["Ai:Providers:OpenAi:ApiKey"]</c> — env / secret store only.
/// The key is NEVER logged, never included in error messages.
/// </summary>
public sealed class OpenAiProvider : IAiProvider
{
    // --- Named client key ---
    internal const string HttpClientName = "openai";

    // --- OpenAI API constants (non-user-facing technical identifiers) ---
    private const string AuthorizationScheme  = "Bearer";
    private const string ChatEndpoint         = "v1/chat/completions";
    private const int    DefaultMaxTokens     = 1024;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration     _configuration;
    private readonly ILoggerManager     _logger;

    public string ProviderName => "OpenAi";

    public OpenAiProvider(
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
            var body    = BuildRequestBody(request, modelId);
            var content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync(ChatEndpoint, content, cancellationToken);
            var latencyMs = (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds;

            return await ParseResponseAsync(response, modelId, latencyMs, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var latencyMs = (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds;
            _logger.LogWarn($"OpenAiProvider: HttpClient timeout after {latencyMs}ms for model {modelId}");
            return AiResult.Fail(
                new AiError(AiErrorKind.Timeout, "AI service timed out"),
                BuildPartialUsage(modelId, latencyMs));
        }
        catch (HttpRequestException)
        {
            var latencyMs = (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds;
            _logger.LogWarn($"OpenAiProvider: HttpRequestException for model {modelId}");
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
        // Streaming implementation is owned by P3-04. Fall back to single-shot here.
        var result = await CompleteAsync(request, modelId, cancellationToken);

        yield return new AiChunk
        {
            Delta  = result.Successed ? (result.Content ?? string.Empty) : string.Empty,
            IsLast = true,
            Usage  = result.Usage,
        };
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private HttpClient CreateHttpClient()
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var apiKey = _configuration["Ai:Providers:OpenAi:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(AuthorizationScheme, apiKey);
        }
        return client;
    }

    private static object BuildRequestBody(AiRequest request, string modelId)
    {
        var messages = new List<object>();

        // System prompt (if present).
        if (!string.IsNullOrWhiteSpace(request.CacheableSystemPrompt))
        {
            messages.Add(new { role = "system", content = request.CacheableSystemPrompt });
        }

        // Conversation history.
        if (request.History is { Count: > 0 })
        {
            foreach (var msg in request.History)
            {
                messages.Add(new { role = MapRole(msg.Role), content = msg.Content });
            }
        }

        messages.Add(new { role = "user", content = request.Prompt });

        return new
        {
            model      = modelId,
            max_tokens = request.MaxTokens ?? DefaultMaxTokens,
            messages,
        };
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

        var json   = await response.Content.ReadAsStringAsync(cancellationToken);
        OpenAiChatResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<OpenAiChatResponse>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"OpenAiProvider: failed to parse response JSON for model {modelId}");
            return AiResult.Fail(
                new AiError(AiErrorKind.InternalError, "AI response could not be parsed"),
                BuildPartialUsage(modelId, latencyMs));
        }

        var text = parsed?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;

        var usage = new AiUsage
        {
            Provider         = ProviderName,
            ModelId          = modelId,
            PromptTokens     = parsed?.Usage?.PromptTokens     ?? 0,
            CompletionTokens = parsed?.Usage?.CompletionTokens ?? 0,
            LatencyMs        = latencyMs,
            EstimatedCostUsd = 0m,
        };

        return AiResult.Ok(text, usage);
    }

    private AiResult MapHttpError(HttpStatusCode statusCode, string modelId, long latencyMs)
    {
        _logger.LogWarn($"OpenAiProvider: HTTP {(int)statusCode} for model {modelId}");

        var (kind, message) = (int)statusCode switch
        {
            429       => (AiErrorKind.RateLimited,  "AI service rate-limited; retry later"),
            >= 500    => (AiErrorKind.Unavailable,  "AI service temporarily unavailable"),
            401 or 403 => (AiErrorKind.BadRequest,  "AI request authentication failed"),
            _         => (AiErrorKind.BadRequest,   "AI request was rejected"),
        };

        return AiResult.Fail(new AiError(kind, message), BuildPartialUsage(modelId, latencyMs));
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
        AiMessageRole.System    => "system",
        AiMessageRole.Assistant => "assistant",
        AiMessageRole.User      => "user",
        _                       => "user",
    };

    // -------------------------------------------------------------------------
    // OpenAI API response shapes (internal)
    // -------------------------------------------------------------------------

    private sealed class OpenAiChatResponse
    {
        [JsonPropertyName("choices")]
        public OpenAiChoice[]? Choices { get; init; }

        [JsonPropertyName("usage")]
        public OpenAiUsage? Usage { get; init; }
    }

    private sealed class OpenAiChoice
    {
        [JsonPropertyName("message")]
        public OpenAiMessage? Message { get; init; }
    }

    private sealed class OpenAiMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; init; }
    }

    private sealed class OpenAiUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; init; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; init; }
    }
}
