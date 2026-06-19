namespace Learnexia.Modules.Ai.Application.Features.AdminTutorUsage.Dtos;

/// <summary>
/// Aggregated AI tutor usage/cost summary over a date range (P7-11 usage/cost dashboard).
///
/// <para>Totals are computed over all <c>ai.AiUsageLogs</c> rows in the requested time window.
/// Empty windows return zeroed totals and empty breakdown lists (HTTP 200, never 404).</para>
///
/// <para>No PII: no prompt text, no response text, no student name or email — token counts and cost only.</para>
/// </summary>
public record TutorUsageDto(
    /// <summary>Inclusive lower bound of the query window (UTC).</summary>
    DateTime From,

    /// <summary>Inclusive upper bound of the query window (UTC).</summary>
    DateTime To,

    /// <summary>Total number of AI provider calls in the window.</summary>
    int TotalCalls,

    /// <summary>Sum of all input prompt tokens billed by providers.</summary>
    int TotalPromptTokens,

    /// <summary>Sum of all output completion tokens billed by providers.</summary>
    int TotalCompletionTokens,

    /// <summary>Sum of all estimated USD costs. Indicative only — actual billing is from the provider dashboard.</summary>
    decimal TotalEstimatedCostUsd,

    /// <summary>Average end-to-end latency across all calls in the window, in milliseconds.</summary>
    double AvgLatencyMs,

    /// <summary>Proportion of calls that reported a provider cache hit (0.0 – 1.0).</summary>
    double CacheHitRate,

    /// <summary>Per-model breakdown (model, calls, tokens, cost).</summary>
    IReadOnlyList<TutorUsageByModelDto> ByModel,

    /// <summary>Per-task-kind breakdown (taskKind, calls, tokens, cost).</summary>
    IReadOnlyList<TutorUsageByTaskKindDto> ByTaskKind,

    /// <summary>Per-day trend buckets (date, calls, cost). Ordered ascending by date.</summary>
    IReadOnlyList<TutorUsageTrendBucketDto> Trend);
