using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Reengagement.Republishers;

/// <summary>
/// Re-publishes <see cref="StreakAdvancedDomainEvent"/> as
/// <see cref="StreakMilestoneReachedIntegrationEvent"/> when the student's new streak value
/// is one of the configured milestone thresholds (default 3/7/14/30 days).
///
/// <para><strong>Producer-side dedupe by construction:</strong>
/// <c>StreakAdvancedDomainEvent</c> increments by 1 per day-transition, so each threshold
/// value (e.g. 7) is crossed exactly once per streak episode — "fire only when
/// <c>NewStreak ∈ milestone set</c>" is inherently one-time-per-threshold-per-episode.
/// A streak reset (<c>ResetStreakAndStart</c>) raises <c>NewStreak=1</c>, which is below
/// all default thresholds, so no spurious milestone fires. The consumer also retains the
/// existing Redis (child, category, day) dedupe as a duplicate-delivery guard.</para>
///
/// <para><strong>Threshold configuration:</strong> thresholds are read at each invocation
/// via <see cref="IGlobalSettingsProvider"/> key <c>Gamification:Streak:MilestoneDays</c>
/// (default <c>"3,7,14,30"</c>) — tunable without a redeploy, consistent with P9-07 config
/// style. Malformed entries are silently skipped.</para>
///
/// <para>Fail-soft per ADR 0002 §3: catch + log; do NOT rethrow into the streak commit path
/// (NFR-1: no hot-path latency degradation).</para>
/// Auto-registered via <c>Gamification.Application.AssemblyReference</c> (host MediatR scan).
/// </summary>
public sealed class StreakMilestoneReachedRepublisher
    : INotificationHandler<StreakAdvancedDomainEvent>
{
    private const string MilestoneDaysKey     = "Gamification:Streak:MilestoneDays";
    private const string DefaultMilestoneDays = "3,7,14,30";

    private readonly IPublisher _publisher;
    private readonly IGlobalSettingsProvider _settings;
    private readonly ILoggerManager _logger;

    public StreakMilestoneReachedRepublisher(
        IPublisher publisher,
        IGlobalSettingsProvider settings,
        ILoggerManager logger)
    {
        _publisher = publisher;
        _settings  = settings;
        _logger    = logger;
    }

    public async Task Handle(StreakAdvancedDomainEvent notification, CancellationToken ct)
    {
        try
        {
            var milestones = ParseMilestones(_settings.GetString(MilestoneDaysKey, DefaultMilestoneDays));

            if (!milestones.Contains(notification.NewStreak))
                return; // not a milestone day — common path, no publish

            _logger.LogInfo(
                $"P9-06: streak milestone reached — studentId={notification.StudentId}, " +
                $"milestone={notification.NewStreak}.");

            await _publisher.Publish(new StreakMilestoneReachedIntegrationEvent(
                EventId:       Guid.NewGuid(),
                OccurredOnUtc: notification.OccurredOnUtc,
                StudentId:     notification.StudentId,
                Milestone:     notification.NewStreak), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P9-06: republish failed for {nameof(StreakAdvancedDomainEvent)} " +
                $"studentId={notification.StudentId}, newStreak={notification.NewStreak} — " +
                $"milestone nudge may be missed.");
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Parses a comma-separated milestone string (e.g. "3,7,14,30") into a <see cref="HashSet{T}"/>.
    /// Malformed entries (non-integer tokens) are silently skipped.
    /// </summary>
    private static HashSet<int> ParseMilestones(string raw)
    {
        var result = new HashSet<int>();
        foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(token, out var value) && value > 0)
                result.Add(value);
        }
        return result;
    }
}
