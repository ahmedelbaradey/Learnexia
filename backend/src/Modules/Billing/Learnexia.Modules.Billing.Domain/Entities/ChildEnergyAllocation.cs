using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Billing.Domain.Entities;

/// <summary>
/// Per-child allocation row within a family energy wallet cycle (P10-13).
///
/// <para>One row per (family, child, cycle). The <see cref="Remaining"/> balance
/// is derived: <c>AllocatedAmount − SpentAmount</c> (never negative).</para>
///
/// <para>Mutation methods return the matching <see cref="CreditTransaction"/> to insert;
/// the caller persists inside an explicit transaction. Never calls SaveChanges.</para>
///
/// <para><see cref="Version"/> maps to the Npgsql <c>xmin</c> concurrency token — guards
/// concurrent allocation-row debits (normal AI spend hot path).</para>
///
/// <para><see cref="SendTransfer"/> / <see cref="ReceiveTransfer"/> stubs are present-but-unused
/// in P10-13 (consumed by P10-16 sibling redistribution). Keep the stubs to maintain the contract.</para>
/// </summary>
public class ChildEnergyAllocation : FullAuditedEntity
{
    // ── FK (within billing schema) ────────────────────────────────────────────────
    public int FamilyEnergyAccountId { get; set; }
    public FamilyEnergyAccount FamilyEnergyAccount { get; set; } = null!;

    // ── Child reference (loose — no cross-module FK) ──────────────────────────────
    public int ChildId { get; set; }

    // ── Cycle ─────────────────────────────────────────────────────────────────────
    public DateTime CycleStartUtc { get; set; }
    public DateTime CycleEndUtc   { get; set; }

    // ── Balances ──────────────────────────────────────────────────────────────────

    /// <summary>Credits allocated to this child for the cycle (sum over the equal-split).</summary>
    public int AllocatedAmount { get; set; }

    /// <summary>Credits spent so far this cycle from this allocation row.</summary>
    public int SpentAmount { get; set; }

    /// <summary>Unspent remaining (derived). Never negative.</summary>
    public int Remaining => Math.Max(0, AllocatedAmount - SpentAmount);

    // ── Optimistic-concurrency token (xmin) ──────────────────────────────────────
    public uint Version { get; set; }

    // ── Mutators ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets (or resets) <see cref="AllocatedAmount"/> for this cycle and appends a
    /// <c>GrantAllocation</c> ledger row. Called by the allocation service on grant.
    /// </summary>
    public CreditTransaction Allocate(
        int amount,
        string idempotencyKey,
        int? familyEnergyAccountId = null)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Allocation amount must be non-negative.");
        AllocatedAmount = amount;
        SpentAmount     = 0; // fresh cycle
        return Build(
            type          : CreditTransactionType.Grant,
            reasonCode    : CreditReasonCode.GrantAllocation,
            amount        : amount,
            idempotencyKey: idempotencyKey);
    }

    /// <summary>
    /// Debits <paramref name="amount"/> from this child's allocation row (allocation-first spend path).
    /// Only called when <see cref="Remaining"/> >= amount (caller must guard).
    /// </summary>
    public CreditTransaction Debit(
        int amount,
        CreditReasonCode reasonCode,
        string idempotencyKey,
        string? relatedActionId = null)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Debit amount must be positive.");
        if (amount > Remaining) throw new InvalidOperationException("Debit exceeds child allocation Remaining balance.");
        SpentAmount += amount;
        var tx = Build(
            type          : CreditTransactionType.Spend,
            reasonCode    : reasonCode,
            amount        : amount,
            idempotencyKey: idempotencyKey);
        tx.RelatedActionId = relatedActionId;
        return tx;
    }

    /// <summary>
    /// P10-16 sibling redistribution — send <paramref name="amount"/> from this child to another.
    /// Present-but-unused in P10-13; implemented in P10-16.
    /// </summary>
    public CreditTransaction SendTransfer(int amount, string idempotencyKey, Guid correlationId)
    {
        if (amount <= 0 || amount > Remaining)
            throw new InvalidOperationException("Transfer amount must be positive and <= Remaining.");
        // Sent energy LEAVES this child's allocation — it was never consumed, so we reduce
        // AllocatedAmount (NOT SpentAmount). Reducing Spent would inflate the parent's "spent"
        // report with energy the child merely handed to a sibling. Symmetric with ReceiveTransfer,
        // which increases AllocatedAmount. amount <= Remaining <= AllocatedAmount, so the result
        // stays >= SpentAmount and Remaining stays >= 0.
        AllocatedAmount -= amount;
        var tx = Build(CreditTransactionType.Spend, CreditReasonCode.TransferOut, amount, idempotencyKey);
        tx.CorrelationId = correlationId;
        return tx;
    }

    /// <summary>
    /// P10-16 sibling redistribution — receive <paramref name="amount"/> from a sibling.
    /// Present-but-unused in P10-13; implemented in P10-16.
    /// </summary>
    public CreditTransaction ReceiveTransfer(int amount, string idempotencyKey, Guid correlationId)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        AllocatedAmount += amount; // increases Remaining without touching SpentAmount
        var tx = Build(CreditTransactionType.Grant, CreditReasonCode.TransferIn, amount, idempotencyKey);
        tx.CorrelationId = correlationId;
        return tx;
    }

    /// <summary>
    /// Expires this child's allocation at cycle rollover. <c>AllocatedAmount</c> zeroed;
    /// <c>SpentAmount</c> unchanged for reconciliation. Returns an <c>Expiry</c> ledger row.
    /// </summary>
    public CreditTransaction Expire(string idempotencyKey)
    {
        var unspent = Remaining;
        AllocatedAmount = SpentAmount; // sets Remaining = 0 while preserving spent history
        return Build(CreditTransactionType.Expiry, CreditReasonCode.SubscriptionExpire, unspent, idempotencyKey);
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private CreditTransaction Build(
        CreditTransactionType type,
        CreditReasonCode reasonCode,
        int amount,
        string idempotencyKey)
        => new()
        {
            ChildEnergyAllocationId   = Id,
            ChildEnergyAllocation     = this,
            FamilyEnergyAccountId     = FamilyEnergyAccountId,
            Type                      = type,
            Pool                      = CreditPool.Granted, // allocation rows are always Subscription bucket
            SourceBucket              = EnergyBucket.Subscription,
            Amount                    = amount,
            ReasonCode                = reasonCode,
            ResultingGrantedBalance   = Remaining,  // post-mutation remaining
            ResultingPurchasedBalance = 0,          // not applicable; see FamilyEnergyAccount
            OccurredAtUtc             = DateTime.UtcNow,
            IdempotencyKey            = idempotencyKey,
        };
}
