namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Raised when a child's streak first reaches a milestone (3/7/14/30 days).
/// Re-published by <c>StreakMilestoneReachedRepublisher</c> (Gamification) so the
/// Notifications module can dispatch a celebratory "streak milestone" nudge without
/// referencing Gamification.Domain (module isolation rule 1).
///
/// <para>Consumed by P9-06 Notifications — Achievement category, code STREAK_MILESTONE.
/// Producer-side dedupe by construction: <c>StreakAdvancedDomainEvent</c> increments by 1
/// per day-transition, so each threshold value is crossed exactly once per streak episode.
/// The consumer retains the existing Redis (child, category, day) dedupe as a
/// duplicate-delivery guard.</para>
///
/// Payload carries opaque int IDs + numeric milestone only — NO PII.
/// Mirrors <see cref="StreakFreezeConsumedIntegrationEvent"/> shape.
/// </summary>
public sealed record StreakMilestoneReachedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int StudentId,
    int Milestone) : IIntegrationEvent;
