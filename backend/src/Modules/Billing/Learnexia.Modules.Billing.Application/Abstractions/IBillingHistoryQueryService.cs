using Learnexia.Modules.Billing.Application.Features.BillingHistory.Dtos;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Abstractions;

/// <summary>
/// Service seam (Option C) for billing history and receipt read operations.
///
/// <para><strong>Option C contract:</strong> Application-layer handlers inject THIS interface,
/// not <see cref="IBillingDbContext"/>. All EF, projection, and pagination logic lives in the
/// Infrastructure implementation. Handlers stay EF-free.</para>
///
/// <para><strong>IDOR scoping:</strong>
/// <list type="bullet">
///   <item><see cref="GetHistoryAsync"/> filters strictly to <c>parentUserId</c> — a parent
///         can never see another parent's payment rows.</item>
///   <item><see cref="GetReceiptAsync"/> re-checks <c>Payment.ParentUserId == parentUserId</c>
///         inside the impl (defence-in-depth) before returning any data — prevents IDOR even
///         if the caller passes a guessed <c>paymentId</c>.</item>
///   <item>Admin overload: when <c>adminView = true</c>, <c>parentUserId</c> is treated as the
///         subject (the parent being inspected) and is not compared to the current session user.</item>
/// </list>
/// </para>
///
/// <para><strong>Never return <c>IQueryable</c>:</strong> the implementation composes the query,
/// <c>ProjectTo</c>, pagination, and <c>ORDER BY</c> server-side and returns a materialized
/// <see cref="PaginatedResult{T}"/> or a nullable DTO. No EF types cross the seam boundary.</para>
/// </summary>
public interface IBillingHistoryQueryService
{
    /// <summary>
    /// Returns a chronological (newest-first) paginated list of the parent's payment and refund
    /// entries.
    ///
    /// <para>Strictly scoped to <c>parentUserId</c> — no cross-account IDOR possible.
    /// Includes both <c>Subscription</c> and <c>Pack</c> payment kinds.
    /// Refund rows are included as separate entries with <c>Status = Refunded</c>
    /// and a non-null <c>LinkedOriginalPaymentId</c> pointing back to the original charge.</para>
    /// </summary>
    /// <param name="parentUserId">The owning parent's user id (from JWT — never from the request body).</param>
    /// <param name="pageNumber">1-based page number.</param>
    /// <param name="pageSize">Items per page (capped at 100 internally).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated, chronological list of <see cref="BillingHistoryItemDto"/>.</returns>
    Task<PaginatedResult<BillingHistoryItemDto>> GetHistoryAsync(
        int parentUserId,
        int pageNumber,
        int pageSize,
        CancellationToken ct);

    /// <summary>
    /// Returns the receipt DTO for a single payment.
    ///
    /// <para><strong>IDOR defence-in-depth:</strong> verifies <c>Payment.ParentUserId == parentUserId</c>
    /// inside the implementation before returning any data. Returns <c>null</c> if the payment
    /// is not found OR does not belong to the caller — the handler maps both cases to 404
    /// (intentionally no distinction to prevent enumeration).</para>
    /// </summary>
    /// <param name="parentUserId">The requesting parent's user id (from JWT).</param>
    /// <param name="paymentId">The payment to fetch a receipt for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <see cref="ReceiptDto"/>, or <c>null</c> if not found / not owned.</returns>
    Task<ReceiptDto?> GetReceiptAsync(
        int parentUserId,
        int paymentId,
        CancellationToken ct);
}
