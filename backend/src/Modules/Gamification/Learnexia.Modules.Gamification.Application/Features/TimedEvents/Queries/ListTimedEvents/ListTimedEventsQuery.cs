using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.TimedEvents.Queries.ListTimedEvents;

/// <summary>
/// Admin read query — returns all <c>TimedEvent</c> catalog rows (active + future + past),
/// ordered by <c>StartUtc DESC</c>, capped at 200 rows (P4-11-B4).
///
/// READ-ONLY query: <c>ValidationBehavior</c> does NOT run (IQuery, not ICommand).
/// No parameters — returns the full visible catalog; admin filtering can be added in P7.
/// </summary>
public sealed record ListTimedEventsQuery : IQuery<BaseResponse<IReadOnlyList<TimedEventListItemDto>>>;

/// <summary>
/// Single row in the admin timed-event catalog listing.
///
/// <see cref="Scope"/> is surfaced as a string (<c>nameof</c> of the enum member) for admin
/// readability — e.g. "AllXp", "MissionXp" — rather than a raw integer.
/// <see cref="IsActive"/> reflects the current <c>TimedEvent.IsActive</c> flag (sweep-job state).
/// </summary>
public sealed record TimedEventListItemDto(
    int Id,
    string Code,
    string NameEn,
    string NameAr,
    decimal Multiplier,
    DateTime StartUtc,
    DateTime EndUtc,
    bool IsActive,
    string Scope);
