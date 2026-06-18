namespace Learnexia.Modules.Billing.Application.Features.Seats.Dtos;

/// <summary>
/// Response DTO for the seat-status query (P10-14-BE-9 / P10-15-BE-6 extended).
/// Exposes the family seat capacity + per-child seat lifecycle status.
/// </summary>
public sealed record SeatStatusDto(
    int IncludedSeats,
    int PurchasedExtraSeats,
    int TotalSeats,
    int OccupiedSeats,
    int AvailableSeats,
    int MaxSeats,
    IReadOnlyList<ChildSeatItemDto> Children)
{
    // ── P10-15-BE-6: Grace window info ──────────────────────────────────────────────

    /// <summary>
    /// UTC timestamp when the seat grace window ends, if one is currently open.
    /// Null when no grace window is active.
    /// </summary>
    public DateTime? GraceEndsAt { get; init; }

    /// <summary>
    /// True when a seat grace window is currently open (GraceEndsAt &gt; now).
    /// </summary>
    public bool IsInGrace { get; init; }
}

/// <summary>Per-child seat lifecycle status row within <see cref="SeatStatusDto"/> (P10-15 extended).</summary>
public sealed record ChildSeatItemDto(
    int    ChildId,
    string Status)
{
    // ── P10-15-BE-6: seat lifecycle state + scheduling ───────────────────────────

    /// <summary>
    /// Seat lifecycle state: "Active" or "NoSeatLocked". Derived from <c>SeatReservation.SeatState</c>.
    /// </summary>
    public string SeatState { get; init; } = "Active";

    /// <summary>
    /// UTC timestamp when this child's seat is scheduled for voluntary removal at cycle end.
    /// Null when no removal is pending.
    /// </summary>
    public DateTime? RemovalScheduledAt { get; init; }
}
