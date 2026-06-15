using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Credits.Dtos;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Credits.Commands.SpendCredit;

/// <summary>
/// Atomically debits credits from a child's account.
///
/// Algorithm (ADR-0001 — explicit transaction, no UoW):
/// <list type="number">
///   <item>Open an explicit DB transaction.</item>
///   <item>Load <c>CreditAccount</c> with the <c>xmin</c> concurrency token.</item>
///   <item>Idempotency pre-check (SELECT before INSERT — cheaper than catching constraint).</item>
///   <item>If <c>TotalBalance &lt; Amount</c> → return <see cref="DebitOutcome.InsufficientBalance"/> (no write).</item>
///   <item>Compute Granted-first split; call <c>account.Debit()</c>; insert the returned <c>CreditTransaction</c>.</item>
///   <item>Commit. On <see cref="DbUpdateConcurrencyException"/> → retry (bounded). On unique-key violation → idempotent success.</item>
/// </list>
/// </summary>
public class SpendCreditCommandHandler : BaseResponseHandler, ICommandHandler<SpendCreditCommand, BaseResponse<DebitResultDto>>
{
    private const int MaxRetries = 3;

    private readonly IBillingDbContext _db;
    private readonly ILoggerManager _logger;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public SpendCreditCommandHandler(
        IBillingDbContext db,
        ILoggerManager logger,
        ICurrentUserService currentUser,
        IStringLocalizer<SharedResources> localizer)
    {
        _db = db;
        _logger = logger;
        _currentUser = currentUser;
        _localizer = localizer;
    }

    public async Task<BaseResponse<DebitResultDto>> Handle(SpendCreditCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CreditReasonCode>(request.ReasonCode, ignoreCase: true, out var reasonCode))
            reasonCode = CreditReasonCode.Unspecified;

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await ExecuteDebitAsync(request, reasonCode, cancellationToken);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxRetries)
            {
                _logger.LogWarn($"SpendCredit: concurrency conflict attempt {attempt + 1} for childId={request.ChildId}. Retrying.");
                _db.ChangeTracker.Clear();
            }
            catch (DbUpdateException dbEx) when (IsUniqueViolation(dbEx))
            {
                // Duplicate idempotency key from a racing concurrent call — return idempotent result.
                _logger.LogInfo($"SpendCredit: idempotent duplicate key={request.IdempotencyKey}.");
                return await BuildIdempotentResultAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in SpendCreditCommand for childId={request.ChildId}");
                return ServerError<DebitResultDto>(_localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
            }
        }

        _logger.LogError(new Exception("Retries exhausted"), $"SpendCredit: exceeded {MaxRetries} retries childId={request.ChildId}");
        return ServerError<DebitResultDto>(_localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
    }

    private async Task<BaseResponse<DebitResultDto>> ExecuteDebitAsync(
        SpendCreditCommand request,
        CreditReasonCode reasonCode,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var account = await _db.CreditAccounts
            .FirstOrDefaultAsync(a => a.ChildId == request.ChildId, ct);

        if (account is null)
        {
            await tx.RollbackAsync(ct);
            return new BaseResponse<DebitResultDto>
            {
                Successed = false,
                StatusCode = System.Net.HttpStatusCode.NotFound,
                Message = _localizer[SharedResourcesKey.CreditAccountNotFound],
                Data = new DebitResultDto { Outcome = DebitOutcome.InsufficientBalance },
            };
        }

        // Idempotency pre-check.
        if (await _db.CreditTransactions
                .AnyAsync(t => t.IdempotencyKey == request.IdempotencyKey, ct))
        {
            await tx.RollbackAsync(ct);
            return await BuildIdempotentResultAsync(request, ct);
        }

        if (account.TotalBalance < request.Amount)
        {
            await tx.RollbackAsync(ct);
            return new BaseResponse<DebitResultDto>
            {
                Successed = false,
                StatusCode = System.Net.HttpStatusCode.UnprocessableEntity,
                Message = _localizer[SharedResourcesKey.InsufficientBalance],
                Data = new DebitResultDto { Outcome = DebitOutcome.InsufficientBalance },
            };
        }

        // Granted-first split.
        var fromGranted = Math.Min(request.Amount, account.GrantedBalance);
        var fromPurchased = request.Amount - fromGranted;

        var creditTransaction = account.Debit(
            fromGranted, fromPurchased, reasonCode, request.IdempotencyKey, request.RelatedActionId);
        await _db.CreditTransactions.AddAsync(creditTransaction, ct);

        await _db.SaveChangesAsync(_currentUser.UserId ?? 0);
        await tx.CommitAsync(ct);

        return Success(new DebitResultDto
        {
            Charged = true,
            FromGranted = fromGranted,
            FromPurchased = fromPurchased,
            ResultingTotal = account.TotalBalance,
            Outcome = DebitOutcome.Charged,
        });
    }

    private async Task<BaseResponse<DebitResultDto>> BuildIdempotentResultAsync(
        SpendCreditCommand request,
        CancellationToken ct)
    {
        var prior = await _db.CreditTransactions
            .FirstOrDefaultAsync(t => t.IdempotencyKey == request.IdempotencyKey, ct);

        return new BaseResponse<DebitResultDto>
        {
            Successed = true,
            StatusCode = System.Net.HttpStatusCode.OK,
            Message = _localizer[SharedResourcesKey.CreditIdempotentDuplicate],
            Data = new DebitResultDto
            {
                Charged = true,
                FromGranted = 0,
                FromPurchased = 0,
                ResultingTotal = prior is not null
                    ? prior.ResultingGrantedBalance + prior.ResultingPurchasedBalance
                    : 0,
                Outcome = DebitOutcome.DuplicateIdempotent,
            },
        };
    }

    // Postgres unique-violation SQLSTATE 23505.
    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("23505", StringComparison.Ordinal) == true
           || ex.InnerException?.Message.Contains("UX_CreditTransactions_IdempotencyKey",
               StringComparison.OrdinalIgnoreCase) == true;
}
