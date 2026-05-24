using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Commands.ForgotPassword;

// P1-12 BE-6. Anti-enumeration by construction: EVERY path returns the SAME localized generic success
// (ForgotPasswordGenericResponse), whether the email is unknown, belongs to an inactive account, or maps
// to a real user. The only side effect that differs is whether a reset event is published, which is not
// observable by the caller. Email delivery is cross-module via a Shared.Contracts integration event
// (PasswordResetRequestedIntegrationEvent) consumed by the Notifications module — Identity never
// references Notifications. The reset token is embedded in the URL and is never logged.
public class ForgotPasswordCommandHandler : BaseResponseHandler, ICommandHandler<ForgotPasswordCommand, BaseResponse<string>>
{
    // Config key for the client app origin used to build the reset link. Technical identifier, not user text.
    private const string ClientAppBaseUrlKey = "ClientAppBaseUrl";
    // Fallback origin if the key is unset (dev). Technical identifier (a URL), not user-facing copy.
    private const string DefaultClientAppBaseUrl = "http://localhost:3000";
    // Client route that handles the reset. Technical route template, not user-facing copy.
    private const string ResetPasswordRoute = "/reset-password";

    private readonly IIdentityServiceManager _service;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;
    private readonly IPublisher _publisher;
    private readonly IConfiguration _configuration;

    public ForgotPasswordCommandHandler(
        IIdentityServiceManager service,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger,
        IPublisher publisher,
        IConfiguration configuration)
    {
        _service = service;
        _localizer = localizer;
        _logger = logger;
        _publisher = publisher;
        _configuration = configuration;
    }

    public async Task<BaseResponse<string>> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        // Build the generic response once; it is returned identically on every code path below.
        var genericResponse = Success<string>(_localizer[SharedResourcesKey.ForgotPasswordGenericResponse]);

        try
        {
            var user = await _service.UserManagmentService.FindByEmailAsync(request.Email);

            // Unknown email OR inactive account: silently succeed (no enumeration, no email). We deliberately
            // do not branch the response — only whether an event is published changes.
            if (user is null || !user.IsActive)
            {
                _logger.LogInfo("ForgotPassword: no eligible account for the supplied email; returning generic success.");
                return genericResponse;
            }

            await PublishPasswordResetRequestedEventAsync(user, cancellationToken);
            return genericResponse;
        }
        catch (Exception ex)
        {
            // Never surface internals to an anonymous caller, and never turn an error into an enumeration
            // oracle: log server-side and still return the SAME generic success.
            _logger.LogError(ex, "Error: in ForgotPasswordCommand");
            return genericResponse;
        }
    }

    private async Task PublishPasswordResetRequestedEventAsync(User user, CancellationToken cancellationToken)
    {
        try
        {
            var token = await _service.UserManagmentService.GeneratePasswordResetTokenAsync(user);
            var resetUrl = BuildResetUrl(user.Email!, token);

            var integrationEvent = new PasswordResetRequestedIntegrationEvent(
                EventId: Guid.NewGuid(),
                OccurredOnUtc: DateTime.UtcNow,
                Email: user.Email!,
                ResetUrl: resetUrl,
                UserName: user.FullName ?? user.UserName);

            await _publisher.Publish(integrationEvent, cancellationToken);

            // Never log the token or the full URL (it carries the token). Log only the user id.
            _logger.LogInfo($"Published PasswordResetRequestedIntegrationEvent for user {user.Id}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to publish PasswordResetRequestedIntegrationEvent for user {user.Id}.");
        }
    }

    private string BuildResetUrl(string email, string token)
    {
        var baseUrl = _configuration[ClientAppBaseUrlKey];
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = DefaultClientAppBaseUrl;

        baseUrl = baseUrl.TrimEnd('/');

        var encodedEmail = Uri.EscapeDataString(email);
        var encodedToken = Uri.EscapeDataString(token);

        return $"{baseUrl}{ResetPasswordRoute}?email={encodedEmail}&token={encodedToken}";
    }
}
