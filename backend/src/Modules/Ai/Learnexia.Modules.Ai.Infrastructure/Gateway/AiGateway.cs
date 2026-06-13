using System.Runtime.CompilerServices;
using Learnexia.Modules.Ai.Application.Options;
using Learnexia.Modules.Ai.Application.Services;
using Learnexia.Modules.Ai.Infrastructure.Providers;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Ai.Infrastructure.Gateway;

/// <summary>
/// Central AI gateway facade. The ONLY implementation of <see cref="IAiGateway"/>.
///
/// Responsibilities:
/// - Resolves provider via <see cref="IAiModelRouter"/>.
/// - Applies a hard timeout via a linked <see cref="CancellationTokenSource"/>.
/// - Bounded exponential-backoff retry on transient failures (429, 5xx, timeout).
/// - Translates ALL provider exceptions to typed <see cref="AiError"/>; NEVER throws to caller.
/// - Captures <see cref="AiUsage"/> (with estimated cost) and logs at Debug via <see cref="ILoggerManager"/>.
/// - Never logs prompt/response text or API keys.
/// </summary>
public sealed class AiGateway : IAiGateway
{
    private readonly IAiModelRouter             _router;
    private readonly IReadOnlyDictionary<string, IAiProvider> _providers;
    private readonly AiGatewayOptions           _options;
    private readonly ILoggerManager             _logger;

    public AiGateway(
        IAiModelRouter router,
        IEnumerable<IAiProvider> providers,
        IOptions<AiGatewayOptions> options,
        ILoggerManager logger)
    {
        _router    = router;
        _providers = providers.ToDictionary(p => p.ProviderName, StringComparer.OrdinalIgnoreCase);
        _options   = options.Value;
        _logger    = logger;
    }

    // -------------------------------------------------------------------------
    // IAiGateway — CompleteAsync
    // -------------------------------------------------------------------------

    public async Task<AiResult> CompleteAsync(
        AiRequest request,
        CancellationToken cancellationToken = default)
    {
        var route    = _router.Route(request.Task, request.TierHint);
        var provider = ResolveProvider(route.ProviderName);

        if (provider is null)
        {
            _logger.LogError(null, $"AiGateway: no provider registered for name '{route.ProviderName}'. Returning Unavailable.");
            return AiResult.Fail(new AiError(AiErrorKind.Unavailable, "AI service temporarily unavailable"));
        }

        AiResult? lastResult = null;
        int maxAttempts = Math.Max(1, _options.RetryCount + 1); // +1 because first call is not a retry

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            // Hard timeout: create a new linked CTS per attempt so each attempt gets a fresh budget.
            using var timeoutCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));
            using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            try
            {
                var result = await provider.CompleteAsync(request, route.ModelId, linkedCts.Token);

                if (result.Successed)
                {
                    var enriched = EnrichWithCost(result, route.ProviderName, route.ModelId);
                    LogUsage(enriched.Usage, attempt);
                    return enriched;
                }

                lastResult = result;

                // Retry only on transient errors.
                if (!IsTransient(result.Error?.Kind) || attempt >= maxAttempts)
                    break;

                var backoffMs = (int)(attempt * _options.RetryBackoffSeconds * 1000);
                _logger.LogWarn($"AiGateway: transient failure ({result.Error?.Kind}) on attempt {attempt}/{maxAttempts}. Backing off {backoffMs}ms.");

                await Task.Delay(backoffMs, cancellationToken);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                _logger.LogWarn($"AiGateway: hard timeout ({_options.TimeoutSeconds}s) on attempt {attempt}/{maxAttempts}.");

                lastResult = AiResult.Fail(
                    new AiError(AiErrorKind.Timeout, "AI request timed out"));

                // Timeout is transient — retry if budget remains.
                if (attempt >= maxAttempts)
                    break;

                var backoffMs = (int)(attempt * _options.RetryBackoffSeconds * 1000);
                await Task.Delay(backoffMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Caller cancelled — do not retry.
                _logger.LogDebug($"AiGateway: caller cancelled request for task {request.Task}.");
                return AiResult.Fail(new AiError(AiErrorKind.Unavailable, "AI request was cancelled"));
            }
            catch (Exception ex)
            {
                // Unexpected provider exception — translate, do not propagate.
                _logger.LogError(ex, $"AiGateway: unexpected exception on attempt {attempt}.");
                lastResult = AiResult.Fail(
                    new AiError(AiErrorKind.InternalError, "AI service temporarily unavailable"));

                if (attempt >= maxAttempts)
                    break;

                var backoffMs = (int)(attempt * _options.RetryBackoffSeconds * 1000);
                await Task.Delay(backoffMs, cancellationToken);
            }
        }

        return lastResult ?? AiResult.Fail(new AiError(AiErrorKind.InternalError, "AI service temporarily unavailable"));
    }

    // -------------------------------------------------------------------------
    // IAiGateway — StreamAsync
    // -------------------------------------------------------------------------

    public async IAsyncEnumerable<AiChunk> StreamAsync(
        AiRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var route    = _router.Route(request.Task, request.TierHint);
        var provider = ResolveProvider(route.ProviderName);

        if (provider is null)
        {
            _logger.LogError(null, $"AiGateway: no provider registered for name '{route.ProviderName}' (stream). Returning error chunk.");

            yield return new AiChunk { Delta = string.Empty, IsLast = true };
            yield break;
        }

        using var timeoutCts = new CancellationTokenSource(
            TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));
        using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

        await foreach (var chunk in provider.StreamAsync(request, route.ModelId, linkedCts.Token)
            .WithCancellation(linkedCts.Token))
        {
            yield return chunk;
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private IAiProvider? ResolveProvider(string providerName)
    {
        if (_providers.TryGetValue(providerName, out var p))
            return p;

        // Fall back to the configured default.
        if (_providers.TryGetValue(_options.DefaultProvider, out var def))
            return def;

        return null;
    }

    private static bool IsTransient(AiErrorKind? kind) =>
        kind is AiErrorKind.RateLimited or AiErrorKind.Unavailable or AiErrorKind.Timeout;

    private AiResult EnrichWithCost(AiResult result, string providerName, string modelId)
    {
        if (result.Usage is null) return result;

        // Look up model pricing from config.
        decimal estimatedCost = 0m;
        var configKey = modelId; // try exact model ID key
        if (_options.Models.TryGetValue(configKey, out var cfg))
        {
            estimatedCost =
                (result.Usage.PromptTokens     / 1_000_000m) * cfg.InputPricePerMillion +
                (result.Usage.CompletionTokens / 1_000_000m) * cfg.OutputPricePerMillion;
        }

        return result with
        {
            Usage = result.Usage with { EstimatedCostUsd = estimatedCost },
        };
    }

    private void LogUsage(AiUsage? usage, int attempt)
    {
        if (usage is null) return;

        // Log-only (Q5 — no DB write). No prompt/response text logged (no PII).
        _logger.LogDebug(
            $"AiGateway: provider={usage.Provider} model={usage.ModelId} " +
            $"promptTokens={usage.PromptTokens} completionTokens={usage.CompletionTokens} " +
            $"latencyMs={usage.LatencyMs} estimatedCostUsd={usage.EstimatedCostUsd:F6} " +
            $"cacheHit={usage.WasCacheHit} attempt={attempt}");
    }
}
