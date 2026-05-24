using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Commands.ResetPassword;

// P1-12 BE-6. Validates the single-use reset token (Identity enforces single-use + expiry) and applies the
// configured password policy via ResetPasswordAsync. A missing account and an invalid/expired token return
// the SAME generic localized failure (ResetPasswordInvalidLink) — the caller cannot distinguish them, so
// neither path is an enumeration oracle. On success we invalidate every OTHER session: bump the security
// stamp (cookie/stamp-validated tokens are rejected) AND delete the Redis refresh-token entry (reusing the
// exact key SignOutCommandHandler revokes) so no refresh token can be exchanged again.
public class ResetPasswordCommandHandler : BaseResponseHandler, ICommandHandler<ResetPasswordCommand, BaseResponse<string>>
{
    private readonly IIdentityServiceManager _service;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly IDistributedCache _distributedCache;
    private readonly ILoggerManager _logger;

    public ResetPasswordCommandHandler(
        IIdentityServiceManager service,
        IStringLocalizer<SharedResources> localizer,
        IDistributedCache distributedCache,
        ILoggerManager logger)
    {
        _service = service;
        _localizer = localizer;
        _distributedCache = distributedCache;
        _logger = logger;
    }

    public async Task<BaseResponse<string>> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _service.UserManagmentService.FindByEmailAsync(request.Email);

            // Unknown account → generic failure (same as a bad token below). Do not reveal which it was.
            if (user is null || !user.IsActive)
            {
                _logger.LogInfo("ResetPassword: no eligible account for the supplied email; returning generic failure.");
                return BadRequest<string>(_localizer[SharedResourcesKey.ResetPasswordInvalidLink]);
            }

            // ResetPasswordAsync validates the token (single-use + expiry) AND enforces the password policy.
            var result = await _service.UserManagmentService.ResetPasswordAsync(user, request.Token, request.NewPassword);
            if (!result.Succeeded)
            {
                // Log Identity detail server-side only (NOT the token); return a single generic message that
                // covers both "bad/expired token" and "password-policy" without disclosing which.
                _logger.LogWarn($"ResetPassword failed for user {user.Id}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                return BadRequest<string>(_localizer[SharedResourcesKey.ResetPasswordInvalidLink]);
            }

            await InvalidateOtherSessionsAsync(user);

            return Success<string>(_localizer[SharedResourcesKey.ResetPasswordSuccessful]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ResetPasswordCommand");
            return ServerError<string>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
        }
    }

    // Mirrors SignOutCommandHandler's revocation: bump the security stamp so existing stamp-validated
    // tokens are rejected, and delete the Redis refresh-token entry so it can never be exchanged again.
    private async Task InvalidateOtherSessionsAsync(User user)
    {
        try
        {
            await _service.UserManagmentService.UpdateSecurityStampAsync(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"ResetPassword: failed to update security stamp for user {user.Id}.");
        }

        try
        {
            await _distributedCache.RemoveAsync($"userrefreshtoken-{user.Id}");
            _logger.LogInfo($"ResetPassword: revoked refresh token for user {user.Id}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"ResetPassword: failed to revoke refresh token for user {user.Id}.");
        }
    }
}
