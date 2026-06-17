using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;

namespace Learnexia.Modules.Billing.Infrastructure.Contracts;

/// <summary>
/// TEMPORARY in-Billing implementation of <see cref="ISeatQuery"/> (P10-13-BE-8).
///
/// <para>Returns active child ids via <see cref="IParentChildQuery.GetChildIdsForParentAsync"/>
/// (OQ-B seam) and seat count = child count (1 seat per included child, no extra seats yet).
/// This keeps <c>ActivePaidSeats == ActiveChildIds.Count</c> so the equal-split allocation
/// produces the right denominator.</para>
///
/// <para><strong>CUTOVER FLAG (P10-14-BE-3a):</strong> this impl is replaced when P10-14 (seats)
/// lands. At that point the real <c>SeatReservation</c>-backed impl takes over the
/// <see cref="ISeatQuery"/> registration, and this class is deleted or left unreferenced.</para>
/// </summary>
public sealed class TemporarySeatQuery : ISeatQuery
{
    private readonly IParentChildQuery _parentChildQuery;
    private readonly ILoggerManager _logger;

    public TemporarySeatQuery(IParentChildQuery parentChildQuery, ILoggerManager logger)
    {
        _parentChildQuery = parentChildQuery;
        _logger           = logger;
    }

    /// <inheritdoc/>
    public async Task<ActiveSeatInfo> GetActiveSeatsAsync(int parentUserId, CancellationToken ct = default)
    {
        var childIds = await _parentChildQuery.GetChildIdsForParentAsync(parentUserId, ct);

        _logger.LogInfo(
            $"TemporarySeatQuery (P10-13 placeholder): parentUserId={parentUserId}, " +
            $"activeChildCount={childIds.Count}. Replace with real SeatReservation impl in P10-14.");

        return new ActiveSeatInfo(
            ActivePaidSeats: childIds.Count,
            ActiveChildIds : childIds);
    }
}
