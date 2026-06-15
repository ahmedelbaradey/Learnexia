using FluentAssertions;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Xunit;

namespace Modules.Billing.UnitTests;

/// <summary>
/// Unit tests for <see cref="CreditAccount"/> domain entity — the ledger primitive.
///
/// Coverage:
///   LA-01  Grant increases GrantedBalance; transaction type = Grant; pool = Granted.
///   LA-02  Debit draws Granted-first; resulting balances correct.
///   LA-03  Debit when GrantedBalance insufficient draws from Purchased.
///   LA-04  Mixed debit (grant + purchased) when both pools needed.
///   LA-05  Debit throws when TotalBalance insufficient (never-negative invariant).
///   LA-06  Balance after grant + debit is correct (accounting identity).
///   LA-07  ExpireGrant zeroes GrantedBalance.
///   LA-08  Refund increases PurchasedBalance (clamped).
///   LA-09  Adjust positive/negative applies correctly.
///   LA-10  ApplyPurchase adds to PurchasedBalance; does not touch GrantedBalance.
/// </summary>
public sealed class CreditAccountLedgerTests
{
    // ── LA-01: Grant ──────────────────────────────────────────────────────────────

    [Fact]
    public void ApplyGrant_IncreasesGrantedBalance_AndReturnsGrantTransaction()
    {
        var account = CreditAccount.CreateEmpty(childId: 1);

        var tx = account.ApplyGrant(100, DateTime.UtcNow.AddMonths(1), CreditReasonCode.MonthlyGrantFree, "grant:1:202601");

        account.GrantedBalance.Should().Be(100);
        account.PurchasedBalance.Should().Be(0);
        account.TotalBalance.Should().Be(100);
        tx.Type.Should().Be(CreditTransactionType.Grant);
        tx.Pool.Should().Be(CreditPool.Granted);
        tx.Amount.Should().Be(100);
        tx.ResultingGrantedBalance.Should().Be(100);
        tx.ResultingPurchasedBalance.Should().Be(0);
        tx.IdempotencyKey.Should().Be("grant:1:202601");
    }

    // ── LA-02: Debit — Granted-first when sufficient ──────────────────────────────

    [Fact]
    public void Debit_DrawsFromGrantedFirst_WhenGrantedIsSufficient()
    {
        var account = CreditAccount.CreateEmpty(childId: 2);
        account.ApplyGrant(50, DateTime.UtcNow.AddMonths(1), CreditReasonCode.MonthlyGrantFree, "grant:2:202601");
        account.ApplyPurchase(20, "purchase:2:001");

        var tx = account.Debit(fromGranted: 10, fromPurchased: 0, CreditReasonCode.AiHint, "spend:2:001");

        account.GrantedBalance.Should().Be(40);
        account.PurchasedBalance.Should().Be(20);
        account.TotalBalance.Should().Be(60);
        tx.Type.Should().Be(CreditTransactionType.Spend);
        tx.Pool.Should().Be(CreditPool.Granted);
        tx.Amount.Should().Be(10);
    }

    // ── LA-03: Debit — from Purchased when Granted is zero ───────────────────────

    [Fact]
    public void Debit_DrawsFromPurchased_WhenGrantedIsZero()
    {
        var account = CreditAccount.CreateEmpty(childId: 3);
        account.ApplyPurchase(50, "purchase:3:001");

        var tx = account.Debit(fromGranted: 0, fromPurchased: 5, CreditReasonCode.AiDeepExplanation, "spend:3:001");

        account.GrantedBalance.Should().Be(0);
        account.PurchasedBalance.Should().Be(45);
        tx.Pool.Should().Be(CreditPool.Purchased);
        tx.Amount.Should().Be(5);
    }

    // ── LA-04: Mixed debit ────────────────────────────────────────────────────────

    [Fact]
    public void Debit_MixedPool_WhenBothPoolsNeeded()
    {
        var account = CreditAccount.CreateEmpty(childId: 4);
        account.ApplyGrant(3, DateTime.UtcNow.AddMonths(1), CreditReasonCode.MonthlyGrantFree, "grant:4:202601");
        account.ApplyPurchase(10, "purchase:4:001");

        var tx = account.Debit(fromGranted: 3, fromPurchased: 2, CreditReasonCode.AiDeepExplanation, "spend:4:001");

        account.GrantedBalance.Should().Be(0);
        account.PurchasedBalance.Should().Be(8);
        tx.Pool.Should().Be(CreditPool.Mixed);
        tx.Amount.Should().Be(5);
    }

    // ── LA-05: Never-negative invariant ──────────────────────────────────────────

    [Fact]
    public void Debit_Throws_WhenRequestedAmountExceedsBalance()
    {
        var account = CreditAccount.CreateEmpty(childId: 5);
        account.ApplyGrant(5, DateTime.UtcNow.AddMonths(1), CreditReasonCode.MonthlyGrantFree, "grant:5:202601");

        // Requesting more from Granted than available.
        var act = () => account.Debit(fromGranted: 10, fromPurchased: 0, CreditReasonCode.AiHint, "spend:5:001");

        act.Should().Throw<InvalidOperationException>();
        account.GrantedBalance.Should().Be(5, "balance must not mutate on a failed debit");
    }

    // ── LA-06: Accounting identity ────────────────────────────────────────────────

    [Fact]
    public void BalanceAfterGrantAndDebit_IsCorrect()
    {
        var account = CreditAccount.CreateEmpty(childId: 6);
        account.ApplyGrant(100, DateTime.UtcNow.AddMonths(1), CreditReasonCode.MonthlyGrantFree, "grant:6:202601");
        account.Debit(fromGranted: 1, fromPurchased: 0, CreditReasonCode.AiHint, "spend:6:001");
        account.Debit(fromGranted: 2, fromPurchased: 0, CreditReasonCode.AiWhyWrong, "spend:6:002");
        account.Debit(fromGranted: 3, fromPurchased: 0, CreditReasonCode.AiDeepExplanation, "spend:6:003");

        account.GrantedBalance.Should().Be(94);
        account.TotalBalance.Should().Be(94);
    }

    // ── LA-07: ExpireGrant ────────────────────────────────────────────────────────

    [Fact]
    public void ExpireGrant_ZeroesGrantedBalance()
    {
        var account = CreditAccount.CreateEmpty(childId: 7);
        account.ApplyGrant(80, DateTime.UtcNow.AddMonths(1), CreditReasonCode.MonthlyGrantFree, "grant:7:202601");
        account.Debit(fromGranted: 10, fromPurchased: 0, CreditReasonCode.AiHint, "spend:7:001");

        var tx = account.ExpireGrant("expire:7:202601");

        account.GrantedBalance.Should().Be(0);
        account.GrantExpiresAtUtc.Should().BeNull();
        tx.Type.Should().Be(CreditTransactionType.Expiry);
        tx.Amount.Should().Be(70, "expired amount = remaining granted balance before expiry");
    }

    // ── LA-08: Refund (clamped) ───────────────────────────────────────────────────

    [Fact]
    public void Refund_IncreasesOrClampsPurchasedBalance()
    {
        var account = CreditAccount.CreateEmpty(childId: 8);
        account.ApplyPurchase(50, "purchase:8:001");
        account.Debit(fromGranted: 0, fromPurchased: 30, CreditReasonCode.AiPracticeGeneration, "spend:8:001");

        // Purchased balance is now 20. Refund 15 (within available).
        var tx = account.Refund(15, "refund:8:001");

        account.PurchasedBalance.Should().Be(5);
        tx.Type.Should().Be(CreditTransactionType.Refund);
        tx.Amount.Should().Be(15);

        // Refund more than available (50) → clamped to remaining (5).
        var tx2 = account.Refund(50, "refund:8:002");
        account.PurchasedBalance.Should().Be(0, "never negative");
        tx2.Amount.Should().Be(5, "clamped to available balance");
    }

    // ── LA-09: Adjust ─────────────────────────────────────────────────────────────

    [Fact]
    public void Adjust_PositiveDelta_CreditsGrantedPool()
    {
        var account = CreditAccount.CreateEmpty(childId: 9);
        var tx = account.Adjust(+20, "adj:9:001");

        account.GrantedBalance.Should().Be(20);
        tx.Type.Should().Be(CreditTransactionType.Adjustment);
        tx.ReasonCode.Should().Be(CreditReasonCode.AdminCredit);
        tx.Amount.Should().Be(20);
    }

    [Fact]
    public void Adjust_NegativeDelta_DebitsGrantedPoolClampedToZero()
    {
        var account = CreditAccount.CreateEmpty(childId: 10);
        account.ApplyGrant(10, DateTime.UtcNow.AddMonths(1), CreditReasonCode.MonthlyGrantFree, "grant:10:202601");

        // Deduct more than available → clamps to current balance.
        var tx = account.Adjust(-50, "adj:10:001");

        account.GrantedBalance.Should().Be(0, "never negative");
        tx.ReasonCode.Should().Be(CreditReasonCode.AdminDebit);
        tx.Amount.Should().Be(10, "clamped to available balance");
    }

    // ── LA-10: ApplyPurchase ──────────────────────────────────────────────────────

    [Fact]
    public void ApplyPurchase_AddsToPurchasedBalance_DoesNotTouchGranted()
    {
        var account = CreditAccount.CreateEmpty(childId: 11);
        account.ApplyGrant(30, DateTime.UtcNow.AddMonths(1), CreditReasonCode.MonthlyGrantFree, "grant:11:202601");

        var tx = account.ApplyPurchase(500, "purchase:11:001");

        account.GrantedBalance.Should().Be(30);
        account.PurchasedBalance.Should().Be(500);
        account.TotalBalance.Should().Be(530);
        tx.Type.Should().Be(CreditTransactionType.Purchase);
        tx.Pool.Should().Be(CreditPool.Purchased);
    }
}
