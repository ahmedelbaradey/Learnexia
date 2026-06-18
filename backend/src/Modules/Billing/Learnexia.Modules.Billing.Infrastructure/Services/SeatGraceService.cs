using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="ISeatGraceService"/> (P10-15-BE-2).
///
/// <para>Opens or no-ops a payment-failure seat grace window on the subscription. All writes use
/// the ambient <see cref="IBillingDbContext"/>; the caller is responsible for the outer transaction.</para>
/// </summary>
public sealed class SeatGraceService : ISeatGraceService
{
    private readonly IBillingDbContext _db;
    private readonly IGlobalSettingsProvider _settings;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;

    public SeatGraceService(
        IBillingDbContext db,
        IGlobalSettingsProvider settings,
        ICurrentUserService currentUser,
        ILoggerManager logger)
    {
        _db          = db;
        _settings    = settings;
        _currentUser = currentUser;
        _logger      = logger;
    }

    /// <inheritdoc />
    public async Task StartPaymentFailureGraceAsync(int subscriptionId, CancellationToken ct = default)
    {
        var subscription = await _db.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);

        if (subscription is null)
        {
            _logger.LogInfo($"SeatGraceService: subscription {subscriptionId} not found — no-op.");
            return;
        }

        var nowUtc = DateTime.UtcNow;

        // Idempotency: if a grace window is already open (GraceEndsAt > now), do not touch it.
        if (subscription.GraceEndsAt.HasValue && subscription.GraceEndsAt.Value > nowUtc)
        {
            _logger.LogInfo(
                $"SeatGraceService: subscriptionId={subscriptionId} already has an open grace window " +
                $"ending {subscription.GraceEndsAt:u} — no-op.");
            return;
        }

        var graceDays = _settings.GetInt(GlobalSettingKeys.SeatsGraceDays, 7);
        subscription.GraceEndsAt       = nowUtc.AddDays(graceDays);
        subscription.SeatGraceStartedAt = nowUtc;
        subscription.SeatGraceReason    = SeatGraceReason.PaymentFailure;

        await _db.SaveChangesAsync(_currentUser.UserId ?? 0);

        _logger.LogInfo(
            $"SeatGraceService: opened {graceDays}-day seat grace for subscriptionId={subscriptionId}, " +
            $"ends={subscription.GraceEndsAt:u}.");
    }
}
