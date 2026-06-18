namespace Learnexia.Modules.Parent.Application.Abstractions;

/// <summary>
/// Service that aggregates a child's prior-week activity across the cross-module seams
/// (XP / learning stats / mastery / weak areas) and upserts a <c>WeeklyReport</c> row
/// for that (child, week) pair — idempotent per week.
///
/// <para>Interface lives in Application so the handler and tests can reference it without
/// taking an EF / Infrastructure dependency (Option-C persistence rule).</para>
///
/// <para>Implemented in <c>Parent.Infrastructure.Service.WeeklyReportGeneratorService</c>.</para>
/// </summary>
public interface IWeeklyReportGeneratorService
{
    /// <summary>
    /// Aggregates the prior-week report for <paramref name="childId"/> starting on
    /// <paramref name="weekStartUtc"/> (Monday 00:00 UTC) and upserts the
    /// <c>WeeklyReport</c> row idempotently.
    ///
    /// <para>No-activity week: generates a valid report with zeroed XP / skills-improved
    /// and empty weak-areas / recommendations — never throws, never skips.</para>
    ///
    /// <para>Called by:</para>
    /// <list type="bullet">
    ///   <item><see cref="Learnexia.Modules.Parent.Infrastructure.Jobs.WeeklyReportJob"/> (scheduled sweep).</item>
    /// </list>
    /// </summary>
    /// <param name="childId">Loose int reference to the Identity student user (no cross-module FK).</param>
    /// <param name="weekStartUtc">Monday 00:00 UTC of the week to generate. Must be a Monday.</param>
    /// <param name="ct">Cancellation token.</param>
    Task GenerateAsync(int childId, DateTime weekStartUtc, CancellationToken ct = default);
}
