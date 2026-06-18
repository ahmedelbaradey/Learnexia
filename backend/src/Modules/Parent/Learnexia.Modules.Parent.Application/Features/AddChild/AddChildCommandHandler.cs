using System.Security.Cryptography;
using System.Text;
using AutoMapper;
using Learnexia.Modules.Parent.Application.Abstractions;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Parent.Application.Features.AddChild;

// Relocated from Identity. The Identity-side account creation (duplicate-email check, UserManager.CreateAsync,
// AddToRoleAsync Student, compensating delete, UserRegisteredIntegrationEvent publish) is encapsulated behind
// the IChildAccountService seam — this handler owns only the family-link concern (the parent schema link row).
// The acting parent is ALWAYS the JWT-resolved caller; never read from the request body.
//
// P10-14-BE-5: seat reservation wired BEFORE child account creation. Flow:
//   1. ReserveSeatAsync → NoFreeSeat → reject (localized), no child created, no orphan reservation.
//   2. ReserveSeatAsync → Reserved → CreateChildAsync → success → ActivateSeatAsync.
//   3. ReserveSeatAsync → Reserved → CreateChildAsync → failure → ReleaseSeatAsync (compensation).
// Cross-module via ISubscriptionSeatContract (Shared.Contracts) only — never Billing.* types.
public class AddChildCommandHandler : BaseResponseHandler, ICommandHandler<AddChildCommand, BaseResponse<AddedChildResponse>>
{
    private readonly IChildAccountService _childAccountService;
    private readonly ILinkParentStudentService _linkService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISubscriptionSeatContract _seatContract;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public AddChildCommandHandler(
        IChildAccountService childAccountService,
        ILinkParentStudentService linkService,
        ICurrentUserService currentUserService,
        ISubscriptionSeatContract seatContract,
        IMapper mapper,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _childAccountService = childAccountService;
        _linkService         = linkService;
        _currentUserService  = currentUserService;
        _seatContract        = seatContract;
        _mapper              = mapper;
        _localizer           = localizer;
        _logger              = logger;
    }

    public async Task<BaseResponse<AddedChildResponse>> Handle(AddChildCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var parentId = _currentUserService.UserId;
            if (parentId is null)
                return Unauthorized<AddedChildResponse>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            // ── P10-14-BE-5: Reserve a seat BEFORE creating the child ──────────────────────
            // NIT (a) child-privacy: hash the plaintext email before persisting to IdempotencyKey.
            // SHA256 hex, first 16 chars — collision risk is negligible for this use-case.
            // P10-15-BE-10: the reservation key must be UNIQUE PER ATTEMPT, not purely email-derived.
            // A duplicate-email add is a NEW attempt that must be rejected with 400 — NOT treated as an
            // idempotent retry of the original (which, under the key-based reserve/release, would
            // mis-resolve and release the EXISTING sibling's active seat). The per-attempt nonce keeps
            // each attempt's reservation independent; concurrency safety comes from the SELECT…FOR UPDATE
            // serialization in ReserveSeatAsync, and child-email uniqueness is enforced at creation.
            var emailHashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(request.Email.ToLowerInvariant()));
            var emailHash      = Convert.ToHexStringLower(emailHashBytes)[..16];
            var reservationKey = $"add-child-seat:{parentId.Value}:{emailHash}:{Guid.NewGuid():N}";
            var seatResult = await _seatContract.ReserveSeatAsync(
                parentId.Value,
                0,              // childId not yet known — 0 placeholder; updated to Active after creation
                reservationKey,
                cancellationToken);

            if (seatResult == SeatReservationResult.NoFreeSeat)
            {
                _logger.LogInfo(
                    $"AddChildCommand: no free seat for parent={parentId.Value}. Child NOT created.");
                return new BaseResponse<AddedChildResponse>
                {
                    Successed  = false,
                    StatusCode = System.Net.HttpStatusCode.Conflict,
                    Message    = _localizer[SharedResourcesKey.NoFreeSeatAvailable],
                };
            }

            // Seat reserved (Reserved or AlreadyReserved idempotent).
            // ── Create the child account ───────────────────────────────────────────────────
            var createResult = await _childAccountService.CreateChildAsync(
                new CreateChildRequest(
                    Email           : request.Email,
                    Password        : request.Password,
                    FullName        : request.FullName,
                    Language        : request.Language,
                    Country         : request.Country,
                    Grade           : request.Grade,
                    ActingParentId  : parentId.Value,
                    LearningLanguage: request.LearningLanguage),
                cancellationToken);

            if (!createResult.Succeeded || createResult.Profile is null)
            {
                // ── COMPENSATION: release the reserved seat so the ceiling is not wasted ──
                // P10-15-BE-10: thread reservationKey so the service resolves the EXACT row
                // by idempotency key — not by "most-recent childId=0" (ambiguous under concurrency).
                await _seatContract.ReleaseSeatAsync(parentId.Value, 0, cancellationToken,
                    reservationKey: reservationKey);

                _logger.LogError(null,
                    $"AddChildCommand: child creation failed for parent={parentId.Value}, " +
                    $"reason={createResult.ErrorCode}. Seat released.");
                return createResult.ErrorCode switch
                {
                    ChildAccountError.DuplicateEmail => BadRequest<AddedChildResponse>(_localizer[SharedResourcesKey.ProfileDuplicateEmail]),
                    _                                => ServerError<AddedChildResponse>(_localizer[SharedResourcesKey.SystemErrorSavingData]),
                };
            }

            // ── Auto-link (commits immediately in the parent schema) ───────────────────────
            try
            {
                await _linkService.LinkAsync(parentId.Value, createResult.ChildUserId, cancellationToken);
            }
            catch (Exception linkEx)
            {
                // Link failed after child creation — compensate the seat reservation.
                // P10-15-BE-10: key-based release so we release the exact row for this call.
                await _seatContract.ReleaseSeatAsync(parentId.Value, 0, cancellationToken,
                    reservationKey: reservationKey);
                _logger.LogError(linkEx,
                    $"AddChildCommand: link failed for parent={parentId.Value}, child={createResult.ChildUserId}. Seat released.");
                return ServerError<AddedChildResponse>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
            }

            // ── Activate the seat (Reserved → Active) ─────────────────────────────────────
            // Best-effort: if activation fails the seat remains Reserved until the next
            // reconciliation cycle (P10-15 enforcement handles stale Reserved seats).
            // P10-15-BE-10: thread reservationKey so ActivateSeatAsync finds the EXACT row
            // for this call's placeholder and stamps the real childId — not another concurrent
            // call's childId=0 row.
            await _seatContract.ActivateSeatAsync(parentId.Value, createResult.ChildUserId, cancellationToken,
                reservationKey: reservationKey);

            _logger.LogInfo(
                $"AddChildCommand: added child {createResult.Profile.Id} for parent={parentId.Value}. Seat activated.");
            return Success(_mapper.Map<AddedChildResponse>(createResult.Profile));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in AddChildCommand");
            return ServerError<AddedChildResponse>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
        }
    }
}
