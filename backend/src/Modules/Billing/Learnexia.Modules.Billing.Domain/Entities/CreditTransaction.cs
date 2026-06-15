using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Billing.Domain.Entities;

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
    // ── FK within billing schema ─────────────────────────────────────────────────

    public int CreditAccountId { get; set; }
    public CreditAccount CreditAccount { get; set; } = null!;

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
}
