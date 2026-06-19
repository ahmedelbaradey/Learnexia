using Learnexia.Modules.Analytics.Application.Abstractions;
using Learnexia.Shared.Contracts.Analytics;

namespace Learnexia.Modules.Analytics.Infrastructure.Contracts;

/// <summary>
/// Implements <see cref="IPlatformAnalyticsQuery"/> by delegating to
/// <see cref="IActivitySessionService"/> for windowed session and retention derivation.
///
/// <para>Cross-module isolation: the Identity façade injects <see cref="IPlatformAnalyticsQuery"/>
/// from <c>Shared.Contracts</c> — it never references this adapter or any Analytics project
/// directly. This adapter lives in <c>Analytics.Infrastructure</c> and is registered in
/// <c>AddAnalyticsInfrastructure</c> as Scoped.</para>
///
/// <para>Option C: all EF access is behind <see cref="IActivitySessionService"/>; this adapter
/// is EF-free and only maps from <see cref="ActivitySessionStats"/> to
/// <see cref="PlatformAnalyticsStats"/>.</para>
///
/// <para>All results are sentinel-safe: delegates to <see cref="IActivitySessionService"/> which
/// is itself sentinel-safe — never null, never throws.</para>
/// </summary>
public sealed class PlatformAnalyticsQueryAdapter : IPlatformAnalyticsQuery
{
    private readonly IActivitySessionService _sessionService;

    public PlatformAnalyticsQueryAdapter(IActivitySessionService sessionService)
    {
        _sessionService = sessionService;
    }

    /// <inheritdoc />
    public async Task<PlatformAnalyticsStats> GetPlatformAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        var stats = await _sessionService.GetStatsAsync(fromUtc, toUtc, ct);

        return new PlatformAnalyticsStats(
            DistinctActiveStudents:    stats.DistinctActiveStudents,
            TotalSessions:             stats.TotalSessions,
            AvgSessionDurationSeconds: stats.AvgSessionDurationSeconds,
            AvgActiveDaysPerStudent:   stats.AvgActiveDaysPerStudent,
            ReturningStudentRate:      stats.ReturningStudentRate);
    }
}
