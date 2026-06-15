using FluentAssertions;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Xunit;

namespace Modules.Billing.UnitTests;

/// <summary>
/// Unit tests for the monthly grant idempotency contract (domain-level).
///
/// <para>These tests verify the <see cref="CreditAccount"/> domain mutators that
/// <c>BillingGrantJob</c> calls — specifically that:
/// <list type="bullet">
///   <item>A grant with a given idempotency key increases <see cref="CreditAccount.GrantedBalance"/>
///         and stamps <see cref="CreditAccount.GrantExpiresAtUtc"/>.</item>
///   <item>Expiring a prior grant zeroes <see cref="CreditAccount.GrantedBalance"/> without
///         touching <see cref="CreditAccount.PurchasedBalance"/>.</item>
///   <item>The DB-level idempotency guard is provided by the <c>IdempotencyKey</c> on the
///         returned <see cref="CreditTransaction"/>; the grant job uses a pre-check pattern
///         so the domain mutator itself is not re-entered on a duplicate.</item>
///   <item>Grant expiry correctly reflects the <see cref="CreditAccount.GrantExpiresAtUtc"/> field.</item>
/// </list>
/// </para>
///
/// Coverage:
///   GJ-01  Grant sets GrantedBalance and GrantExpiresAtUtc.
///   GJ-02  ApplyGrant transaction has the expected idempotency key.
///   GJ-03  ExpireGrant zeroes GrantedBalance; PurchasedBalance untouched.
///   GJ-04  Expire key is distinct from grant key (no collision).
///   GJ-05  Re-granting same amount on a non-expired account accumulates (domain layer allows;
///           DB unique constraint prevents in production — domain alone does not block it).
///   GJ-06  Grant expiry timestamp is stamped correctly on the account.
/// </summary>
public sealed class BillingGrantJobIdempotencyTests
{
    private static readonly DateTime FutureExpiry = DateTime.UtcNow.AddDays(31);

    // ── GJ-01: Grant sets GrantedBalance and GrantExpiresAtUtc ───────────────────

    [Fact]
    public void ApplyGrant_SetsGrantedBalanceAndExpiresAtUtc()
    {
        var account = CreditAccount.CreateEmpty(childId: 1);

        var tx = account.ApplyGrant(100, FutureExpiry, CreditReasonCode.MonthlyGrantFree, "grant:1:202606");

        account.GrantedBalance.Should().Be(100);
        account.GrantExpiresAtUtc.Should().Be(FutureExpiry);
        tx.Type.Should().Be(CreditTransactionType.Grant);
        tx.Amount.Should().Be(100);
    }

    // ── GJ-02: ApplyGrant transaction carries the idempotency key ────────────────

    [Fact]
    public void ApplyGrant_TransactionCarriesCorrectIdempotencyKey()
    {
        var account = CreditAccount.CreateEmpty(childId: 2);
        const string expectedKey = "grant:2:202606";

        var tx = account.ApplyGrant(100, FutureExpiry, CreditReasonCode.MonthlyGrantFree, expectedKey);

        tx.IdempotencyKey.Should().Be(expectedKey);
    }

    // ── GJ-03: ExpireGrant zeroes GrantedBalance; PurchasedBalance untouched ─────

    [Fact]
    public void ExpireGrant_ZeroesGrantedBalance_DoesNotTouchPurchased()
    {
        var account = CreditAccount.CreateEmpty(childId: 3);
        account.ApplyGrant(100, FutureExpiry, CreditReasonCode.MonthlyGrantFree, "grant:3:202605");
        account.ApplyPurchase(50, "purchase:3:001");

        var expireTx = account.ExpireGrant("expire:3:202605");

        account.GrantedBalance.Should().Be(0, "granted balance must be zeroed on expiry");
        account.PurchasedBalance.Should().Be(50, "purchased balance must not be touched");
        account.GrantExpiresAtUtc.Should().BeNull();
        expireTx.Type.Should().Be(CreditTransactionType.Expiry);
        expireTx.Amount.Should().Be(100, "expired amount = balance before expiry");
    }

    // ── GJ-04: Expire key is distinct from grant key ─────────────────────────────

    [Fact]
    public void ExpireAndGrantKeys_AreDistinct()
    {
        const int childId = 4;
        const string cycleId = "202606";
        const string priorCycleId = "202605";

        var grantKey = $"grant:{childId}:{cycleId}";
        var expireKey = $"expire:{childId}:{priorCycleId}";

        grantKey.Should().NotBe(expireKey, "grant and expire keys must be distinct to avoid idempotency collision");
    }

    // ── GJ-05: Domain layer allows re-grant; DB constraint prevents it in production ──

    [Fact]
    public void ApplyGrant_AccumulatesBalance_WhenCalledTwice_DomainLayerAlone()
    {
        // The domain does NOT enforce single-grant-per-cycle — that invariant is enforced by the
        // DB unique constraint on IdempotencyKey + the pre-check in BillingGrantJob.
        // This test documents that the domain mutator is NOT idempotent by itself.
        var account = CreditAccount.CreateEmpty(childId: 5);

        account.ApplyGrant(100, FutureExpiry, CreditReasonCode.MonthlyGrantFree, "grant:5:202606");
        account.ApplyGrant(100, FutureExpiry, CreditReasonCode.MonthlyGrantFree, "grant:5:202606-dup");

        // Domain allows accumulation — the idempotency block is at DB level.
        account.GrantedBalance.Should().Be(200);
    }

    // ── GJ-06: GrantExpiresAtUtc stamped correctly ───────────────────────────────

    [Fact]
    public void ApplyGrant_StampsGrantExpiresAtUtcFromArgument()
    {
        var account = CreditAccount.CreateEmpty(childId: 6);
        var cycleEnd = new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc);

        account.ApplyGrant(100, cycleEnd, CreditReasonCode.MonthlyGrantFree, "grant:6:202606");

        account.GrantExpiresAtUtc.Should().Be(cycleEnd);
    }

    // ── GJ-07: Premium grant uses MonthlyGrantPremium reason code ────────────────

    [Fact]
    public void ApplyGrant_WithPremiumReason_SetsCorrectReasonCode()
    {
        var account = CreditAccount.CreateEmpty(childId: 7);

        var tx = account.ApplyGrant(5000, FutureExpiry, CreditReasonCode.MonthlyGrantPremium, "grant:7:202606");

        tx.ReasonCode.Should().Be(CreditReasonCode.MonthlyGrantPremium);
        tx.Amount.Should().Be(5000);
    }
}
