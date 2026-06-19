using Learnexia.Modules.Ai.Application.Abstractions;
using Learnexia.Modules.Ai.Application.Features.AdminTutorUsage.Dtos;
using Learnexia.Modules.Ai.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Ai.Infrastructure.Services;

/// <summary>
/// Option-C implementation of <see cref="IAiTutorUsageService"/>.
/// All EF Core concerns (IQueryable, filters, materialisation, grouping) are composed here —
/// inside the Infrastructure layer that owns <see cref="AiDbContext"/> —
/// so the Application layer and query handlers stay completely EF-free.
///
/// <para><strong>Aggregation strategy:</strong> Fetches the date-windowed result set (bounded by
/// the indexed <c>OccurredAtUtc</c> predicate) and aggregates in-memory for breakdowns that
/// require grouping across multiple properties. This matches the pattern established by
/// <see cref="AiSafetyDashboardService"/> and is safe for a monitoring dashboard where the
/// window is expected to be small relative to the whole table.</para>
///
/// <para><strong>UTC normalisation:</strong> Npgsql legacy-timestamp reads <c>timestamptz</c>
/// as <see cref="DateTimeKind.Local"/>; normalise to UTC at the service boundary via
/// <c>ToUniversalTime()</c> before emitting to callers.</para>
/// </summary>
public sealed class AiTutorUsageService : IAiTutorUsageService
{
    private readonly AiDbContext _db;

    public AiTutorUsageService(AiDbContext db)
    {
        _db = db;
    }

    public async Task<TutorUsageDto> GetUsageSummaryAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        // Fetch the windowed, PII-light projection in one round-trip.
        // AsNoTracking: read-only dashboard query, no change tracking needed.
        var rows = await _db.AiUsageLogs
            .AsNoTracking()
            .Where(l => l.OccurredAtUtc >= from && l.OccurredAtUtc <= to)
            .Select(l => new
            {
                l.Provider,
                l.ModelId,
                l.TaskKind,
                l.PromptTokens,
                l.CompletionTokens,
                l.EstimatedCostUsd,
                l.LatencyMs,
                l.WasCacheHit,
                OccurredAtUtc = l.OccurredAtUtc,
            })
            .ToListAsync(cancellationToken);

        // Empty window — return zeroed payload (HTTP 200, never 404).
        if (rows.Count == 0)
        {
            return new TutorUsageDto(
                From:                   from.ToUniversalTime(),
                To:                     to.ToUniversalTime(),
                TotalCalls:             0,
                TotalPromptTokens:      0,
                TotalCompletionTokens:  0,
                TotalEstimatedCostUsd:  0m,
                AvgLatencyMs:           0d,
                CacheHitRate:           0d,
                ByModel:                Array.Empty<TutorUsageByModelDto>(),
                ByTaskKind:             Array.Empty<TutorUsageByTaskKindDto>(),
                Trend:                  Array.Empty<TutorUsageTrendBucketDto>());
        }

        // ── Totals ───────────────────────────────────────────────────────────────────

        var totalCalls            = rows.Count;
        var totalPromptTokens     = rows.Sum(r => r.PromptTokens);
        var totalCompletionTokens = rows.Sum(r => r.CompletionTokens);
        var totalEstimatedCost    = rows.Sum(r => r.EstimatedCostUsd);
        var avgLatencyMs          = rows.Average(r => (double)r.LatencyMs);
        var cacheHitRate          = rows.Count(r => r.WasCacheHit) / (double)totalCalls;

        // ── ByModel breakdown ────────────────────────────────────────────────────────

        var byModel = rows
            .GroupBy(r => r.ModelId)
            .Select(g => new TutorUsageByModelDto(
                ModelId:               g.Key,
                Calls:                 g.Count(),
                TotalTokens:           g.Sum(r => r.PromptTokens + r.CompletionTokens),
                TotalEstimatedCostUsd: g.Sum(r => r.EstimatedCostUsd)))
            .OrderByDescending(d => d.Calls)
            .ToList();

        // ── ByTaskKind breakdown ─────────────────────────────────────────────────────

        var byTaskKind = rows
            .GroupBy(r => r.TaskKind)
            .Select(g => new TutorUsageByTaskKindDto(
                TaskKind:              g.Key,
                Calls:                 g.Count(),
                TotalTokens:           g.Sum(r => r.PromptTokens + r.CompletionTokens),
                TotalEstimatedCostUsd: g.Sum(r => r.EstimatedCostUsd)))
            .OrderByDescending(d => d.Calls)
            .ToList();

        // ── Per-day trend ────────────────────────────────────────────────────────────

        var trend = rows
            .GroupBy(r => r.OccurredAtUtc.ToUniversalTime().Date)
            .OrderBy(g => g.Key)
            .Select(g => new TutorUsageTrendBucketDto(
                Date:                  DateTime.SpecifyKind(g.Key, DateTimeKind.Utc),
                Calls:                 g.Count(),
                TotalEstimatedCostUsd: g.Sum(r => r.EstimatedCostUsd)))
            .ToList();

        return new TutorUsageDto(
            From:                   from.ToUniversalTime(),
            To:                     to.ToUniversalTime(),
            TotalCalls:             totalCalls,
            TotalPromptTokens:      totalPromptTokens,
            TotalCompletionTokens:  totalCompletionTokens,
            TotalEstimatedCostUsd:  totalEstimatedCost,
            AvgLatencyMs:           Math.Round(avgLatencyMs, 2),
            CacheHitRate:           Math.Round(cacheHitRate, 4),
            ByModel:                byModel,
            ByTaskKind:             byTaskKind,
            Trend:                  trend);
    }
}
