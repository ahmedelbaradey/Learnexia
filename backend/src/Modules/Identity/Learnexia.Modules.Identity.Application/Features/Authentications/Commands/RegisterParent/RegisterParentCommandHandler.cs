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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Commands.RegisterParent;

public class RegisterParentCommandHandler : BaseResponseHandler, ICommandHandler<RegisterParentCommand, BaseResponse<JwtAuthResponse>>
{
    private readonly IIdentityServiceManager _service;
    private readonly ISessionManagementService _sessionManagementService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;
    private readonly IPublisher _publisher;
    private readonly ICaptchaVerifier _captchaVerifier;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RegisterParentCommandHandler(
        IIdentityServiceManager service,
        ISessionManagementService sessionManagementService,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger,
        IPublisher publisher,
        ICaptchaVerifier captchaVerifier,
        IHttpContextAccessor httpContextAccessor)
    {
        _service = service;
        _sessionManagementService = sessionManagementService;
        _localizer = localizer;
        _logger = logger;
        _publisher = publisher;
        _captchaVerifier = captchaVerifier;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<BaseResponse<JwtAuthResponse>> Handle(RegisterParentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Anti-automation gate (P1-13 BE-4): verify the CAPTCHA token before touching the data store,
            // so a bot can't probe for duplicate emails or spam account creation. When Captcha:Enabled=false
            // (the default) the verifier is a no-op that returns true, so this is transparent in dev/tests;
            // when enabled it fails closed. The check lives here (not the validator) because it is async I/O
            // and config-gated. remoteIp is best-effort from the request context (siteverify works without it).
            var remoteIp = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
            var captchaOk = await _captchaVerifier.VerifyAsync(request.CaptchaToken, remoteIp, cancellationToken);
            if (!captchaOk)
                return BadRequest<JwtAuthResponse>(_localizer[SharedResourcesKey.CaptchaVerificationFailed]);

            // Race-safe backstop. The async validator already rejects duplicates with a 422; this guards
            // the window between validation and creation. UserName == Email for parent registration.
            var existingUser = await _service.UserManagmentService.FindByEmailAsync(request.Email);
            if (existingUser != null)
                return BadRequest<JwtAuthResponse>(_localizer[SharedResourcesKey.ProfileDuplicateEmail]);

            var user = new User
            {
                UserName = request.Email,
                Email = request.Email,
                FullName = string.IsNullOrWhiteSpace(request.FullName)
                    ? GetEmailLocalPart(request.Email)
                    : request.FullName!,
                PreferredLanguage = "ar-EG",
                CountryCode = "+20",
                // BE-9: optional country stored on Nationality; consent stamped as a timestamp
                // (presence == consent given). The validator guarantees AcceptedTerms is true here,
                // so AcceptedTermsAtUtc is always set on this path. Set before CreateAsync so both
                // persist in the single Identity write (no Unit of Work).
                Nationality = string.IsNullOrWhiteSpace(request.Country) ? null : request.Country,
                AcceptedTermsAtUtc = DateTime.UtcNow,
                RegistrationIsCompleted = false,
                CreatedAt = DateTime.UtcNow,
            };

            // Identity hashes the password and persists immediately (no Unit of Work). Surfaces
            // password-policy failures as defense-in-depth behind the validator.
            var result = await _service.UserManagmentService.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                // Log Identity error detail server-side only; return a generic localized message so we don't
                // disclose internals (and avoid an enumeration oracle), consistent with the sign-in handler.
                _logger.LogWarn($"RegisterParent failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                return BadRequest<JwtAuthResponse>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
            }

            // Role is server-assigned — always Parent, never client-supplied (AC-2). Seeded as PascalCase.
            await _service.UserManagmentService.AddToRoleAsync(user, Roles.Parent.ToString());

            // Token + session issuance at exact parity with SignInCommandHandler.
            var accessToken = await _service.AuthenticationService.GetJwtToken(user);
            accessToken.UserId = user.Id;
            accessToken.IsFirstLogin = true;

            var sessionInfo = ExtractSessionInfoFromToken(accessToken.AccessToken);
            if (sessionInfo != null)
            {
                try
                {
                    var session = await _sessionManagementService.CreateSessionAsync(user.Id, sessionInfo.JwtId);
                    accessToken.SessionId = session.SessionId;
                }
                catch
                {
                    // Session will be created by middleware on first API call.
                }
            }

            // Best-effort cross-module fan-out (existing UserRegistered path); a publish failure must not
            // affect the committed user or the success response (mirrors AddUserCommandHandler / ADR 0002).
            await PublishUserRegisteredEventAsync(user, cancellationToken);

            return Success(accessToken);
        }
        catch (Exception ex)
        {
            // Security finding #2: never echo raw exception text to an anonymous caller (it can carry
            // DB/Identity internals). Log the detail; return a generic localized message to the client.
            _logger.LogError(ex, "Error: in RegisterParentCommand");
            return ServerError<JwtAuthResponse>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
        }
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
