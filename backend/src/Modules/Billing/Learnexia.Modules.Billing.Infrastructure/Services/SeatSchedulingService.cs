using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="ISeatSchedulingService"/> (P10-15-BE-3).
///
/// <para>Marks a specific child's <c>SeatReservation</c> with <c>RemovalScheduledAt = cycleEndUtc</c>
/// for cycle-end enforcement. The child keeps <see cref="SeatState.Active"/> and full energy
/// access until enforcement runs at that boundary. No grace; no energy forfeit.</para>
/// </summary>
public sealed class SeatSchedulingService : ISeatSchedulingService
{
    private readonly IBillingDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;

    public SeatSchedulingService(
        IBillingDbContext db,
        ICurrentUserService currentUser,
        ILoggerManager logger)
    {
        _db          = db;
        _currentUser = currentUser;
        _logger      = logger;
    }

    /// <inheritdoc />
    public async Task ScheduleRemovalAtCycleEndAsync(
        int subscriptionId,
        int childId,
        DateTime cycleEndUtc,
        CancellationToken ct = default)
    {
        var reservation = await _db.SeatReservations
            .FirstOrDefaultAsync(
                r => r.SubscriptionId == subscriptionId
                  && r.ChildId        == childId
                  && (r.Status == SeatStatus.Active || r.Status == SeatStatus.Reserved),
                ct);

        if (reservation is null)
        {
            _logger.LogInfo(
                $"SeatSchedulingService: no active reservation for childId={childId} " +
                $"under subscriptionId={subscriptionId} — no-op.");
            return;
        }

        // Idempotency: already scheduled for the same boundary — no-op.
        if (reservation.RemovalScheduledAt.HasValue
            && Math.Abs((reservation.RemovalScheduledAt.Value - cycleEndUtc).TotalSeconds) < 1)
        {
            _logger.LogInfo(
                $"SeatSchedulingService: childId={childId} removal already scheduled at {cycleEndUtc:u} — no-op.");
            return;
        }

        reservation.RemovalScheduledAt = cycleEndUtc;
        await _db.SaveChangesAsync(_currentUser.UserId ?? 0);

        _logger.LogInfo(
            $"SeatSchedulingService: scheduled removal for childId={childId} " +
            $"under subscriptionId={subscriptionId} at {cycleEndUtc:u}.");
    }
}
