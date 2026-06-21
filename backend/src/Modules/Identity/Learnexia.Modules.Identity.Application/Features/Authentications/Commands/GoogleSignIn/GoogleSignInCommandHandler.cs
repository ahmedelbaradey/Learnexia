using System.IdentityModel.Tokens.Jwt;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Features.Authentications.Commands.SignIn;
using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Commands.GoogleSignIn;

public class GoogleSignInCommandHandler : BaseResponseHandler, ICommandHandler<GoogleSignInCommand, BaseResponse<JwtAuthResponse>>
{
    // ASP.NET Identity external-login provider key/display name for Google (technical identifier, not user-facing).
    private const string GoogleLoginProvider = "Google";

    private readonly IGoogleTokenValidator _googleTokenValidator;
    private readonly IIdentityServiceManager _service;
    private readonly ISessionManagementService _sessionManagementService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;
    private readonly IPublisher _publisher;

    public GoogleSignInCommandHandler(
        IGoogleTokenValidator googleTokenValidator,
        IIdentityServiceManager service,
        ISessionManagementService sessionManagementService,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger,
        IPublisher publisher)
    {
        _googleTokenValidator = googleTokenValidator;
        _service = service;
        _sessionManagementService = sessionManagementService;
        _localizer = localizer;
        _logger = logger;
        _publisher = publisher;
    }

    public async Task<BaseResponse<JwtAuthResponse>> Handle(GoogleSignInCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // 1) Verify the Google ID token. Any failure returns null (never throws/leaks internals).
            var googleUser = await _googleTokenValidator.ValidateAsync(request.IdToken, cancellationToken);
            if (googleUser == null || string.IsNullOrWhiteSpace(googleUser.Email) || string.IsNullOrWhiteSpace(googleUser.Subject))
                return Unauthorized<JwtAuthResponse>(_localizer[SharedResourcesKey.GoogleSignInFailed]);

            // Google must have verified the email — we trust it to confirm the address.
            if (!googleUser.EmailVerified)
                return BadRequest<JwtAuthResponse>(_localizer[SharedResourcesKey.GoogleEmailNotVerified]);

            // 2) Find-or-create the user by email.
            var user = await _service.UserManagmentService.FindByEmailAsync(googleUser.Email);
            var isNewUser = false;

            if (user == null)
            {
                user = await CreateExternalParentAsync(googleUser);
                if (user == null)
                    return ServerError<JwtAuthResponse>(_localizer[SharedResourcesKey.GoogleSignInFailed]);

                isNewUser = true;
            }
            else
            {
                if (!user.IsActive)
                    return BadRequest<JwtAuthResponse>(_localizer[SharedResourcesKey.LoginAccountDeactivated]);

                // Ensure the Google external login is linked to the existing account (idempotent).
                await EnsureGoogleLoginLinkedAsync(user, googleUser.Subject);
            }

            // 3) Issue OUR JWT + session — at exact parity with SignInCommandHandler / RegisterParentCommandHandler.
            var accessToken = await _service.AuthenticationService.GetJwtToken(user);
            accessToken.UserId = user.Id;
            accessToken.IsFirstLogin = isNewUser || !user.RegistrationIsCompleted;

            var sessionInfo = ExtractSessionInfoFromToken(accessToken.AccessToken);
            if (sessionInfo != null)
            {
                try
                {
                    var session = await _sessionManagementService.CreateSessionAsync(user.Id, sessionInfo.JwtId, sessionInfo.SessionId);
                    accessToken.SessionId = session.SessionId;
                }
                catch (Exception ex)
                {
                    // Session creation failed. The issued token's first authenticated call will be rejected by
                    // OnTokenValidated because no session is persisted under its SessionId claim. Log loudly.
                    _logger.LogError(ex, $"GoogleSignIn: failed to create session for user {user.Id}. The issued token will be invalid on first authenticated call.");
                }
            }

            if (isNewUser)
                await PublishUserRegisteredEventAsync(user, cancellationToken);

            return Success(accessToken);
        }
        catch (Exception ex)
        {
            // Never echo raw exception text to an anonymous caller; log detail, return a generic localized message.
            _logger.LogError(ex, "Error: in GoogleSignInCommand");
            return ServerError<JwtAuthResponse>(_localizer[SharedResourcesKey.GoogleSignInFailed]);
        }
    }

    /// <summary>
    /// Creates a passwordless (external-only) Parent account for a first-time Google sign-in.
    /// EmailConfirmed is set (Google verified the address). AcceptedTermsAtUtc is stamped at account
    /// creation as the consent point — see the FE/legal follow-up note in the BE-5 report. Each Identity
    /// call commits per call (no Unit of Work); if a step after CreateAsync fails we log and abort cleanly.
    /// </summary>
    private async Task<User?> CreateExternalParentAsync(GoogleUserInfo googleUser)
    {
        var user = new User
        {
            UserName = googleUser.Email,
            Email = googleUser.Email,
            EmailConfirmed = true,
            FullName = string.IsNullOrWhiteSpace(googleUser.Name)
                ? GetEmailLocalPart(googleUser.Email)
                : googleUser.Name!,
            PreferredLanguage = "ar-EG",
            CountryCode = "+20",
            AcceptedTermsAtUtc = DateTime.UtcNow,
            RegistrationIsCompleted = false,
            CreatedAt = DateTime.UtcNow,
        };

        // Passwordless creation — the account authenticates only through the external (Google) login.
        var createResult = await _service.UserManagmentService.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            _logger.LogWarn($"Google sign-in: user creation failed: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            return null;
        }

        await _service.UserManagmentService.AddToRoleAsync(user, Roles.Parent.ToString());
        await EnsureGoogleLoginLinkedAsync(user, googleUser.Subject);

        return user;
    }

    /// <summary>Links the Google external login to the user if not already present (idempotent).</summary>
    private async Task EnsureGoogleLoginLinkedAsync(User user, string subject)
    {
        var logins = await _service.UserManagmentService.GetLoginsAsync(user);
        if (logins.Any(l => l.LoginProvider == GoogleLoginProvider && l.ProviderKey == subject))
            return;

        var addResult = await _service.UserManagmentService.AddLoginAsync(
            user, new UserLoginInfo(GoogleLoginProvider, subject, GoogleLoginProvider));

        if (!addResult.Succeeded)
            _logger.LogWarn($"Google sign-in: failed to link external login for user {user.Id}: {string.Join(", ", addResult.Errors.Select(e => e.Description))}");
    }

    private async Task PublishUserRegisteredEventAsync(User user, CancellationToken cancellationToken)
    {
        try
        {
            var integrationEvent = new UserRegisteredIntegrationEvent(
                EventId: Guid.NewGuid(),
                OccurredOnUtc: DateTime.UtcNow,
                UserId: user.Id,
                UserName: user.UserName ?? string.Empty);

            await _publisher.Publish(integrationEvent, cancellationToken);

            _logger.LogInfo($"Published UserRegisteredIntegrationEvent for user {user.Id}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to publish UserRegisteredIntegrationEvent for user {user.Id}.");
        }
    }

    private static string GetEmailLocalPart(string email)
    {
        var atIndex = email.IndexOf('@');
        return atIndex > 0 ? email[..atIndex] : email;
    }

    private static SessionExtractionInfo? ExtractSessionInfoFromToken(string? accessToken)
    {
        if (string.IsNullOrEmpty(accessToken))
            return null;

        try
        {
            var jwtHandler = new JwtSecurityTokenHandler();
            if (!jwtHandler.CanReadToken(accessToken))
                return null;

            var jwtToken = jwtHandler.ReadJwtToken(accessToken);
            var jwtIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);
            var sessionIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "SessionId");

            if (jwtIdClaim == null || sessionIdClaim == null)
                return null;

            return new SessionExtractionInfo
            {
                JwtId = jwtIdClaim.Value,
                SessionId = sessionIdClaim.Value,
            };
        }
        catch
        {
            return null;
        }
    }
}
