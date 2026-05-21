using System.IdentityModel.Tokens.Jwt;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Commands.SignIn;

public class SignInCommandHandler : BaseResponseHandler, ICommandHandler<SignInCommand, BaseResponse<JwtAuthResponse>>
{
    private readonly SignInManager<User> _signInManager;
    private readonly IIdentityServiceManager _service;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ISessionManagementService _sessionManagementService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SignInCommandHandler(
        SignInManager<User> signInManager,
        IIdentityServiceManager service,
        IStringLocalizer<SharedResources> localizer,
        ISessionManagementService sessionManagementService,
        IHttpContextAccessor httpContextAccessor)
    {
        _signInManager = signInManager;
        _service = service;
        _localizer = localizer;
        _sessionManagementService = sessionManagementService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<BaseResponse<JwtAuthResponse>> Handle(SignInCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _service.UserManagmentService.FindByNameAsync(request.UserName);
            if (user == null)
                return NotFound<JwtAuthResponse>(_localizer[SharedResourcesKey.LoginUserNotFound]);

            if (!user.IsActive)
                return BadRequest<JwtAuthResponse>(_localizer[SharedResourcesKey.LoginAccountDeactivated]);

            var signInResult = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
            if (!signInResult.Succeeded)
                return BadRequest<JwtAuthResponse>(_localizer[SharedResourcesKey.LoginIncorrectPassword]);

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
            return ServerError<JwtAuthResponse>(ex.Message);
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
