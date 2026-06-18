using Learnexia.Modules.Billing.Application.Features.ChildAccess.Dtos;
using Learnexia.Modules.Billing.Domain.Enums;

namespace Learnexia.Modules.Billing.Application.Abstractions;

/// <summary>
/// Workflow service owning all parent-pause business rules for a child (P10-18-BE-2, Option C).
///
/// <para><strong>Option C contract:</strong> Application-layer handlers inject THIS interface.
/// All EF, transaction, and seat/energy guard logic lives in the Infrastructure implementation.
/// Handlers stay EF-free.</para>
///
/// <para><strong>IDOR guard:</strong> both methods verify the child belongs to the parent's
/// family using the Billing-owned <c>SeatReservation.ParentUserId</c>. Any mismatch = rejection.
/// <c>parentUserId</c> MUST come from the JWT — never from the request body or URL.</para>
///
/// <para><strong>Zero energy/seat side-effects:</strong> neither method writes to energy balances,
/// allocations, seat counts, or <c>SeatState</c>. Allowed writes: <c>ParentPauseState</c> flip +
/// one <c>SeatLedgerEntry{ParentPause | ParentUnpause}</c> per non-idempotent call.</para>
///
/// <para><strong>Idempotency:</strong> pausing an already-Paused child is a no-op (success, no
/// ledger entry). Unpausing an already-Active child is a no-op (success, no ledger entry).</para>
///
/// <para><strong>Transaction:</strong> the state flip + ledger append run in a single explicit
/// transaction inside the Infrastructure implementation. No UoW (ADR 0001).</para>
/// </summary>
public interface IPauseChildService
{
    /// <summary>
    /// Pauses a child's AI access immediately.
    ///
    /// <para>Steps: (1) family-scope ownership guard; (2) idempotency guard — if already
    /// <c>Paused</c>, return success no-op without writing; (3) inside an explicit transaction:
    /// set <c>ParentPauseState = Paused</c>, set <c>ParentPauseStateChangedAt = UtcNow</c>,
    /// append a <c>SeatLedgerEntry{ParentPause}</c>; commit.</para>
    ///
    /// <para>ZERO writes to energy balances, allocations, seat counts, or <c>SeatState</c>.</para>
    /// </summary>
    /// <param name="parentUserId">The authenticated parent user id (from JWT).</param>
    /// <param name="childId">The child to pause (plain int — no cross-module FK).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<PauseServiceResult> PauseChildAsync(int parentUserId, int childId, CancellationToken ct = default);

    /// <summary>
    /// Unpauses a child's AI access immediately.
    ///
    /// <para>Mirror of <see cref="PauseChildAsync"/> — same ownership guard and idempotency
    /// logic, but moves <c>ParentPauseState</c> to <c>Active</c> and appends
    /// <c>SeatLedgerEntry{ParentUnpause}</c>.</para>
    ///
    /// <para>ZERO writes to energy balances, allocations, seat counts, or <c>SeatState</c>.</para>
    /// </summary>
    /// <param name="parentUserId">The authenticated parent user id (from JWT).</param>
    /// <param name="childId">The child to unpause (plain int — no cross-module FK).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<PauseServiceResult> UnpauseChildAsync(int parentUserId, int childId, CancellationToken ct = default);

    /// <summary>
    /// Returns the combined access status for a child: SeatState + ParentPauseState + derived
    /// IsAccessAllowed flag.
    ///
    /// <para>Family-scope ownership guard: the child must belong to <paramref name="parentUserId"/>'s
    /// family. Returns <see cref="PauseServiceResult.NotOwned"/> on mismatch.</para>
    /// </summary>
    Task<ChildAccessStatusResult> GetChildAccessStatusAsync(int parentUserId, int childId, CancellationToken ct = default);
}

// ── Result types ──────────────────────────────────────────────────────────────────────────────

/// <summary>Result returned by <see cref="IPauseChildService.PauseChildAsync"/> / <see cref="IPauseChildService.UnpauseChildAsync"/>.</summary>
public sealed record PauseServiceResult
{
    public bool Succeeded { get; init; }
    public PauseFailureReason? FailureReason { get; init; }
    public PauseResultDto? Data { get; init; }

    public static PauseServiceResult Ok(PauseResultDto data)
        => new() { Succeeded = true, Data = data };

    public static PauseServiceResult Fail(PauseFailureReason reason)
        => new() { Succeeded = false, FailureReason = reason };

    /// <summary>Convenience check: the operation was a success and a no-op (already in target state).</summary>
    public bool NotOwned => !Succeeded && FailureReason == PauseFailureReason.ChildNotOwnedByParent;
}

/// <summary>Typed failure reasons for <see cref="IPauseChildService"/> operations.</summary>
public enum PauseFailureReason
{
    /// <summary>The child does not belong to the parent's family (IDOR rejection).</summary>
    ChildNotOwnedByParent = 1,
}

/// <summary>Result returned by <see cref="IPauseChildService.GetChildAccessStatusAsync"/>.</summary>
public sealed record ChildAccessStatusResult
{
    public bool Succeeded { get; init; }
    public PauseFailureReason? FailureReason { get; init; }
    public ChildAccessStatusDto? Data { get; init; }

    public static ChildAccessStatusResult Ok(ChildAccessStatusDto data)
        => new() { Succeeded = true, Data = data };

    public static ChildAccessStatusResult Fail(PauseFailureReason reason)
        => new() { Succeeded = false, FailureReason = reason };
}
