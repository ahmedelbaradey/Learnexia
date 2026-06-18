namespace Learnexia.Modules.Parent.Application.Abstractions;

/// <summary>
/// Read-only service for the E9 weekly report read endpoint.
///
/// <para>Keeps the <c>GetWeeklyReportQueryHandler</c> EF-free (Option-C): the handler
/// calls this service interface; the Infrastructure implementation holds the EF query.</para>
///
/// <para>Implemented in <c>Parent.Infrastructure.Service.WeeklyReportQueryService</c>.</para>
/// </summary>
public interface IWeeklyReportQueryService
{
    /// <summary>
    /// Returns the latest stored <see cref="WeeklyReportReadDto"/> for <paramref name="childId"/>,
    /// or <c>null</c> when no report has been generated yet.
    /// </summary>
    /// <param name="childId">Loose int reference to the Identity student user (no cross-module FK).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WeeklyReportReadDto?> GetLatestAsync(int childId, CancellationToken ct = default);

    /// <summary>
    /// Returns the stored <see cref="WeeklyReportReadDto"/> for <paramref name="childId"/> on
    /// <paramref name="weekStartUtc"/> (Monday 00:00 UTC), or <c>null</c> when not found.
    /// </summary>
    /// <param name="childId">Loose int reference to the Identity student user (no cross-module FK).</param>
    /// <param name="weekStartUtc">Monday 00:00 UTC of the requested week.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WeeklyReportReadDto?> GetByWeekAsync(int childId, DateTime weekStartUtc, CancellationToken ct = default);
}

/// <summary>
/// Materialised weekly report row as returned by <see cref="IWeeklyReportQueryService"/>.
/// Callers (the E9 handler) map this to the response DTO.
/// </summary>
public sealed class WeeklyReportReadDto
{
    public int      Id                  { get; init; }
    public int      ChildId             { get; init; }
    public DateTime WeekStartUtc        { get; init; }
    public int      XpEarned            { get; init; }
    public int      SkillsImproved      { get; init; }
    public string   WeakAreasJson       { get; init; } = "[]";
    public string   RecommendationsJson { get; init; } = "[]";
    public DateTime GeneratedAtUtc      { get; init; }
}
