namespace Learnexia.Modules.Billing.Application.Abstractions;

/// <summary>
/// Workflow service owning all seat business rules (P10-14-BE-3, Option C).
///
/// <para><strong>Option C contract:</strong> Application-layer handlers inject THIS interface.
/// All EF, transaction, and seat-state-machine logic lives in the Infrastructure implementation.
/// Handlers stay EF-free.</para>
///
/// <para><strong>Seat count formula:</strong>
/// <c>ActivePaidSeats = IncludedSeats(plan tier) + PurchasedExtraSeats</c>
/// while the subscription is billing-active (including grace), capped at <c>seats.max</c>
/// from <c>IGlobalSettingsProvider</c>. Never hard-coded.</para>
///
/// <para><strong>IDOR guard:</strong> <c>parentUserId</c> MUST come from the JWT.
/// The service never accepts it from the request body.</para>
/// </summary>
public interface ISeatService
{
    /// <summary>
    /// Returns the number of active paid seats for <paramref name="parentUserId"/>.
    ///
    /// <para>= <c>IncludedSeats(plan tier) + PurchasedExtraSeats</c> while billing-active
    /// (including grace), capped at <c>seats.max</c>. Returns 0 if no active subscription exists.</para>
    /// </summary>
    Task<int> GetActiveSeatCountAsync(int parentUserId, CancellationToken ct = default);

    /// <summary>
    /// Atomically checks whether a free seat is available and, if so, inserts a
    /// <c>SeatReservation{Reserved}</c>.
    ///
    /// <para><strong>Idempotent on <c>(parentUserId, childId, idempotencyKey)</c>:</strong>
    /// a retry with the same key returns the prior result without double-reserving.</para>
    ///
    /// <para><strong>Explicit transaction inside the service</strong> (free-seat check + insert
    /// are atomic). No UoW (ADR 0001).</para>
    /// </summary>
    Task<SeatReservationServiceResult> ReserveSeatAsync(
        int parentUserId,
        int childId,
        string idempotencyKey,
        CancellationToken ct = default);

    /// <summary>
    /// Transitions a <c>Reserved</c> or <c>Active</c> <c>SeatReservation</c> for
    /// <paramref name="childId"/> to <c>Released</c> (compensation path when add-child fails after a
    /// reservation). Idempotent: no-op if no reservation exists or already released.
    /// (Voluntary seat cancels do NOT release here — they use the cycle-end scheduled-removal marker.)
    ///
    /// <para><strong>P10-15-BE-10 (concurrent-reservation fix):</strong> when <paramref name="childId"/>
    /// is the <c>0</c> placeholder, pass <paramref name="reservationKey"/> to resolve the exact row by
    /// idempotency key rather than "most-recent childId=0" (which is ambiguous under concurrency).</para>
    /// </summary>
    Task ReleaseSeatAsync(int parentUserId, int childId, CancellationToken ct = default,
        string? reservationKey = null);

    /// <summary>
    /// Transitions an existing <c>SeatReservation</c> for <paramref name="childId"/> from
    /// <c>Reserved</c> → <c>Active</c> (called after child creation + link succeeds).
    /// Idempotent: no-op if already <c>Active</c>.
    ///
    /// <para><strong>P10-15-BE-10 (concurrent-reservation fix):</strong> when the reservation was created
    /// with <paramref name="childId"/>=0 as a placeholder, pass <paramref name="reservationKey"/> to resolve
    /// the exact row by idempotency key and stamp the real child id — preventing one concurrent call from
    /// activating another's placeholder.</para>
    /// </summary>
    Task ActivateSeatAsync(int parentUserId, int childId, CancellationToken ct = default,
        string? reservationKey = null);

    /// <summary>
    /// Returns the full seat-status snapshot for <paramref name="parentUserId"/>:
    /// total/occupied/free/available + per-child seat status.
    /// </summary>
    Task<SeatStatusSnapshot> GetSeatStatusAsync(int parentUserId, CancellationToken ct = default);

    // NOTE (P10-14): the purchased-seat increment lives INLINE in
    // WebhookEventService's Seat branch (not a separate service method) — it must run inside the
    // webhook's single transaction (the BLOCKER-3 atomicity fix), which a method opening its own
    // transaction could not join. The seats.max ceiling guard + per-payment idempotency live there.

    /// <summary>
    /// Schedules <paramref name="seatQuantity"/> purchased extra seats for removal at the NEXT
    /// renewal boundary (P10-14-BE-8 / OQ-5, lead-approved 2026-06-17) — a CYCLE-END cancel, NOT a
    /// grace cancel.
    ///
    /// <para>Records the intent on the subscription (<c>PendingExtraSeatRemovals</c> +
    /// <c>ExtraSeatCancelEffectiveAt = CurrentCycleEnd</c>) and does NONE of the following: it does
    /// NOT reduce <c>PurchasedExtraSeats</c> mid-cycle, does NOT touch <c>GraceEndsAt</c> (grace is
    /// payment-failure only), and does NOT reclaim/forfeit/refund energy or delete a child. The seat
    /// stays Active and the child keeps its remaining allowance for the rest of the current cycle.
    /// P10-15 applies the removal + NoSeat/Locked enforcement at renewal.</para>
    /// </summary>
    Task<SeatCancelResult> ScheduleExtraSeatCancellationAsync(
        int parentUserId,
        int seatQuantity,
        CancellationToken ct = default);

    /// <summary>
    /// Applies the parent's explicit selection of which children hold active seats (P10-15-BE-6).
    ///
    /// <para><strong>Guard:</strong> <c>activeChildIds.Count</c> must not exceed <c>paidActiveSeats</c>.
    /// Seats not in the list transition to <c>NoSeatLocked</c> (enforcement logic inline); seats in the
    /// list that are currently <c>NoSeatLocked</c> MUST first be reactivated via a paid checkout (this
    /// method returns <c>ChoiceFailureReason.LockedChildRequiresReactivation</c> for those).</para>
    ///
    /// <para><strong>IDOR:</strong> all child ids must belong to <paramref name="parentUserId"/>'s family.
    /// The service validates this; any foreign child id → <c>ChoiceFailureReason.ChildNotInFamily</c>.</para>
    ///
    /// <para><strong>Idempotent:</strong> re-submitting the same selection is a no-op.</para>
    ///
    /// <para><strong>SeatLedgerEntry{ChoiceApplied}</strong> is appended for each child whose state changes.</para>
    /// </summary>
    Task<SeatChoiceResult> ChooseActiveChildrenAsync(
        int parentUserId,
        IReadOnlyList<int> activeChildIds,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a <c>Payment{Kind=SeatReactivation, Status=Initiated}</c> row, calls
    /// <c>IPaymentProvider.CreateCheckoutSessionAsync</c>, persists the provider ref, and
    /// returns the redirect URL + internal payment id.
    ///
    /// <para><strong>Guard:</strong> the child's seat must be <c>NoSeatLocked</c>. Prorated pricing
    /// (server-resolved). Mint ZERO energy on reactivation (only money, no grant).</para>
    ///
    /// <para><strong>IDOR:</strong> the child must belong to <paramref name="parentUserId"/>'s family.</para>
    /// </summary>
    Task<SeatCheckoutResult> StartSeatReactivationCheckoutAsync(
        int parentUserId,
        int childId,
        string idempotencyKey,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a <c>Payment{Kind=Seat, Status=Initiated}</c> row, calls
    /// <c>IPaymentProvider.CreateCheckoutSessionAsync</c>, persists the provider ref, and
    /// returns the redirect URL + internal payment id.
    ///
    /// <para><strong>Security:</strong> the extra-seat monthly price is resolved SERVER-SIDE
    /// from <c>IGlobalSettingsProvider</c> (key <c>seats.extra_price_egp</c>).
    /// The client never supplies the price. <c>parentUserId</c> MUST come from the JWT.</para>
    ///
    /// <para><strong>Guard:</strong> the total after purchase must not exceed <c>seats.max</c>
    /// AND the parent must not be on the Free plan (<c>PlanCode.Free</c>).</para>
    ///
    /// <para><strong>Option C:</strong> all EF, transaction, and provider calls live here.
    /// The Application handler stays EF-free.</para>
    /// </summary>
    Task<SeatCheckoutResult> StartSeatCheckoutAsync(
        int parentUserId,
        int seatQuantity,
        string idempotencyKey,
        CancellationToken ct = default);
}

// ── Result types ─────────────────────────────────────────────────────────────────────────────────

/// <summary>Result of <see cref="ISeatService.StartSeatCheckoutAsync"/>.</summary>
public sealed record SeatCheckoutResult
{
    public bool    Succeeded       { get; init; }
    public int     PaymentId       { get; init; }
    public string? RedirectUrl     { get; init; }
    public string? ProviderPaymentRef { get; init; }

    /// <summary>
    /// The server-computed charge in EGP for this checkout — the <em>prorated</em> amount for a
    /// mid-cycle add (<c>SeatPrice × seatQuantity × remaining-cycle-ratio</c>), surfaced so the
    /// parent UI can show the exact charge before redirecting to the provider. Never client-supplied.
    /// </summary>
    public decimal Amount          { get; init; }

    public SeatCheckoutFailureReason? FailureReason { get; init; }

    public static SeatCheckoutResult Ok(int paymentId, string redirectUrl, string providerRef, decimal amount)
        => new() { Succeeded = true, PaymentId = paymentId, RedirectUrl = redirectUrl, ProviderPaymentRef = providerRef, Amount = amount };

    public static SeatCheckoutResult Fail(SeatCheckoutFailureReason reason)
        => new() { Succeeded = false, FailureReason = reason };
}

/// <summary>Typed failure reasons for <see cref="ISeatService.StartSeatCheckoutAsync"/>.</summary>
public enum SeatCheckoutFailureReason
{
    /// <summary>The parent is on the Free plan — extra seats are not available on Free.</summary>
    FreePlanNotAllowed = 1,

    /// <summary>Purchasing the requested quantity would push the total above <c>seats.max</c>.</summary>
    ExceedsMaxSeats = 2,

    /// <summary>The parent has no active subscription.</summary>
    NoActiveSubscription = 3,

    /// <summary>The payment provider returned an error when creating the checkout session.</summary>
    ProviderUnavailable = 4,
}

/// <summary>Outcome of a seat reservation attempt.</summary>
public enum SeatReservationOutcome
{
    /// <summary>A new reservation was successfully inserted.</summary>
    Reserved = 0,

    /// <summary>No free seats are available (active count = max or no subscription).</summary>
    NoFreeSeat = 1,

    /// <summary>The same idempotency key was already used — prior reservation returned.</summary>
    AlreadyReserved = 2,
}

/// <summary>Result returned by <see cref="ISeatService.ReserveSeatAsync"/>.</summary>
public sealed record SeatReservationServiceResult(
    SeatReservationOutcome Outcome,
    int? ReservationId);

/// <summary>Snapshot returned by <see cref="ISeatService.GetSeatStatusAsync"/>.</summary>
public sealed record SeatStatusSnapshot(
    int IncludedSeats,
    int PurchasedExtraSeats,
    int TotalSeats,
    int OccupiedSeats,
    int AvailableSeats,
    int MaxSeats,
    IReadOnlyList<ChildSeatStatusDto> Children)
{
    // ── P10-15-BE-6: grace window info ───────────────────────────────────────────

    /// <summary>UTC timestamp when the seat grace window ends. Null when no grace is active.</summary>
    public DateTime? GraceEndsAt { get; init; }

    /// <summary>True when a seat grace window is currently open.</summary>
    public bool IsInGrace { get; init; }
}

/// <summary>Per-child seat status within a <see cref="SeatStatusSnapshot"/> (P10-15 extended).</summary>
public sealed record ChildSeatStatusDto(
    int ChildId,
    string Status)    // maps from SeatStatus enum name — no free-text
{
    // ── P10-15-BE-6: seat lifecycle state ────────────────────────────────────────

    /// <summary>Seat lifecycle state: "Active" or "NoSeatLocked".</summary>
    public string SeatState { get; init; } = "Active";

    /// <summary>UTC timestamp when voluntary removal is scheduled (cycle-end). Null if not scheduled.</summary>
    public DateTime? RemovalScheduledAt { get; init; }
}

/// <summary>Result returned by <see cref="ISeatService.ScheduleExtraSeatCancellationAsync"/>.</summary>
public sealed record SeatCancelResult(
    bool Succeeded,
    string? FailureReason);

/// <summary>Result returned by <see cref="ISeatService.ChooseActiveChildrenAsync"/>.</summary>
public sealed record SeatChoiceResult
{
    public bool Succeeded { get; init; }
    public SeatChoiceFailureReason? FailureReason { get; init; }

    /// <summary>Number of children whose SeatState changed as a result of this choice.</summary>
    public int ChangedCount { get; init; }

    public static SeatChoiceResult Ok(int changedCount)
        => new() { Succeeded = true, ChangedCount = changedCount };

    public static SeatChoiceResult Fail(SeatChoiceFailureReason reason)
        => new() { Succeeded = false, FailureReason = reason };
}

/// <summary>Typed failure reasons for <see cref="ISeatService.ChooseActiveChildrenAsync"/>.</summary>
public enum SeatChoiceFailureReason
{
    /// <summary>The number of requested active children exceeds the paid-active-seat limit.</summary>
    ExceedsPaidSeats = 1,

    /// <summary>One or more of the supplied child ids does not belong to the authenticated parent's family.</summary>
    ChildNotInFamily = 2,

    /// <summary>One or more of the children requested as Active are currently NoSeatLocked; they need a reactivation checkout first.</summary>
    LockedChildRequiresReactivation = 3,

    /// <summary>The parent has no active subscription.</summary>
    NoActiveSubscription = 4,
}
