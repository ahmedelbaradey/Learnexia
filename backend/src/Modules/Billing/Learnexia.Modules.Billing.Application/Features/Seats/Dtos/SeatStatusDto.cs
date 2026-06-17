namespace Learnexia.Modules.Billing.Application.Features.Seats.Dtos;

/// <summary>
/// Response DTO for the seat-status query (P10-14-BE-9).
/// Exposes the family seat capacity + per-child seat status.
/// </summary>
public sealed record SeatStatusDto(
    int IncludedSeats,
    int PurchasedExtraSeats,
    int TotalSeats,
    int OccupiedSeats,
    int AvailableSeats,
    int MaxSeats,
    IReadOnlyList<ChildSeatItemDto> Children);

/// <summary>Per-child seat status row within <see cref="SeatStatusDto"/>.</summary>
public sealed record ChildSeatItemDto(
    int    ChildId,
    string Status);
