using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Billing.Domain.Entities;

/// <summary>
/// Aggregate root for a child's credit (energy) balance.
///
/// <para>One account per child. <see cref="ChildId"/> is a plain int — no cross-module FK
/// (module isolation rule 1). Balances are always non-negative. The concurrency token
/// <see cref="Version"/> maps to the Npgsql <c>xmin</c> system column for optimistic
/// concurrency on debits.</para>
///
/// <para>P10-04 daily-cap columns (<see cref="DailyUsed"/> / <see cref="DailyUsedDateLocal"/>
/// / <see cref="ChildTimeZoneId"/>) are folded into <c>InitialBilling</c> so Wave 2 needs
/// no extra migration.</para>
///
/// <para>Mutation methods return the matching <see cref="CreditTransaction"/> ready to be
/// inserted by the caller in an explicit transaction. They never call SaveChanges.</para>
/// </summary>
public class CreditAccount : FullAuditedEntity
{
    // ── Child reference (loose — no cross-module FK) ──────────────────────────────
    public int ChildId { get; set; }

    // ── Balances ──────────────────────────────────────────────────────────────────

    /// <summary>Monthly-grant pool (expires). Never negative.</summary>
    public int GrantedBalance { get; set; }

    /// <summary>Pack-purchase pool (never expires). Never negative.</summary>
    public int PurchasedBalance { get; set; }

    /// <summary>Derived total. Always <c>GrantedBalance + PurchasedBalance</c>.</summary>
    public int TotalBalance => GrantedBalance + PurchasedBalance;

    // ── Grant tracking ────────────────────────────────────────────────────────────

    /// <summary>When the current monthly grant expires (null when no active grant).</summary>
    public DateTime? GrantExpiresAtUtc { get; set; }

    // ── P10-04 daily-cap columns (folded in — no extra migration needed in W2) ───

    /// <summary>Credits spent today (lazy-reset to 0 when <see cref="DailyUsedDateLocal"/> is stale).</summary>
    public int DailyUsed { get; set; }

    /// <summary>
    /// The calendar date (child's local timezone) for which <see cref="DailyUsed"/> was last
    /// incremented. Stored as a plain date string <c>yyyy-MM-dd</c> for portability.
    /// When this differs from today in <see cref="ChildTimeZoneId"/>, <see cref="DailyUsed"/> is reset to 0.
    /// </summary>
    public string? DailyUsedDateLocal { get; set; }

    /// <summary>IANA timezone id for the child (e.g. <c>Africa/Cairo</c>). Used to compute local "today".</summary>
    public string ChildTimeZoneId { get; set; } = "Africa/Cairo";

    // ── Optimistic-concurrency token ──────────────────────────────────────────────

    /// <summary>
    /// Maps to the Npgsql <c>xmin</c> system column. EF uses this as the concurrency token
    /// on Update so concurrent debits trigger <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>
    /// and the handler can retry.
    /// </summary>
    public uint Version { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────────────
    public ICollection<CreditTransaction> Transactions { get; set; } = new List<CreditTransaction>();

    // ── Factory ───────────────────────────────────────────────────────────────────

    /// <summary>Creates a new empty account for a child.</summary>
    public static CreditAccount CreateEmpty(int childId, string childTimeZoneId = "Africa/Cairo")
        => new()
        {
            ChildId = childId,
            GrantedBalance = 0,
            PurchasedBalance = 0,
            ChildTimeZoneId = childTimeZoneId,
        };

    // ── Mutators ──────────────────────────────────────────────────────────────────
    // Each mutator returns the ledger row to insert; the caller handles the DB write.

    /// <summary>Applies a monthly energy grant (overwrites any previous unexpired grant balance).</summary>
    public CreditTransaction ApplyGrant(int amount, DateTime expiresAtUtc, CreditReasonCode reasonCode, string idempotencyKey)
    {
        GrantedBalance += amount;
        GrantExpiresAtUtc = expiresAtUtc;
        return BuildTransaction(CreditTransactionType.Grant, CreditPool.Granted, amount, reasonCode, idempotencyKey);
    }

    /// <summary>
    /// Debits credits, drawing from <see cref="GrantedBalance"/> first then
    /// <see cref="PurchasedBalance"/>. Throws <see cref="InvalidOperationException"/> if
    /// <see cref="TotalBalance"/> is insufficient — the caller must guard with a pre-check.
    /// </summary>
    public CreditTransaction Debit(
        int fromGranted,
        int fromPurchased,
        CreditReasonCode reasonCode,
        string idempotencyKey,
        string? relatedActionId = null)
    {
        if (fromGranted < 0 || fromPurchased < 0)
            throw new ArgumentOutOfRangeException("Debit amounts must be non-negative.");
        if (fromGranted > GrantedBalance || fromPurchased > PurchasedBalance)
            throw new InvalidOperationException("Debit exceeds available balance.");

        GrantedBalance -= fromGranted;
        PurchasedBalance -= fromPurchased;

        var pool = fromGranted > 0 && fromPurchased > 0
            ? CreditPool.Mixed
            : fromGranted > 0
                ? CreditPool.Granted
                : CreditPool.Purchased;

        var tx = BuildTransaction(CreditTransactionType.Spend, pool, fromGranted + fromPurchased, reasonCode, idempotencyKey);
        tx.RelatedActionId = relatedActionId;
        // Record exact split so ReconcileAccountQueryHandler can reconstruct Mixed-pool debits exactly.
        tx.FromGranted = fromGranted;
        tx.FromPurchased = fromPurchased;
        return tx;
    }

    /// <summary>Applies a pack purchase to the purchased pool.</summary>
    public CreditTransaction ApplyPurchase(int amount, string idempotencyKey, string? relatedPaymentId = null)
    {
        PurchasedBalance += amount;
        var tx = BuildTransaction(CreditTransactionType.Purchase, CreditPool.Purchased, amount, CreditReasonCode.PackPurchase, idempotencyKey);
        tx.RelatedPaymentId = relatedPaymentId;
        return tx;
    }

    /// <summary>Expires unused granted balance at cycle end.</summary>
    public CreditTransaction ExpireGrant(string idempotencyKey)
    {
        var expired = GrantedBalance;
        GrantedBalance = 0;
        GrantExpiresAtUtc = null;
        return BuildTransaction(CreditTransactionType.Expiry, CreditPool.Granted, expired, CreditReasonCode.GrantExpiry, idempotencyKey);
    }

    /// <summary>Refunds unspent purchased credits (clamps to available balance).</summary>
    public CreditTransaction Refund(int amount, string idempotencyKey, string? relatedPaymentId = null)
    {
        var refundable = Math.Min(amount, PurchasedBalance);
        PurchasedBalance -= refundable;
        var tx = BuildTransaction(CreditTransactionType.Refund, CreditPool.Purchased, refundable, CreditReasonCode.PackRefund, idempotencyKey);
        tx.RelatedPaymentId = relatedPaymentId;
        return tx;
    }

    /// <summary>Admin manual adjustment (positive adds to granted pool; negative reduces granted pool clamped to 0).</summary>
    public CreditTransaction Adjust(int delta, string idempotencyKey, string? adminNote = null)
    {
        if (delta >= 0)
        {
            GrantedBalance += delta;
            return BuildTransaction(CreditTransactionType.Adjustment, CreditPool.Granted, delta, CreditReasonCode.AdminCredit, idempotencyKey, adminNote);
        }
        else
        {
            var reduction = Math.Min(-delta, GrantedBalance);
            GrantedBalance -= reduction;
            return BuildTransaction(CreditTransactionType.Adjustment, CreditPool.Granted, reduction, CreditReasonCode.AdminDebit, idempotencyKey, adminNote);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private CreditTransaction BuildTransaction(
        CreditTransactionType type,
        CreditPool pool,
        int amount,
        CreditReasonCode reasonCode,
        string idempotencyKey,
        string? reason = null)
        => new()
        {
            CreditAccount = this,
            Type = type,
            Pool = pool,
            Amount = amount,
            ReasonCode = reasonCode,
            Reason = reason,
            ResultingGrantedBalance = GrantedBalance,
            ResultingPurchasedBalance = PurchasedBalance,
            OccurredAtUtc = DateTime.UtcNow,
            IdempotencyKey = idempotencyKey,
        };
}
