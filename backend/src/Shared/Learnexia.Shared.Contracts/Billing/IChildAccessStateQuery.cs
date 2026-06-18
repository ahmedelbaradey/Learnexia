namespace Learnexia.Shared.Contracts.Billing;

/// <summary>
/// Cross-module seam: combined "is this child's AI access currently allowed?" (P10-18-BE-5).
///
/// <para>Returns <see langword="true"/> ONLY when BOTH conditions hold:
/// <list type="bullet">
///   <item><c>SeatState == Active</c> (billing-driven seat lifecycle — P10-15).</item>
///   <item><c>ParentPauseState == Active</c> (parent-initiated pause — P10-18).</item>
/// </list>
/// Either condition alone returns <see langword="false"/>.</para>
///
/// <para>No cross-module FK. <see cref="IsChildAccessAllowedAsync"/> takes a plain <c>int childId</c>.</para>
///
/// <para><strong>Implement in Billing.Infrastructure</strong>; register as Scoped in
/// <c>Billing.Infrastructure.DependencyInjection</c>. Consumed by the Ai module and any
/// other module that needs a unified access gate — they never reference Billing internals.</para>
///
/// <para><strong>Coordination with <see cref="ISeatStateQuery"/>:</strong> this seam supersedes
/// individual calls to <see cref="ISeatStateQuery.IsChildSeatActiveAsync"/> for access-gate
/// purposes. The Ai module should prefer this combined seam; the inner spend gate in
/// <c>CreditSpendService</c> checks both states independently for defence-in-depth.</para>
/// </summary>
public interface IChildAccessStateQuery
{
    /// <summary>
    /// Returns <see langword="true"/> if the child's AI access is currently allowed —
    /// i.e. the child holds an active seat AND has not been paused by their parent.
    /// Returns <see langword="false"/> if either condition fails.
    /// </summary>
    /// <param name="childId">The child user id (plain int — no cross-module FK).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> IsChildAccessAllowedAsync(int childId, CancellationToken ct = default);

    /// <summary>
    /// Returns a structured <see cref="ChildAccessDecision"/> indicating whether access is
    /// allowed, and — when denied — the specific reason (<see cref="ChildAccessDecision.SeatLocked"/>
    /// or <see cref="ChildAccessDecision.Paused"/>). Prefer this over
    /// <see cref="IsChildAccessAllowedAsync"/> when a caller needs to surface a differentiated
    /// localized message to the student (P10-18 security fix).
    /// </summary>
    /// <param name="childId">The child user id (plain int — no cross-module FK).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ChildAccessDecision> GetAccessDecisionAsync(int childId, CancellationToken ct = default);
}
