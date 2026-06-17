using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IEnergyPackService"/> (Option C).
///
/// <para><strong>P10-13-BE-12 re-home:</strong> <see cref="CreditPurchasedPackAsync"/> now credits
/// the <strong>shared family <c>FamilyEnergyAccount.PurchasedBalance</c></strong> (not the old
/// per-child <c>CreditAccount.PurchasedBalance</c>). Resolves the family wallet from the
/// pack <c>Payment.ParentUserId</c> (the parent who paid). Writes a bucket-B <c>Purchase</c>
/// ledger row with <c>SourceBucket = Purchased</c> and <c>RelatedPaymentId</c> for P10-17 FIFO
/// refund reconciliation. The old per-child <c>CreditAccount</c> write path is removed.</para>
///
/// <para><strong>Idempotency:</strong> guarded by TWO independent keys:
/// <list type="number">
///   <item>The <c>WebhookEvent.ProviderEventId</c> unique constraint (outer guard — caller's responsibility).</item>
///   <item><c>CreditTransaction.IdempotencyKey = "pack-credit:{paymentId}:{providerEventId}"</c> (inner guard).</item>
/// </list>
/// A unique-key violation on the credit key is treated as an idempotent success.</para>
/// </summary>
public sealed class EnergyPackService : IEnergyPackService
{
    private readonly BillingDbContext _db;
    private readonly IPaymentProvider _paymentProvider;
    private readonly IGlobalSettingsProvider _settings;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;

    public EnergyPackService(
        BillingDbContext db,
        IPaymentProvider paymentProvider,
        IGlobalSettingsProvider settings,
        IParentChildQuery parentChildQuery,
        ICurrentUserService currentUser,
        ILoggerManager logger)
    {
        _db               = db;
        _paymentProvider  = paymentProvider;
        _settings         = settings;
        _parentChildQuery = parentChildQuery;
        _currentUser      = currentUser;
        _logger           = logger;
    }

    // ── StartPackCheckoutAsync ────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<PackCheckoutResult> StartPackCheckoutAsync(
        int parentId,
        int childId,
        string idempotencyKey,
        CancellationToken ct)
    {
        // ── 1. IDOR / family-scope guard ──────────────────────────────────────────
        var parentOwnsChild = await _parentChildQuery.IsParentOfChildAsync(parentId, childId, ct);
        if (!parentOwnsChild)
        {
            _logger.LogInfo(
                $"EnergyPackService.StartPackCheckout: parentId={parentId} does not own childId={childId} — rejected.");
            return PackCheckoutResult.Fail(PackCheckoutFailureReason.ChildNotOwnedByParent);
        }

        // ── 2. Resolve pack size + price SERVER-SIDE ───────────────────────────────
        var packSize  = _settings.GetInt(GlobalSettingKeys.PackSize, 1000);
        var packPrice = _settings.GetDecimal(GlobalSettingKeys.PackPriceEgp, 50m);

        // ── 3. Create the Payment row (Initiated) ──────────────────────────────────
        var payment = new Payment
        {
            ParentUserId   = parentId,
            TargetChildId  = childId,
            Amount         = packPrice,
            Currency       = PaymentCurrency.EGP,
            Status         = PaymentStatus.Initiated,
            Kind           = PaymentKind.Pack,
            IdempotencyKey = idempotencyKey,
            CreatedAt      = DateTime.UtcNow,
            CreatedBy      = _currentUser.UserId ?? 0,
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(_currentUser.UserId ?? 0);

        // ── 4. Call the payment provider ──────────────────────────────────────────
        CheckoutSession session;
        try
        {
            session = await _paymentProvider.CreateCheckoutSessionAsync(payment, ct);
        }
        catch (Exception provEx)
        {
            _logger.LogError(provEx,
                $"EnergyPackService: provider call failed for parentId={parentId}, childId={childId}.");
            return PackCheckoutResult.Fail(PackCheckoutFailureReason.ProviderUnavailable);
        }

        // ── 5. Persist the provider ref ───────────────────────────────────────────
        payment.ProviderPaymentRef = session.ProviderPaymentRef;
        await _db.SaveChangesAsync(_currentUser.UserId ?? 0);

        _logger.LogInfo(
            $"EnergyPackService: pack checkout created paymentId={payment.Id}, parentId={parentId}, childId={childId}.");

        return PackCheckoutResult.Ok(payment.Id, session.RedirectUrl, session.ProviderPaymentRef);
    }

    // ── CreditPurchasedPackAsync ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    /// <remarks>
    /// P10-13-BE-12: credits the SHARED family <c>FamilyEnergyAccount.PurchasedBalance</c>.
    /// The <paramref name="targetChildId"/> parameter is preserved for signature compatibility
    /// but is now used only to look up the parent (family wallet owner) via the payment row.
    /// </remarks>
    public async Task<PackCreditResult> CreditPurchasedPackAsync(
        int paymentId,
        int targetChildId,
        string providerEventId,
        CancellationToken ct)
    {
        var creditIdempotencyKey = $"pack-credit:{paymentId}:{providerEventId}";

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Inner idempotency pre-check.
            var alreadyCredited = await _db.CreditTransactions
                .AnyAsync(t => t.IdempotencyKey == creditIdempotencyKey, ct);

            if (alreadyCredited)
            {
                await tx.RollbackAsync(ct);
                _logger.LogInfo(
                    $"EnergyPackService.CreditPurchasedPack: duplicate credit key for paymentId={paymentId} — no-op.");
                return PackCreditResult.Duplicate();
            }

            // Resolve parent from payment.
            var payment = await _db.Payments.FindAsync(new object[] { paymentId }, ct);
            if (payment is null)
            {
                await tx.RollbackAsync(ct);
                _logger.LogInfo($"EnergyPackService: payment {paymentId} not found — skip credit.");
                return PackCreditResult.Fail();
            }

            var parentId = payment.ParentUserId;

            // Load or create the FAMILY wallet (P10-13-BE-12: credit shared PurchasedBalance).
            var wallet = await _db.FamilyEnergyAccounts
                .FirstOrDefaultAsync(w => w.ParentUserId == parentId, ct);

            if (wallet is null)
            {
                wallet = FamilyEnergyAccount.CreateEmpty(parentId);
                _db.FamilyEnergyAccounts.Add(wallet);
                await _db.SaveChangesAsync(_currentUser.UserId ?? 0);
            }

            // Pack size from GlobalSettings (server-side authoritative).
            var packSize = _settings.GetInt(GlobalSettingKeys.PackSize, 1000);

            // Apply the purchase to the shared family PurchasedBalance.
            var creditTx = wallet.ApplyPurchase(
                amount           : packSize,
                idempotencyKey   : creditIdempotencyKey,
                relatedPaymentId : paymentId.ToString());

            // creditTx.FamilyEnergyAccountId is set by FamilyEnergyAccount.BuildTransaction via Id.
            _db.CreditTransactions.Add(creditTx);

            // Flip Payment → Succeeded.
            payment.Status = PaymentStatus.Succeeded;

            await _db.SaveChangesAsync(_currentUser.UserId ?? 0);
            await tx.CommitAsync(ct);

            _logger.LogInfo(
                $"EnergyPackService: pack credited {packSize} to family wallet parentId={parentId}, paymentId={paymentId}.");

            return PackCreditResult.Ok(packSize);
        }
        catch (DbUpdateException dbEx) when (IsUniqueViolation(dbEx))
        {
            await tx.RollbackAsync(ct);
            _logger.LogInfo(
                $"EnergyPackService: concurrent credit duplicate for paymentId={paymentId} — no-op.");
            return PackCreditResult.Duplicate();
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────────────────

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is Npgsql.PostgresException pg
           && pg.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation;
}
