namespace Learnexia.Modules.Billing.Application.Abstractions;

/// <summary>
/// Service seam (Option C) for the family allocation redistribution operation (P10-16-BE-2).
///
/// <para><strong>Option C contract:</strong> Application-layer handlers inject THIS interface.
/// All EF, transaction, and business-rule logic lives in the Infrastructure implementation.
/// Handlers stay EF-free.</para>
///
/// <para><strong>Security:</strong> <c>parentUserId</c> MUST come from the JWT (never the request
/// body). The service enforces the same-family guard — cross-family transfers are rejected
/// by construction (not just policy).</para>
/// </summary>
public interface IFamilyAllocationService
{
    /// <summary>
    /// Returns the per-child allocation snapshot for ALL active-seat children of
    /// <paramref name="parentUserId"/>, including those with zero current allocation
    /// (mid-cycle-added children who have an active seat but no <c>ChildEnergyAllocation</c>
    /// row for the current cycle — <c>IsMidCycleNoGrant = true</c>).
    ///
    /// <para>Family-scope IDOR: the Infrastructure impl only returns children that hold
    /// an active <c>SeatReservation</c> for the parent's subscription — no Identity cross-module
    /// FK.</para>
    /// </summary>
    /// <param name="parentUserId">Owning parent (wallet owner) — from JWT.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<FamilyAllocationDto?> GetFamilyAllocationAsync(int parentUserId, CancellationToken ct = default);

    /// <summary>
    /// Transfers <paramref name="amount"/> of UNSPENT allocated energy from
    /// <paramref name="fromChildId"/> to <paramref name="toChildId"/>, both of whom must be
    /// active-seat children of the same <c>FamilyEnergyAccount</c>.
    ///
    /// <para><strong>Invariants enforced:</strong>
    /// <list type="bullet">
    ///   <item>Same-family guard: both children belong to this <c>FamilyEnergyAccount</c> — cross-family impossible by construction.</item>
    ///   <item>Only UNSPENT allocated energy is movable: <c>amount ≤ source.Remaining</c>.</item>
    ///   <item>Neither source nor destination may be a NoSeatLocked child.</item>
    ///   <item>Source ≠ destination.</item>
    ///   <item>Bucket A only: never touches the shared purchased reserve (bucket B).</item>
    ///   <item>If the destination child has no <c>ChildEnergyAllocation</c> row for the current cycle (mid-cycle-added child), an INSERT is performed before the transfer.</item>
    ///   <item>Atomic: decrement source + increment destination + paired <c>TransferOut</c>/<c>TransferIn</c> ledger rows (same <c>CorrelationId</c>) commit or rollback together.</item>
    ///   <item>Idempotent on <paramref name="idempotencyKey"/>: a re-submit returns the prior result without double-moving.</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="parentUserId">Owning parent — from JWT; never supplied by client.</param>
    /// <param name="fromChildId">Source child id (loose int — no cross-module FK).</param>
    /// <param name="toChildId">Destination child id (loose int — no cross-module FK).</param>
    /// <param name="amount">Energy units to transfer. Must be &gt; 0 and ≤ source Remaining.</param>
    /// <param name="idempotencyKey">Client-supplied idempotency key (OQ-I). Re-submission returns the prior result.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<TransferAllocationResult> TransferAllocationAsync(
        int parentUserId,
        int fromChildId,
        int toChildId,
        long amount,
        string idempotencyKey,
        CancellationToken ct = default);
}

// ── DTOs ────────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Per-child allocation snapshot returned by
/// <see cref="IFamilyAllocationService.GetFamilyAllocationAsync"/> (P10-16-BE-2).
/// </summary>
public sealed class FamilyAllocationDto
{
    /// <summary>Per active-seat child allocation snapshot (all active-seat children, including zero-allocation ones).</summary>
    public IReadOnlyList<ChildAllocationItemDto> Children { get; init; } = Array.Empty<ChildAllocationItemDto>();
}

/// <summary>
/// Per-child row in <see cref="FamilyAllocationDto.Children"/>.
/// </summary>
public sealed class ChildAllocationItemDto
{
    /// <summary>Child id (loose int — no cross-module FK).</summary>
    public int ChildId { get; init; }

    /// <summary>Credits allocated to this child for the current cycle (0 for mid-cycle-added children with no grant yet).</summary>
    public int Allocated { get; init; }

    /// <summary>Credits spent by this child in the current cycle.</summary>
    public int Spent { get; init; }

    /// <summary>Unspent remaining (Allocated − Spent, always ≥ 0).</summary>
    public int Remaining { get; init; }

    /// <summary>
    /// True when this child has an active seat but NO <c>ChildEnergyAllocation</c> row for the
    /// current cycle — i.e. they were added or reactivated mid-cycle after the last renewal grant ran.
    /// The parent must use <c>TransferAllocationCommand</c> to give this child energy.
    /// </summary>
    public bool IsMidCycleNoGrant { get; init; }
}

/// <summary>
/// Result of <see cref="IFamilyAllocationService.TransferAllocationAsync"/>.
/// </summary>
public sealed class TransferResultDto
{
    /// <summary>Updated allocation snapshot for the source child after the transfer.</summary>
    public ChildAllocationItemDto FromChild { get; init; } = null!;

    /// <summary>Updated allocation snapshot for the destination child after the transfer.</summary>
    public ChildAllocationItemDto ToChild { get; init; } = null!;
}

/// <summary>
/// Internal service result wrapping the outcome of
/// <see cref="IFamilyAllocationService.TransferAllocationAsync"/>.
/// </summary>
public sealed class TransferAllocationResult
{
    public bool Succeeded { get; init; }
    public TransferResultDto? Data { get; init; }
    public TransferAllocationFailureReason? FailureReason { get; init; }

    public static TransferAllocationResult Ok(TransferResultDto data)
        => new() { Succeeded = true, Data = data };

    public static TransferAllocationResult Fail(TransferAllocationFailureReason reason)
        => new() { Succeeded = false, FailureReason = reason };

    public static TransferAllocationResult Duplicate(TransferResultDto data)
        => new() { Succeeded = true, Data = data };
}

/// <summary>Typed failure reasons for <see cref="IFamilyAllocationService.TransferAllocationAsync"/>.</summary>
public enum TransferAllocationFailureReason
{
    /// <summary>Either <c>fromChildId</c> or <c>toChildId</c> does not belong to the parent's <c>FamilyEnergyAccount</c>.</summary>
    ChildNotInFamily = 1,

    /// <summary>The transfer amount exceeds the source child's unspent remaining allowance.</summary>
    AmountExceedsRemaining = 2,

    /// <summary>The source and destination children are the same.</summary>
    SameSourceAndDestination = 3,

    /// <summary>The destination child does not hold an active seat (NoSeatLocked or no reservation).</summary>
    DestinationHasNoActiveSeat = 4,

    /// <summary>The source child does not hold an active seat (NoSeatLocked or no reservation).</summary>
    SourceHasNoActiveSeat = 5,

    /// <summary>The parent has no <c>FamilyEnergyAccount</c> (wallet not yet initialized).</summary>
    WalletNotFound = 6,

    /// <summary>
    /// The supplied idempotency key was already used for a transfer with a DIFFERENT payload
    /// (from/to child or amount). Reusing a key with a changed payload is a client error (409).
    /// </summary>
    IdempotencyKeyConflict = 7,
}
