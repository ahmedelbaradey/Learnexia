using FluentAssertions;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Xunit;

namespace Modules.Billing.UnitTests;

/// <summary>
/// Unit tests for the W2 ledger-split fix: <see cref="CreditTransaction.FromGranted"/> /
/// <see cref="CreditTransaction.FromPurchased"/> columns that enable exact Mixed-pool reconcile.
///
/// Coverage:
///   LS-01  Pure-Granted debit: FromGranted = amount, FromPurchased = 0.
///   LS-02  Pure-Purchased debit: FromGranted = 0, FromPurchased = amount.
///   LS-03  Mixed debit: FromGranted + FromPurchased = total amount.
///   LS-04  Mixed split values are individually correct.
///   LS-05  FromGranted + FromPurchased from multiple transactions reconstruct the balance correctly.
/// </summary>
public sealed class LedgerSplitTests
{
    // ── LS-01: Pure-Granted debit ─────────────────────────────────────────────────

    [Fact]
    public void Debit_PureGranted_SetsFromGrantedAndZeroFromPurchased()
    {
        var account = CreditAccount.CreateEmpty(childId: 1);
        account.ApplyGrant(50, DateTime.UtcNow.AddDays(30), CreditReasonCode.MonthlyGrantFree, "grant:1:202606");

        var tx = account.Debit(fromGranted: 10, fromPurchased: 0, CreditReasonCode.AiHint, "spend:1:001");

        tx.FromGranted.Should().Be(10);
        tx.FromPurchased.Should().Be(0);
        tx.Pool.Should().Be(CreditPool.Granted);
        tx.Amount.Should().Be(10);
    }

    // ── LS-02: Pure-Purchased debit ──────────────────────────────────────────────

    [Fact]
    public void Debit_PurePurchased_SetsZeroFromGrantedAndFromPurchased()
    {
        var account = CreditAccount.CreateEmpty(childId: 2);
        account.ApplyPurchase(50, "purchase:2:001");

        var tx = account.Debit(fromGranted: 0, fromPurchased: 5, CreditReasonCode.AiDeepExplanation, "spend:2:001");

        tx.FromGranted.Should().Be(0);
        tx.FromPurchased.Should().Be(5);
        tx.Pool.Should().Be(CreditPool.Purchased);
        tx.Amount.Should().Be(5);
    }

    // ── LS-03: Mixed debit total ──────────────────────────────────────────────────

    [Fact]
    public void Debit_Mixed_FromGrantedPlusFromPurchasedEqualsAmount()
    {
        var account = CreditAccount.CreateEmpty(childId: 3);
        account.ApplyGrant(3, DateTime.UtcNow.AddDays(30), CreditReasonCode.MonthlyGrantFree, "grant:3:202606");
        account.ApplyPurchase(10, "purchase:3:001");

        var tx = account.Debit(fromGranted: 3, fromPurchased: 2, CreditReasonCode.AiDeepExplanation, "spend:3:001");

        (tx.FromGranted + tx.FromPurchased).Should().Be(tx.Amount, "split must sum to total amount");
    }

    // ── LS-04: Mixed split values are individually correct ───────────────────────

    [Fact]
    public void Debit_Mixed_RecordsCorrectSplitValues()
    {
        var account = CreditAccount.CreateEmpty(childId: 4);
        account.ApplyGrant(3, DateTime.UtcNow.AddDays(30), CreditReasonCode.MonthlyGrantFree, "grant:4:202606");
        account.ApplyPurchase(10, "purchase:4:001");

        var tx = account.Debit(fromGranted: 3, fromPurchased: 2, CreditReasonCode.AiDeepExplanation, "spend:4:001");

        tx.FromGranted.Should().Be(3);
        tx.FromPurchased.Should().Be(2);
        tx.Pool.Should().Be(CreditPool.Mixed);
    }

    // ── LS-05: Balance reconstruction from split columns ─────────────────────────

    [Fact]
    public void LedgerReconstructsBalance_FromFromGrantedPlusFromPurchasedColumns()
    {
        // Simulate what ReconcileAccountQueryHandler does with exact split columns.
        var account = CreditAccount.CreateEmpty(childId: 5);
        account.ApplyGrant(100, DateTime.UtcNow.AddDays(30), CreditReasonCode.MonthlyGrantFree, "grant:5:202606");
        account.ApplyPurchase(50, "purchase:5:001");

        // Three debits with varying pool draws.
        var tx1 = account.Debit(fromGranted: 5, fromPurchased: 0, CreditReasonCode.AiHint, "spend:5:001");
        var tx2 = account.Debit(fromGranted: 0, fromPurchased: 3, CreditReasonCode.AiWhyWrong, "spend:5:002");
        var tx3 = account.Debit(fromGranted: 10, fromPurchased: 2, CreditReasonCode.AiDeepExplanation, "spend:5:003");

        var transactions = new[] { tx1, tx2, tx3 };

        // Reconstruct using FromGranted/FromPurchased (the exact-split approach).
        var reconstructedGranted = 100 - transactions.Sum(t => t.FromGranted);
        var reconstructedPurchased = 50 - transactions.Sum(t => t.FromPurchased);

        reconstructedGranted.Should().Be(account.GrantedBalance, "reconstructed granted must match stored balance");
        reconstructedPurchased.Should().Be(account.PurchasedBalance, "reconstructed purchased must match stored balance");
    }
}
