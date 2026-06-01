namespace Learnexia.Modules.Gamification.Domain.Enums;

/// <summary>
/// Lifecycle state of a <see cref="Entities.LeagueMembership"/>.
/// Default Active during the week; stamped by LeaguePromotionJob on Monday rollover.
/// </summary>
public enum MembershipStatus
{
    Active   = 0,
    Promoted = 1,
    Demoted  = 2,
    Stayed   = 3,
}
