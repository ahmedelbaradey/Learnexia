using System.Text.Json;
using Learnexia.Modules.Ai.Application.Abstractions;
using Learnexia.Modules.Ai.Application.Features.AdminSafety.Dtos;
using Learnexia.Modules.Ai.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Ai.Infrastructure.Services;

/// <summary>
/// Option-C implementation of <see cref="IAiSafetyDashboardService"/>.
/// All EF Core concerns (IQueryable, filters, materialisation) are composed here —
/// inside the Infrastructure layer that owns <see cref="AiDbContext"/> —
/// so the Application layer and query handlers stay completely EF-free.
///
/// <para><strong>jsonb aggregation (P7-11 brief §backend-feature handoff, IAiSafetyDashboardService):</strong>
/// <c>ReasonCodes</c> and <c>FailedChecks</c> are stored as JSON strings in jsonb columns.
/// PostgreSQL's <c>jsonb_array_elements_text</c> unnest is not directly available via EF Core
/// LINQ for this Npgsql version. V1 strategy: fetch the date-windowed result set (bounded by
/// the indexed <c>OccurredAtUtc</c> predicate) and aggregate in-memory using
/// <c>System.Text.Json</c> to deserialize the arrays. This is safe for a monitoring dashboard
/// where the window is small. A future owner may replace with a raw-SQL unnest for large tables.</para>
///
/// <para><strong>UTC normalisation:</strong> Npgsql legacy-timestamp reads <c>timestamptz</c>
/// as <see cref="DateTimeKind.Local"/>; normalize to UTC at the service boundary via
/// <c>ToUniversalTime()</c> before emitting to callers.</para>
/// </summary>
public sealed class AiSafetyDashboardService : IAiSafetyDashboardService
{
    private readonly AiDbContext _db;

    public AiSafetyDashboardService(AiDbContext db)
    {
        _db = db;
    }

    // ── Summary ───────────────────────────────────────────────────────────────────────

    public async Task<SafetySignalSummaryDto> GetSummaryAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        // Fetch the windowed, PII-light projection in one round-trip.
        // Only the columns needed for aggregation are selected (no FailedChecks/ReasonCodes text in
        // this query; those are fetched as the json string for in-memory unnesting).
        var rows = await _db.SafetyEvents
            .AsNoTracking()
            .Where(e => e.OccurredAtUtc >= from && e.OccurredAtUtc <= to)
            .Select(e => new
            {
                e.ActionTaken,
                e.TaskKind,
                e.ModelId,
                e.ReasonCodes,
            })
            .ToListAsync(cancellationToken);

        var total = rows.Count;

        // Action breakdowns.
        var actionGroups = rows
            .GroupBy(r => r.ActionTaken)
            .ToDictionary(g => g.Key, g => g.Count());

        var blockedCount          = actionGroups.GetValueOrDefault("Blocked", 0);
        var regeneratedCount      = actionGroups.GetValueOrDefault("Regenerated", 0);
        var fallbackReturnedCount = actionGroups.GetValueOrDefault("FallbackReturned", 0);

        var breakdownByAction = actionGroups
            .Select(kv => new CountBreakdownDto(kv.Key, kv.Value))
            .OrderByDescending(d => d.Count)
            .ToList();

        // TaskKind breakdowns.
        var breakdownByTaskKind = rows
            .GroupBy(r => r.TaskKind)
            .Select(g => new CountBreakdownDto(g.Key, g.Count()))
            .OrderByDescending(d => d.Count)
            .ToList();

        // ModelId breakdowns.
        var breakdownByModelId = rows
            .GroupBy(r => r.ModelId)
            .Select(g => new CountBreakdownDto(g.Key, g.Count()))
            .OrderByDescending(d => d.Count)
            .ToList();

        // ReasonCode breakdowns — in-memory unnest of jsonb arrays (see class-level doc).
        var reasonCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var codes = DeserializeJsonArray(row.ReasonCodes);
            foreach (var code in codes)
            {
                reasonCounts[code] = reasonCounts.GetValueOrDefault(code, 0) + 1;
            }
        }

        var breakdownByReason = reasonCounts
            .Select(kv => new CountBreakdownDto(kv.Key, kv.Value))
            .OrderByDescending(d => d.Count)
            .ToList();

        var blockedRate          = total == 0 ? 0d : Math.Round(blockedCount          / (double)total * 100, 2);
        var regeneratedRate      = total == 0 ? 0d : Math.Round(regeneratedCount      / (double)total * 100, 2);
        var fallbackReturnedRate = total == 0 ? 0d : Math.Round(fallbackReturnedCount / (double)total * 100, 2);

        return new SafetySignalSummaryDto(
            From:                   from.ToUniversalTime(),
            To:                     to.ToUniversalTime(),
            TotalEvents:            total,
            BlockedCount:           blockedCount,
            BlockedRate:            blockedRate,
            RegeneratedCount:       regeneratedCount,
            RegeneratedRate:        regeneratedRate,
            FallbackReturnedCount:  fallbackReturnedCount,
            FallbackReturnedRate:   fallbackReturnedRate,
            BreakdownByAction:      breakdownByAction,
            BreakdownByReasonCode:  breakdownByReason,
            BreakdownByModelId:     breakdownByModelId,
            BreakdownByTaskKind:    breakdownByTaskKind);
    }

    // ── Flagged outputs (paged) ───────────────────────────────────────────────────────

    public async Task<PaginatedResult<FlaggedOutputDto>> GetFlaggedOutputsPagedAsync(
        string? action,
        string? reasonCode,
        string? taskKind,
        DateTime? from,
        DateTime? to,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.SafetyEvents.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(e => e.ActionTaken == action);

        if (!string.IsNullOrWhiteSpace(taskKind))
            query = query.Where(e => e.TaskKind == taskKind);

        if (from.HasValue)
            query = query.Where(e => e.OccurredAtUtc >= from.Value);

        if (to.HasValue)
            query = query.Where(e => e.OccurredAtUtc <= to.Value);

        // ReasonCode filter: jsonb array-containment (@>).
        // EF.Functions.JsonContains translates to `"ReasonCodes" @> '["<code>"]'::jsonb`, which is a
        // DB-native exact-element match and avoids the LIKE / ~~ operator that 42883s on jsonb columns.
        // Also fixes the substring over-match where a short code (e.g. "TOX") would have matched longer
        // codes ("TOXICITY_HIGH") via a LIKE predicate.
        if (!string.IsNullOrWhiteSpace(reasonCode))
            query = query.Where(e => EF.Functions.JsonContains(e.ReasonCodes, "[\"" + reasonCode + "\"]"));

        // Newest-first (indexed column).
        query = query.OrderByDescending(e => e.OccurredAtUtc);

        var totalCount = await query.CountAsync(cancellationToken);

        if (totalCount == 0)
            return PaginatedResult<FlaggedOutputDto>.Success(
                new List<FlaggedOutputDto>(), 0, pageNumber, pageSize);

        var rows = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                e.Id,
                e.TaskKind,
                e.ActionTaken,
                e.ReasonCodes,
                e.FailedChecks,
                e.ModelId,
                e.StudentId,
                e.OccurredAtUtc,
            })
            .ToListAsync(cancellationToken);

        var dtos = rows
            .Select(r => new FlaggedOutputDto(
                ContentRef:   r.Id,
                TaskKind:     r.TaskKind,
                ActionTaken:  r.ActionTaken,
                ReasonCodes:  DeserializeJsonArray(r.ReasonCodes),
                FailedChecks: DeserializeJsonArray(r.FailedChecks),
                ModelId:      r.ModelId,
                StudentId:    r.StudentId,
                OccurredAtUtc: r.OccurredAtUtc.ToUniversalTime()))
            .ToList();

        return PaginatedResult<FlaggedOutputDto>.Success(dtos, totalCount, pageNumber, pageSize);
    }

    // ── Trend (per-day buckets) ───────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SafetyTrendBucketDto>> GetTrendAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        // Fetch the minimal projection for the window.
        var rows = await _db.SafetyEvents
            .AsNoTracking()
            .Where(e => e.OccurredAtUtc >= from && e.OccurredAtUtc <= to)
            .Select(e => new { e.ActionTaken, e.OccurredAtUtc })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return Array.Empty<SafetyTrendBucketDto>();

        // Group by calendar date (UTC, day-only) to produce per-day buckets.
        var buckets = rows
            .GroupBy(r => r.OccurredAtUtc.ToUniversalTime().Date)
            .OrderBy(g => g.Key)
            .Select(g => new SafetyTrendBucketDto(
                BucketDate:           DateTime.SpecifyKind(g.Key, DateTimeKind.Utc),
                TotalCount:           g.Count(),
                BlockedCount:         g.Count(r => r.ActionTaken == "Blocked"),
                RegeneratedCount:     g.Count(r => r.ActionTaken == "Regenerated"),
                FallbackReturnedCount: g.Count(r => r.ActionTaken == "FallbackReturned")))
            .ToList();

        return buckets;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Deserialises a stored jsonb string (e.g. <c>["TOXICITY_HIGH","AGE_INAPPROPRIATE"]</c>)
    /// into a list of strings. Returns an empty list on null, empty, or malformed input
    /// (fail-soft — bad JSON should not crash the dashboard).
    /// </summary>
    private static IReadOnlyList<string> DeserializeJsonArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return Array.Empty<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json)
                   ?? new List<string>();
        }
        catch
        {
            // Malformed jsonb — return empty rather than crashing the dashboard.
            return Array.Empty<string>();
        }
    }
}
