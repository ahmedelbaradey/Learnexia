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
/// <para>Owns ALL EF Core access, transaction management, and ledger logic for energy-pack
/// purchases. Application-layer handlers (which implement the Option C rule) inject
/// <see cref="IEnergyPackService"/> and never reference EF or <c>BillingDbContext</c>.</para>
///
/// <para><strong>Atomicity (StartPackCheckoutAsync):</strong> the Payment row is written before
/// the provider call; a second SaveChanges persists the provider ref on return.
/// On provider failure the Payment row stays <c>Initiated</c> — the reconcile job sweeps it.</para>
///
/// <para><strong>Atomicity (CreditPurchasedPackAsync):</strong> one explicit transaction:
/// create/load CreditAccount → apply purchase mutator → insert CreditTransaction → flip
/// Payment.Status to Succeeded → commit. The <c>CreditTransaction.IdempotencyKey</c>
/// DB-unique constraint is the inner idempotency guard; the outer guard is the
/// <c>WebhookEvent.ProviderEventId</c> unique constraint (enforced by the caller before this
/// method is invoked). A unique-key violation on the credit key is treated as a duplicate
/// success (no second credit).</para>
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
        // Anti-IDOR: return a generic failure (not "child not found") to avoid enumeration.
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

        // ── 4. Call the payment provider (outside transaction — can be slow) ───────
        CheckoutSession session;
        try
        {
            session = await _paymentProvider.CreateCheckoutSessionAsync(payment, ct);
        }
        catch (Exception provEx)
        {
            _logger.LogError(provEx,
                $"EnergyPackService: provider call failed for parentId={parentId}, childId={childId}.");
            // Payment stays Initiated — the reconcile job will sweep it.
            return PackCheckoutResult.Fail(PackCheckoutFailureReason.ProviderUnavailable);
        }

        // ── 5. Persist the provider ref ────────────────────────────────────────────
        payment.ProviderPaymentRef = session.ProviderPaymentRef;
        await _db.SaveChangesAsync(_currentUser.UserId ?? 0);

        _logger.LogInfo(
            $"EnergyPackService: pack checkout created paymentId={payment.Id}, parentId={parentId}, childId={childId}, packSize={packSize}, price={packPrice}.");

        return PackCheckoutResult.Ok(payment.Id, session.RedirectUrl, session.ProviderPaymentRef);
    }

    // ── CreditPurchasedPackAsync ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<PackCreditResult> CreditPurchasedPackAsync(
        int paymentId,
        int targetChildId,
        string providerEventId,
        CancellationToken ct)
    {
        // Idempotency key: ensures a second call with the same providerEventId cannot double-credit.
        var creditIdempotencyKey = $"pack-credit:{paymentId}:{providerEventId}";

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // ── Inner idempotency pre-check (guard against race with a concurrent replay) ──
            var alreadyCredited = await _db.CreditTransactions
                .AnyAsync(t => t.IdempotencyKey == creditIdempotencyKey, ct);

            if (alreadyCredited)
            {
                await tx.RollbackAsync(ct);
                _logger.LogInfo(
                    $"EnergyPackService.CreditPurchasedPack: duplicate credit key for paymentId={paymentId} — no-op.");
                return PackCreditResult.Duplicate();
            }

            // ── Load or create the child's CreditAccount ───────────────────────────────
            var account = await _db.CreditAccounts
                .FirstOrDefaultAsync(a => a.ChildId == targetChildId, ct);

            if (account is null)
            {
                account = CreditAccount.CreateEmpty(targetChildId);
                _db.CreditAccounts.Add(account);
                await _db.SaveChangesAsync(_currentUser.UserId ?? 0);
            }

            // ── Resolve pack size from GlobalSettings (server-side — never from provider payload) ──
            var packSize = _settings.GetInt(GlobalSettingKeys.PackSize, 1000);

            // ── Apply the purchase mutator — returns the ledger row to insert ────────
            var creditTransaction = account.ApplyPurchase(
                amount           : packSize,
                idempotencyKey   : creditIdempotencyKey,
                relatedPaymentId : paymentId.ToString());

            _db.CreditTransactions.Add(creditTransaction);

            // ── Flip Payment → Succeeded ───────────────────────────────────────────────
            var payment = await _db.Payments.FindAsync(new object[] { paymentId }, ct);
            if (payment is not null)
                payment.Status = PaymentStatus.Succeeded;

            await _db.SaveChangesAsync(_currentUser.UserId ?? 0);
            await tx.CommitAsync(ct);

            _logger.LogInfo(
                $"EnergyPackService: pack credited {packSize} to childId={targetChildId}, paymentId={paymentId}.");

            return PackCreditResult.Ok(packSize);
        }
        catch (DbUpdateException dbEx) when (IsUniqueViolation(dbEx))
        {
            // Concurrent duplicate insert — treat as idempotent success.
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
        => ex.InnerException?.Message.Contains("23505", StringComparison.Ordinal) == true
           || ex.InnerException?.Message.Contains("UX_CreditTransactions_IdempotencyKey",
               StringComparison.OrdinalIgnoreCase) == true;
}
