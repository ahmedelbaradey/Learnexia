using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="ISeatStateQuery"/> (P10-15-BE-8).
///
/// <para>Cross-module seam: used by <c>CreditSpendService</c> to gate AI energy spend.
/// Answers "does this child hold an active seat?" via a direct DB lookup. No cross-module FK;
/// takes a plain <c>int childId</c>.</para>
/// </summary>
public sealed class SeatStateQuery : ISeatStateQuery
{
    private readonly BillingDbContext _db;

    public SeatStateQuery(BillingDbContext db)
        => _db = db;

    /// <inheritdoc />
    public async Task<bool> IsChildSeatActiveAsync(int childId, CancellationToken ct = default)
    {
        return await _db.SeatReservations
            .AsNoTracking()
            .AnyAsync(
                r => r.ChildId == childId
                  && (r.Status == SeatStatus.Active || r.Status == SeatStatus.Reserved)
                  && r.SeatState == SeatState.Active,
                ct);
    }
}
