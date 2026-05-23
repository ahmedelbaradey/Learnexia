using System.IdentityModel.Tokens.Jwt;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Commands.SignIn;

public class SignInCommandHandler : BaseResponseHandler, ICommandHandler<SignInCommand, BaseResponse<JwtAuthResponse>>
{
    // Used only to burn an equivalent password-hash on the user-not-found path (timing-oracle mitigation).
    private static readonly User EnumerationGuardUser = new();

    private readonly SignInManager<User> _signInManager;
    private readonly IIdentityServiceManager _service;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ISessionManagementService _sessionManagementService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILoggerManager _logger;
    private readonly IPasswordHasher<User> _passwordHasher;

    public SignInCommandHandler(
        SignInManager<User> signInManager,
        IIdentityServiceManager service,
        IStringLocalizer<SharedResources> localizer,
        ISessionManagementService sessionManagementService,
        IHttpContextAccessor httpContextAccessor,
        ILoggerManager logger,
        IPasswordHasher<User> passwordHasher)
    {
        _signInManager = signInManager;
        _service = service;
        _localizer = localizer;
        _sessionManagementService = sessionManagementService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _passwordHasher = passwordHasher;
    }

    public async Task<BaseResponse<JwtAuthResponse>> Handle(SignInCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _service.UserManagmentService.FindByNameAsync(request.UserName);

            // Anti-enumeration (security finding #2): a non-existent user and a wrong password must be
            // indistinguishable to the caller, so an attacker cannot probe which emails are registered.
            // Both paths return the SAME generic "invalid credentials" result with the same status.
            if (user == null)
            {
                // Timing-oracle mitigation: hash the supplied password so the not-found path costs ~the same
                // as the wrong-password path (which runs the hasher), preventing enumeration by latency.
                _ = _passwordHasher.HashPassword(EnumerationGuardUser, request.Password);
                return BadRequest<JwtAuthResponse>(_localizer[SharedResourcesKey.LoginInvalidCredentials]);
            }

            if (!user.IsActive)
                return BadRequest<JwtAuthResponse>(_localizer[SharedResourcesKey.LoginAccountDeactivated]);

            // Account lockout engaged (BE-1): lockoutOnFailure: true makes each failure count toward the
            // configured MaxFailedAccessAttempts=5 / 5-min window. A successful sign-in resets the counter
            // (ASP.NET Identity calls ResetAccessFailedCount on success).
            var signInResult = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

            // Locked-out keeps its own clear localized message (standard, not an enumeration oracle).
            if (signInResult.IsLockedOut)
                return BadRequest<JwtAuthResponse>(_localizer[SharedResourcesKey.LoginTooManyFailedAttempts]);

            if (!signInResult.Succeeded)
                return BadRequest<JwtAuthResponse>(_localizer[SharedResourcesKey.LoginInvalidCredentials]);

            var accessToken = await _service.AuthenticationService.GetJwtToken(user);
            accessToken.IsFirstLogin = false;
            accessToken.UserId = user.Id;

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

            if (!user.RegistrationIsCompleted)
                accessToken.IsFirstLogin = true;

            return Success(accessToken);
        }
        catch (Exception ex)
        {
            // Security finding #2: never echo raw exception text to the caller (it can carry DB/Identity
            // internals). Log the detail server-side; return a generic localized message to the client.
            _logger.LogError(ex, "Error: in SignInCommand");
            return ServerError<JwtAuthResponse>(_localizer[SharedResourcesKey.LoginSystemError]);
        }
    }

    private SessionExtractionInfo? ExtractSessionInfoFromToken(string? accessToken)
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
