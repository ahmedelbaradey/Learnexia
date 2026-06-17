using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Billing.Domain.Entities;

// P10-13 note: CreditTransaction is now the UNIFIED ledger for both the legacy per-child
// CreditAccount model AND the new FamilyEnergyAccount / ChildEnergyAllocation wallet model.
// New columns (SourceBucket, ChildEnergyAllocationId, FamilyEnergyAccountId, CorrelationId)
// are nullable on CreditAccountId rows and populated on wallet rows. The existing
// UX_CreditTransactions_IdempotencyKey unique constraint is preserved.

/// <summary>
/// Append-only ledger row for every credit balance change. Mirrors <c>XpAward</c> in the
/// Gamification module — no mutation methods; idempotency enforced by a DB unique constraint
/// on <see cref="IdempotencyKey"/> (<c>UX_CreditTransactions_IdempotencyKey</c>).
///
/// <para>Module isolation: <see cref="CreditAccountId"/> is a plain int FK within the
/// <c>billing</c> schema only. No cross-module FK.</para>
///
/// <para>Amount is always positive. <see cref="Type"/> and <see cref="Pool"/> convey
/// the semantic direction.</para>
/// </summary>
public class CreditTransaction : FullAuditedEntity
{
    // ── Legacy column — historical reference only (CreditAccount table DROPPED in DropLegacyCreditAccounts) ─
    // CreditAccountId is retained as a plain nullable int on the CreditTransactions table so that
    // pre-cutover ledger rows (written before the family-wallet migration) are not orphaned.
    // No EF navigation or FK constraint exists; the CreditAccount entity and its table are gone.

    /// <summary>
    /// Legacy FK column — preserved as a plain nullable int on existing rows.
    /// The <c>billing.CreditAccounts</c> table was dropped in migration
    /// <c>DropLegacyCreditAccounts</c>; this column is a historical marker only.
    /// No navigation property; no FK constraint.
    /// </summary>
    public int? CreditAccountId { get; set; }

    // ── FK within billing schema — family wallet model (nullable for legacy rows) ──

    /// <summary>FK to <see cref="FamilyEnergyAccount"/>. Populated on wallet-level rows (grant deposit, pack purchase, purchased-fallback).</summary>
    public int? FamilyEnergyAccountId { get; set; }
    public FamilyEnergyAccount? FamilyEnergyAccount { get; set; }

    /// <summary>FK to <see cref="ChildEnergyAllocation"/>. Populated on per-child allocation rows.</summary>
    public int? ChildEnergyAllocationId { get; set; }
    public ChildEnergyAllocation? ChildEnergyAllocation { get; set; }

    // ── Ledger fields ────────────────────────────────────────────────────────────

    public CreditTransactionType Type { get; set; }
    public CreditPool Pool { get; set; }

    /// <summary>Amount of the transaction (always positive).</summary>
    public int Amount { get; set; }

    public CreditReasonCode ReasonCode { get; set; }

    /// <summary>Optional human-readable elaboration on <see cref="ReasonCode"/>.</summary>
    public string? Reason { get; set; }

    // ── Resulting balances snapshot (for quick reconciliation) ───────────────────

    public int ResultingGrantedBalance { get; set; }
    public int ResultingPurchasedBalance { get; set; }

    // ── Ledger split (W2 carried fix — exact reconcile for Mixed-pool debits) ──

    /// <summary>
    /// For <see cref="CreditTransactionType.Spend"/> rows: the portion of <see cref="Amount"/>
    /// drawn from the granted pool. Zero for non-Spend rows.
    /// </summary>
    public int FromGranted { get; set; }

    /// <summary>
    /// For <see cref="CreditTransactionType.Spend"/> rows: the portion of <see cref="Amount"/>
    /// drawn from the purchased pool. Zero for non-Spend rows.
    /// </summary>
    public int FromPurchased { get; set; }

    // ── Timestamps ───────────────────────────────────────────────────────────────

    /// <summary>UTC time the event logically occurred (not the DB insert wall-clock time).</summary>
    public DateTime OccurredAtUtc { get; set; }

    // ── Idempotency ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Unique key supplied by the caller. A DB unique constraint
    /// (<c>UX_CreditTransactions_IdempotencyKey</c>) prevents double-writes.
    /// For AI debits: the per-request action id. For grants: <c>grant:{childId}:{yyyyMM}</c>.
    /// </summary>
    public string IdempotencyKey { get; set; } = null!;

    // ── Correlation ──────────────────────────────────────────────────────────────

    /// <summary>Optional correlation to the AI action id that triggered a spend.</summary>
    public string? RelatedActionId { get; set; }

    /// <summary>Optional correlation to a payment id (pack purchase / refund).</summary>
    public string? RelatedPaymentId { get; set; }

    // ── P10-13 wallet model extensions ───────────────────────────────────────────

    /// <summary>
    /// Which non-convertible energy bucket this row belongs to.
    /// Null on legacy <c>CreditAccount</c>-model rows; populated on all wallet-model rows.
    /// </summary>
    public EnergyBucket? SourceBucket { get; set; }

    /// <summary>
    /// Correlation id for paired P10-16 sibling-redistribution transfers
    /// (<c>TransferOut</c> + <c>TransferIn</c> share the same <see cref="CorrelationId"/>).
    /// Null on non-transfer rows.
    /// </summary>
    public Guid? CorrelationId { get; set; }
}
