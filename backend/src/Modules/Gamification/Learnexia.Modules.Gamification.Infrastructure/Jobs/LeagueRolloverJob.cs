using Learnexia.Modules.Gamification.Application.Configuration;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job — runs Monday 00:15 UTC (<c>"15 0 * * 1"</c>) (P4-07, AC2 + AC4).
///
/// <para><b>Purpose:</b> weekly league rollover. For every cohort in last week's period:
/// <list type="bullet">
///   <item>Rank members by WeeklyXp DESC, JoinedAtUtc ASC (D4 tiebreak).</item>
///   <item>Stamp <c>FinalRank</c>, <c>TierAfter</c>, and <c>Status</c> (Promoted/Demoted/Stayed)
///         per <see cref="LeagueStandings.Apply"/>.</item>
///   <item>Update <c>StudentXpProfile.CurrentTier</c> for promoted/demoted members so that
///         next week's lazy placement reads the new tier.</item>
///   <item>Publish one <see cref="LeagueTierChangedIntegrationEvent"/> per promoted/demoted member
///         AFTER <c>SaveChangesAsync</c> (P4-09 forward-compat; no Notifications consumer in this
///         story — deferred per P4-09 brief §"Not in scope").</item>
/// </list></para>
///
/// <para><b>Idempotency (AC4):</b> per-cohort short-circuit — if any member already has
/// <c>TierAfter != null</c>, the cohort was already processed; skip it.</para>
///
/// <para><b>Event ordering:</b> integration events are published AFTER <c>SaveChangesAsync</c>
/// commits the tier update. A publish failure cannot undo the tier change (fail-soft per ADR 0002 §3).
/// Each publish is individually try/caught.</para>
///
/// <para><b>Scheduling:</b> Monday 00:15 UTC — strictly after StreakSweepJob (00:05) and
/// MissionRolloverJob daily/weekly (00:05 / 00:10).</para>
///
/// Hangfire resolves this class via DI — registered as Transient in
/// <c>AddGamificationInfrastructure</c>; wired in <c>GamificationModule.InitializeAsync</c>.
/// </summary>
public sealed class LeagueRolloverJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISystemClock _clock;
    private readonly IOptions<LeagueOptions> _options;
    private readonly ILoggerManager _logger;

    public LeagueRolloverJob(
        IServiceScopeFactory scopeFactory,
        ISystemClock clock,
        IOptions<LeagueOptions> options,
        ILoggerManager logger)
    {
        _scopeFactory = scopeFactory;
        _clock        = clock;
        _options      = options;
        _logger       = logger;
    }

    /// <summary>
    /// Executes the weekly league rollover for last week's period key.
    /// Creates a fresh DI scope (Hangfire worker has no HTTP request scope — mandatory).
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow.ToUniversalTime();

        // Compute last week's period key: subtract 3 days from Monday 00:15 → lands on last Thursday,
        // which is guaranteed to be inside last week's ISO period (D16 per plan).
        var lastWeekUtc = now.AddDays(-3);
        var (lastWeekKey, _, _) = MissionPeriodCalculator.GetCurrentPeriod(MissionType.Weekly, lastWeekUtc);

        _logger.LogInfo($"P4-07: league-rollover starting for periodKey={lastWeekKey}.");

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        // Load all memberships for last week's period (tracked — mutations must flow to EF).
        // Include League (for tier + cohort metadata) and StudentXpProfile (for CurrentTier update).
        var allMemberships = await db.LeagueMemberships
            .Include(m => m.League)
            .Include(m => m.StudentXpProfile)
            .Where(m => m.PeriodKey == lastWeekKey)
            .ToListAsync(ct);

        if (allMemberships.Count == 0)
        {
            _logger.LogInfo($"P4-07: league-rollover for periodKey={lastWeekKey} — no memberships; nothing to do.");
            return;
        }

        var opts = _options.Value;

        // Group by LeagueId — each cohort processed independently.
        var byLeague = allMemberships.GroupBy(m => m.LeagueId).ToList();

        int totalPromoted = 0;
        int totalDemoted  = 0;
        int totalStayed   = 0;
        int skippedCohorts = 0;

        // Capture tier-changed entries for post-commit event publishing.
        var tierChangedEntries = new List<(int StudentId, string OldTier, string NewTier, string Status)>();

        foreach (var cohortGroup in byLeague)
        {
            var cohortMembers = cohortGroup.ToList();

            // ── Idempotency short-circuit (AC4): if any member already has TierAfter set,
            //    this cohort was already processed (e.g., job ran twice this Monday). Skip.
            if (cohortMembers.Any(m => m.TierAfter != null))
            {
                skippedCohorts++;
                continue;
            }

            // Tier is uniform within a cohort (all members joined the same League row).
            var tier = cohortMembers[0].League.Tier;

            // Sort: WeeklyXp DESC, JoinedAtUtc ASC (D4 tiebreak).
            var ranked = cohortMembers
                .OrderByDescending(m => m.WeeklyXp)
                .ThenBy(m => m.JoinedAtUtc)
                .ToList();

            // Compute promotion/demotion plan + assign status per member.
            var assignments = LeagueStandings.Apply(
                ranked,
                tier,
                opts.PromoteCount,
                opts.DemoteCount);

            // Apply assignments to tracked entities.
            for (int i = 0; i < ranked.Count; i++)
            {
                var member                                       = ranked[i];
                var (membershipId, status, finalRank, tierAfter) = assignments[i];

                // FinalizePromotion uses the public method so Infrastructure can mutate internal fields.
                member.FinalizePromotion(finalRank, tierAfter, status);

                // Update the student's profile CurrentTier so next week's lazy placement
                // reads the correct tier.
                if (status == MembershipStatus.Promoted || status == MembershipStatus.Demoted)
                {
                    member.StudentXpProfile.UpdateTier(tierAfter);

                    // Capture for post-commit event publishing (P4-09 forward-compat).
                    tierChangedEntries.Add((
                        StudentId: member.StudentXpProfile.StudentId,
                        OldTier: tier.ToString(),
                        NewTier: tierAfter.ToString(),
                        Status: status.ToString()));
                }

                switch (status)
                {
                    case MembershipStatus.Promoted: totalPromoted++; break;
                    case MembershipStatus.Demoted:  totalDemoted++;  break;
                    default:                        totalStayed++;   break;
                }
            }
        }

        // Commit all mutations in one SaveChanges call (per-job scope owns its own DbContext).
        // GamificationDbContext.SaveChangesAsync(int userId) stamps audit fields;
        // userId = 0 (system job, no user context).
        await db.SaveChangesAsync(0);

        // Publish LeagueTierChangedIntegrationEvent per promoted/demoted student AFTER commit.
        // Each publish is individually try/caught — a publish failure does NOT roll back the
        // tier update (fail-soft per ADR 0002 §3).
        // Note: per P4-09 brief §"Not in scope", a league-tier push notification is deferred to
        // a follow-up story. These events are published for forward-compatibility.
        int tierEventsPublished = 0;
        int tierEventsFailedPublish = 0;

        foreach (var entry in tierChangedEntries)
        {
            try
            {
                await publisher.Publish(new LeagueTierChangedIntegrationEvent(
                    EventId: Guid.NewGuid(),
                    OccurredOnUtc: now,
                    StudentId: entry.StudentId,
                    OldTier: entry.OldTier,
                    NewTier: entry.NewTier,
                    Status: entry.Status), ct);

                tierEventsPublished++;
            }
            catch (Exception ex)
            {
                tierEventsFailedPublish++;
                _logger.LogError(ex,
                    $"P4-09: LeagueRolloverJob — publish failed for studentId={entry.StudentId} " +
                    $"status={entry.Status}.");
            }
        }

        _logger.LogInfo(
            $"P4-07: league-rollover complete periodKey={lastWeekKey} — " +
            $"promoted={totalPromoted}, demoted={totalDemoted}, stayed={totalStayed}, " +
            $"skippedCohorts={skippedCohorts}, " +
            $"tierEventsPublished={tierEventsPublished}, tierEventsFailedPublish={tierEventsFailedPublish}.");
    }
}
