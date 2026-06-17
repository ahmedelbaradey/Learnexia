using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Billing.Domain.Entities;

/// <summary>
/// Aggregate root for the parent/family energy wallet (P10-13).
///
/// <para>One account per parent (<see cref="ParentUserId"/> unique index). Holds TWO
/// NON-CONVERTIBLE energy buckets:
/// <list type="bullet">
///   <item><see cref="SubscriptionBalance"/> — temporary, monthly grant; resets each cycle;
///         CANNOT be converted to purchased.</item>
///   <item><see cref="PurchasedBalance"/> — permanent, pack-purchase reserve; shared by all
///         family children; NEVER expires; NEVER converts to subscription.</item>
/// </list>
/// </para>
///
/// <para>Mutation methods return the matching <see cref="CreditTransaction"/> ready to be
/// inserted by the caller in an explicit transaction. They NEVER call SaveChanges.
/// There is intentionally NO method that moves energy between the two buckets.</para>
///
/// <para>Module isolation: <see cref="ParentUserId"/> is a plain int — no cross-module FK.</para>
///
/// <para><see cref="Version"/> maps to the Npgsql <c>xmin</c> system column for optimistic
/// concurrency on the shared purchased-balance debit path (HARDEN-01 fallback guard).</para>
/// </summary>
public class FamilyEnergyAccount : FullAuditedEntity
{
    // ── Parent reference (loose — no cross-module FK) ─────────────────────────────
    public int ParentUserId { get; set; }

    // ── Bucket A: subscription (temporary) ───────────────────────────────────────

    /// <summary>
    /// Temporary monthly-grant balance. Deposited by the grant job, allocated to children.
    /// Resets each cycle. CANNOT convert to <see cref="PurchasedBalance"/>.
    /// </summary>
    public int SubscriptionBalance { get; set; }

    /// <summary>When the current subscription allocation cycle expires; null when no active cycle.</summary>
    public DateTime? SubscriptionExpiresAtUtc { get; set; }

    // ── Bucket B: purchased (permanent, shared family reserve) ────────────────────

    /// <summary>
    /// Permanent pack-purchase balance. Shared by all children; touched ONLY when a child's
    /// own <see cref="ChildEnergyAllocation"/> cannot cover the spend (shortfall-fallback).
    /// CANNOT convert to <see cref="SubscriptionBalance"/>.
    /// </summary>
    public int PurchasedBalance { get; set; }

    // ── Optimistic-concurrency token (xmin) ──────────────────────────────────────

    /// <summary>
    /// Maps to the Npgsql <c>xmin</c> system column. Guards the shared purchased-balance
    /// debit path (HARDEN-01) against concurrent fallback writes. Purchased-first debits
    /// use this token; normal allocation-covered debits never touch this row.
    /// </summary>
    public uint Version { get; set; }

    // ── Navigations ───────────────────────────────────────────────────────────────
    public ICollection<ChildEnergyAllocation> Allocations { get; set; } = new List<ChildEnergyAllocation>();

    // ── Factory ───────────────────────────────────────────────────────────────────

    /// <summary>Creates a new empty family wallet for a parent.</summary>
    public static FamilyEnergyAccount CreateEmpty(int parentUserId)
        => new()
        {
            ParentUserId       = parentUserId,
            SubscriptionBalance = 0,
            PurchasedBalance   = 0,
        };

    // ── Mutators — each returns the CreditTransaction to insert; caller persists ──

    /// <summary>
    /// Deposits a monthly subscription grant into <see cref="SubscriptionBalance"/>.
    /// Called once per family per cycle by the grant job.
    /// </summary>
    public CreditTransaction DepositSubscriptionGrant(
        int amount,
        DateTime cycleEndsAtUtc,
        string idempotencyKey)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Grant amount must be non-negative.");
        SubscriptionBalance   += amount;
        SubscriptionExpiresAtUtc = cycleEndsAtUtc;
        return BuildTransaction(
            type          : CreditTransactionType.Grant,
            bucket        : EnergyBucket.Subscription,
            amount        : amount,
            reasonCode    : CreditReasonCode.GrantAllocation,
            idempotencyKey: idempotencyKey);
    }

    /// <summary>
    /// Expires the current subscription cycle: zeroes <see cref="SubscriptionBalance"/>
    /// and clears <see cref="SubscriptionExpiresAtUtc"/>. <see cref="PurchasedBalance"/> untouched.
    /// </summary>
    public CreditTransaction ExpireSubscription(string idempotencyKey)
    {
        var expired = SubscriptionBalance;
        SubscriptionBalance      = 0;
        SubscriptionExpiresAtUtc = null;
        return BuildTransaction(
            type          : CreditTransactionType.Expiry,
            bucket        : EnergyBucket.Subscription,
            amount        : expired,
            reasonCode    : CreditReasonCode.SubscriptionExpire,
            idempotencyKey: idempotencyKey);
    }

    /// <summary>
    /// Credits a pack purchase to the shared <see cref="PurchasedBalance"/>.
    /// Called from <c>EnergyPackService</c> after a verified payment webhook (P10-13-BE-12).
    /// </summary>
    public CreditTransaction ApplyPurchase(
        int amount,
        string idempotencyKey,
        string? relatedPaymentId = null)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Purchase amount must be positive.");
        PurchasedBalance += amount;
        var tx = BuildTransaction(
            type          : CreditTransactionType.Purchase,
            bucket        : EnergyBucket.Purchased,
            amount        : amount,
            reasonCode    : CreditReasonCode.PackPurchase,
            idempotencyKey: idempotencyKey);
        tx.RelatedPaymentId = relatedPaymentId;
        return tx;
    }

    /// <summary>
    /// Debits the shared <see cref="PurchasedBalance"/> by <paramref name="amount"/>
    /// (purchased-fallback path — called ONLY when a child's allocation is exhausted).
    /// Returns the matching ledger row.
    /// </summary>
    public CreditTransaction DebitPurchasedFallback(
        int amount,
        string idempotencyKey,
        int? childEnergyAllocationId = null,
        string? relatedActionId = null)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Debit amount must be positive.");
        if (amount > PurchasedBalance) throw new InvalidOperationException("Purchased fallback debit exceeds available PurchasedBalance.");
        PurchasedBalance -= amount;
        var tx = BuildTransaction(
            type          : CreditTransactionType.Spend,
            bucket        : EnergyBucket.Purchased,
            amount        : amount,
            reasonCode    : CreditReasonCode.SpendPurchasedFallback,
            idempotencyKey: idempotencyKey);
        tx.ChildEnergyAllocationId = childEnergyAllocationId;
        tx.RelatedActionId         = relatedActionId;
        return tx;
    }

    /// <summary>
    /// Claws back unspent pack credits from the shared <see cref="PurchasedBalance"/> on a verified
    /// <c>refund.succeeded</c> webhook (P10-13 CUTOVER re-home of the pack-refund clawback).
    /// CLAMPS to the available balance — never drives <see cref="PurchasedBalance"/> negative — so a
    /// replayed/over-large refund is a safe no-op. Returns a bucket-B <c>Refund</c> ledger row.
    /// (Full FIFO reconciliation is P10-17; this just re-homes the clawback to the shared wallet.)
    /// </summary>
    public CreditTransaction RefundPurchased(
        int amount,
        string idempotencyKey,
        string? relatedPaymentId = null)
    {
        var refundable = Math.Min(Math.Max(0, amount), PurchasedBalance);
        PurchasedBalance -= refundable;
        var tx = BuildTransaction(
            type          : CreditTransactionType.Refund,
            bucket        : EnergyBucket.Purchased,
            amount        : refundable,
            reasonCode    : CreditReasonCode.PackRefund,
            idempotencyKey: idempotencyKey);
        tx.RelatedPaymentId = relatedPaymentId;
        return tx;
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private CreditTransaction BuildTransaction(
        CreditTransactionType type,
        EnergyBucket bucket,
        int amount,
        CreditReasonCode reasonCode,
        string idempotencyKey)
        => new()
        {
            FamilyEnergyAccountId          = Id,
            FamilyEnergyAccount            = this,
            Type                           = type,
            Pool                           = bucket == EnergyBucket.Subscription ? CreditPool.Granted : CreditPool.Purchased,
            SourceBucket                   = bucket,
            Amount                         = amount,
            ReasonCode                     = reasonCode,
            ResultingGrantedBalance        = SubscriptionBalance,
            ResultingPurchasedBalance      = PurchasedBalance,
            OccurredAtUtc                  = DateTime.UtcNow,
            IdempotencyKey                 = idempotencyKey,
        };
}
