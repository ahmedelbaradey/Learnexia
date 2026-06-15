using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.BillingHistory.Dtos;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Implements <see cref="IBillingHistoryQueryService"/> — the Option-C service seam for
/// billing history and receipt read operations.
///
/// <para><strong>Option C contract:</strong> all EF, projection, ordering, and pagination
/// logic lives HERE. The Application-layer handlers inject the interface and stay EF-free.</para>
///
/// <para><strong>IDOR scoping:</strong>
/// <list type="bullet">
///   <item><see cref="GetHistoryAsync"/> applies a WHERE filter on <c>parentUserId</c> at the
///         SQL level — a parent can never read another parent's rows regardless of the <c>paymentId</c>
///         range they guess.</item>
///   <item><see cref="GetReceiptAsync"/> re-checks <c>Payment.ParentUserId == parentUserId</c>
///         inside the projection — returns <c>null</c> for both "not found" and "not owned",
///         making enumeration attacks indistinguishable.</item>
/// </list>
/// </para>
///
/// <para><strong>No IQueryable leak:</strong> this class composes the full query, projects to
/// DTOs, applies pagination, and returns materialized <see cref="PaginatedResult{T}"/> or a
/// nullable DTO — EF types never cross the service boundary.</para>
/// </summary>
public sealed class BillingHistoryQueryService : IBillingHistoryQueryService
{
    private readonly BillingDbContext _db;
    private readonly ILoggerManager _logger;
    private readonly IConfiguration _configuration;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public BillingHistoryQueryService(
        BillingDbContext db,
        ILoggerManager logger,
        IConfiguration configuration,
        IStringLocalizer<SharedResources> localizer)
    {
        _db            = db;
        _logger        = logger;
        _configuration = configuration;
        _localizer     = localizer;
    }

    /// <inheritdoc/>
    public async Task<PaginatedResult<BillingHistoryItemDto>> GetHistoryAsync(
        int parentUserId,
        int pageNumber,
        int pageSize,
        CancellationToken ct)
    {
        // Defensive page-size cap (service-layer guard in addition to handler-layer).
        pageSize = Math.Min(pageSize, 100);

        // Query: project Payment rows for this parent, newest-first.
        // IDOR: WHERE parentUserId is enforced in SQL — never trusts a client-supplied id.
        var query = _db.Payments
            .AsNoTracking()
            .Where(p => p.ParentUserId == parentUserId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new BillingHistoryItemDto
            {
                PaymentId          = p.Id,
                OccurredAt         = p.CreatedAt,
                Kind               = p.Kind.ToString(),
                Amount             = p.Amount,
                Currency           = p.Currency,
                Status             = p.Status.ToString(),
                TargetChildId      = p.TargetChildId,
                ProviderPaymentRef = p.ProviderPaymentRef,
            });

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        _logger.LogInfo(
            $"BillingHistoryQueryService.GetHistory: parentId={parentUserId}, total={totalCount}, page={pageNumber}.");

        return new PaginatedResult<BillingHistoryItemDto>(items, totalCount, pageNumber, pageSize);
    }

    /// <inheritdoc/>
    public async Task<ReceiptDto?> GetReceiptAsync(
        int parentUserId,
        int paymentId,
        CancellationToken ct)
    {
        // IDOR re-check: load only if parentUserId matches (not just by paymentId).
        var payment = await _db.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.Id == paymentId && p.ParentUserId == parentUserId,
                ct);

        if (payment is null)
        {
            _logger.LogInfo(
                $"BillingHistoryQueryService.GetReceipt: paymentId={paymentId} not found or not owned by parentId={parentUserId}.");
            return null; // Intentionally vague (anti-IDOR: "not found" == "not owned")
        }

        // Seller fields from config (never hard-coded — OQ-FIN-1 pending finance confirmation).
        var sellerName      = _configuration["Billing:Receipt:SellerName"]      ?? "Learnexia";
        var sellerTaxNumber = _configuration["Billing:Receipt:SellerTaxNumber"];

        var description = payment.Kind == Domain.Enums.PaymentKind.Pack
            ? _localizer[SharedResourcesKey.ReceiptDescriptionEnergyPack].Value
            : _localizer[SharedResourcesKey.ReceiptDescriptionPremiumSubscription].Value;

        return new ReceiptDto
        {
            PaymentId          = payment.Id,
            ProviderPaymentRef = payment.ProviderPaymentRef,
            PaidAt             = payment.CreatedAt,
            GrossAmount        = payment.Amount,
            Currency           = payment.Currency,
            NetAmount          = null,      // OQ-FIN-1: pending VAT field confirmation
            VatAmount          = null,      // OQ-FIN-1: pending VAT field confirmation
            InvoiceNumber      = null,      // OQ-FIN-1: pending sequential invoice number
            Description        = description,
            Kind               = payment.Kind.ToString(),
            TargetChildId      = payment.TargetChildId,
            SellerName         = sellerName,
            SellerTaxNumber    = sellerTaxNumber,
            Status             = payment.Status.ToString(),
        };
    }
}
