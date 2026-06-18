using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IChildAccessStateQuery"/> (P10-18-BE-5).
///
/// <para>Combined cross-module seam: returns <see langword="true"/> ONLY when the child
/// holds an Active seat (<see cref="SeatState.Active"/>) AND has NOT been paused by their
/// parent (<see cref="ParentPauseState.Active"/>). Either condition alone returns
/// <see langword="false"/>.</para>
///
/// <para>No cross-module FK. Takes a plain <c>int childId</c>. Coordinates with
/// <see cref="SeatStateQuery"/> — uses the same <c>SeatReservation</c> row but checks
/// both columns in a single query, avoiding a duplicate seam.</para>
/// </summary>
public sealed class ChildAccessStateQuery : IChildAccessStateQuery
{
    private readonly BillingDbContext _db;

    public ChildAccessStateQuery(BillingDbContext db)
        => _db = db;

    /// <inheritdoc />
    public async Task<bool> IsChildAccessAllowedAsync(int childId, CancellationToken ct = default)
    {
        return await _db.SeatReservations
            .AsNoTracking()
            .AnyAsync(
                r => r.ChildId == childId
                  && (r.Status == SeatStatus.Active || r.Status == SeatStatus.Reserved)
                  && r.SeatState == SeatState.Active
                  && r.ParentPauseState == ParentPauseState.Active,
                ct);
    }

    /// <inheritdoc />
    public async Task<ChildAccessDecision> GetAccessDecisionAsync(int childId, CancellationToken ct = default)
    {
        // Fetch the child's reservation row in a single round-trip.
        // A missing row is treated as SeatLocked — consistent with IsChildAccessAllowedAsync
        // returning false for the no-row case.
        var row = await _db.SeatReservations
            .AsNoTracking()
            .Where(r => r.ChildId == childId
                     && (r.Status == SeatStatus.Active || r.Status == SeatStatus.Reserved))
            .OrderByDescending(r => r.ReservedAt)   // parity with the pause/spend-gate lookups (deterministic newest-first)
            .Select(r => new { r.SeatState, r.ParentPauseState })
            .FirstOrDefaultAsync(ct);

        if (row is null)
            return ChildAccessDecision.SeatLocked;

        if (row.SeatState != SeatState.Active)
            return ChildAccessDecision.SeatLocked;

        if (row.ParentPauseState != ParentPauseState.Active)
            return ChildAccessDecision.Paused;

        return ChildAccessDecision.Allowed;
    }
}
