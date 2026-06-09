using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.UpdateChildProfile;

/// <summary>
/// Handles <see cref="UpdateChildProfileCommand"/> (P7-08 BE-1).
///
/// Performs a plain, non-destructive update of the child's UI language
/// (<c>PreferredLanguage</c>) and <c>Nationality</c> (country). Deliberately skips:
///   - Grade (owned by OverrideChildGradeCommand — emits ChildGradeChangedIntegrationEvent)
///   - LearningLanguage (owned by AdminChangeLearningLanguageCommand — confirm-gated, destructive)
///
/// Guard order:
///   1. Resolve target user; missing → NotFound.
///   2. Validate the user holds the Student role → not a child → BadRequest.
///   3. Apply partial updates only to the requested fields (null = no change).
///   4. Commit via UserManager.UpdateAsync.
///   5. Best-effort publish AdminActionPerformedEvent(ChildProfileUpdated) post-commit.
///
/// Grade claim staleness note (P7-07 wave, Q-C3): grade is NOT a JWT claim today.
/// PreferredLanguage changes take effect on the next token (stateless JWT — no live rewrite).
/// </summary>
public class UpdateChildProfileCommandHandler
    : BaseResponseHandler,
      ICommandHandler<UpdateChildProfileCommand, BaseResponse<string>>
{
    private readonly IIdentityServiceManager _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;

    public UpdateChildProfileCommandHandler(
        IIdentityServiceManager service,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer,
        IPublisher publisher,
        ILoggerManager logger)
    {
        _service = service;
        _currentUserService = currentUserService;
        _localizer = localizer;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<BaseResponse<string>> Handle(
        UpdateChildProfileCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var adminUserId = _currentUserService.UserId.GetValueOrDefault();

            // Guard 1 — Resolve target user.
            var child = await _service.UserManagmentService.FindByIdWithRolesAsync(request.ChildId.ToString());
            if (child is null)
                return NotFound<string>(_localizer[SharedResourcesKey.UserNotFound]);

            // Guard 2 — Validate this is a child/student account.
            var roles = await _service.UserManagmentService.GetUserRolesAsync(child);
            if (!roles.Any(r => string.Equals(r, Roles.Student.ToString(), StringComparison.OrdinalIgnoreCase)))
                return BadRequest<string>(_localizer[SharedResourcesKey.TargetUserIsNotAChild]);

            // Step 3 — Apply partial updates (null = no change, prevents accidental field wipes).
            bool changed = false;

            if (request.PreferredLanguage is not null)
            {
                child.PreferredLanguage = NormalizePreferredLanguage(request.PreferredLanguage);
                changed = true;
            }

            if (request.Country is not null)
            {
                child.Nationality = request.Country;
                changed = true;
            }

            if (changed)
            {
                child.UpdatedAt = DateTime.UtcNow;
                child.UpdatedBy = adminUserId;

                var updateResult = await _service.UserManagmentService.UpdateAsync(child);
                if (!updateResult.Succeeded)
                {
                    _logger.LogError(null, $"UpdateChildProfileCommandHandler: UpdateAsync failed for child {request.ChildId}.");
                    return BadRequest<string>(_localizer[SharedResourcesKey.ChildAdminOperationSystemError]);
                }
            }

            _logger.LogInfo($"UpdateChildProfileCommandHandler: child {request.ChildId} profile updated by admin {adminUserId}.");

            // Step 5 — Best-effort audit event post-mutation (no grade/language PII in Details).
            await PublishAuditEventAsync(adminUserId, request.ChildId, cancellationToken);

            return Success<string>(_localizer[SharedResourcesKey.ChildProfileUpdatedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in UpdateChildProfileCommandHandler for child {request.ChildId}");
            return ServerError<string>(_localizer[SharedResourcesKey.ChildAdminOperationSystemError]);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Normalizes short language codes to full culture codes consistent with
    /// <c>IdentityChildAccountService.NormalizeLanguage</c> (same mapping, inlined here to
    /// avoid cross-layer dependency — the seam is a cross-module boundary, not in-module).
    /// </summary>
    private static string NormalizePreferredLanguage(string language) =>
        language.Trim().ToLowerInvariant() switch
        {
            "en" or "en-us" => "en-US",
            "ar" or "ar-eg" => "ar-EG",
            _ => "ar-EG",
        };

    private async Task PublishAuditEventAsync(int adminUserId, int childId, CancellationToken ct)
    {
        try
        {
            await _publisher.Publish(
                new AdminActionPerformedEvent(
                    EventId: Guid.NewGuid(),
                    OccurredAtUtc: DateTime.UtcNow,
                    AdminUserId: adminUserId,
                    Action: AdminActions.ChildProfileUpdated,
                    TargetEntityType: "User",
                    TargetEntityId: childId,
                    Details: null), // No PII — field changes tracked by event log consumer (P7-12)
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish AdminActionPerformedEvent(ChildProfileUpdated) — ignored (best-effort).");
        }
    }
}
