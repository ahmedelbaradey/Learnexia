using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Commands.ForgotPassword;

// P1-12 BE-6. Anti-enumeration by construction: EVERY path returns the SAME localized generic success
// (ForgotPasswordGenericResponse), whether the email is unknown, belongs to an inactive account, or maps
// to a real user. The only side effect that differs is whether a reset event is published, which is not
// observable by the caller. Email delivery is cross-module via a Shared.Contracts integration event
// (PasswordResetRequestedIntegrationEvent) consumed by the Notifications module — Identity never
// references Notifications. The reset token is embedded in the URL and is never logged.
//
// P6-06 BE-1: the reset-email dispatch is now out-of-band (Task.Run + fresh IServiceScope) so the
// registered-email and unknown-email paths return in statistically indistinguishable time. The token and
// reset URL are minted BEFORE the background dispatch (cheap, in-process HMAC — not SMTP I/O) so the
// token is never lost regardless of background-task scheduling. The fresh scope avoids the
// ObjectDisposedException trap (AI-cache fire-and-forget documented pattern). The handler's catch-all
// still returns the same generic 200 on any synchronous failure.
//
// P6-06 BE-2: Locale (user.PreferredLanguage) is embedded in the event so the Notifications consumer
// can render the reset email in the recipient's own language without a second IUserLookup call.
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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;

    public ForgotPasswordCommandHandler(
        IIdentityServiceManager service,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _service = service;
        _localizer = localizer;
        _logger = logger;
        _scopeFactory = scopeFactory;
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

            // P6-06 BE-1: mint token + build event INLINE (cheap: in-process HMAC, NOT SMTP I/O),
            // then dispatch the publish out-of-band so this request returns without awaiting email delivery.
            await BuildAndDispatchPasswordResetEventAsync(user);
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

    // P6-06 BE-1: mint the token + build the event on the calling request (cheap: in-process HMAC, not
    // SMTP I/O), then fire the IPublisher.Publish call in a background Task with a FRESH IServiceScope.
    // Using a fresh scope (not the request scope) prevents ObjectDisposedException when the request scope
    // is disposed before the background task's await completes — the same documented fix as the AI-cache
    // fire-and-forget (GetHintCommandHandler / ExplainConceptCommandHandler). The built event is captured
    // in the closure before Task.Run; it is never lost even if background scheduling is delayed.
    private async Task BuildAndDispatchPasswordResetEventAsync(User user)
    {
        try
        {
            // Token mint is in-process (HMAC / data-protection): awaited inline on the request thread so the
            // token is ready before Task.Run. This is the ONLY async I/O on the hot path for a real user;
            // it completes in microseconds and does NOT touch the network.
            var token = await _service.UserManagmentService.GeneratePasswordResetTokenAsync(user);
            var resetUrl = BuildResetUrl(user.Email!, token);

            // P6-06 BE-2: embed Locale (PreferredLanguage resolved at emit time — user is in hand here,
            // no extra lookup required) so the Notifications consumer can render the localized email.
            var integrationEvent = new PasswordResetRequestedIntegrationEvent(
                EventId: Guid.NewGuid(),
                OccurredOnUtc: DateTime.UtcNow,
                Email: user.Email!,
                ResetUrl: resetUrl,
                UserName: user.FullName ?? user.UserName,
                Locale: user.PreferredLanguage);

            // Dispatch publish out-of-band: fresh scope so the request scope's disposal does not
            // cancel/dispose the IPublisher mid-send (mirrors the AI-cache fire-and-forget pattern in
            // GetHintCommandHandler and ExplainConceptCommandHandler). Request returns immediately after
            // Task.Run is scheduled; the background body awaits SMTP delivery inside the handler chain.
            _ = Task.Run(async () =>
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
                    // Never log the token or the reset URL (it carries the single-use token).
                    _logger.LogInfo($"ForgotPassword: publishing PasswordResetRequestedIntegrationEvent for user {user.Id}.");
                    await publisher.Publish(integrationEvent, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    // Fail-soft: a publish or email-send failure must never surface to the caller.
                    // The request has already returned the generic 200 at this point.
                    _logger.LogError(ex, $"ForgotPassword: out-of-band publish failed for user {user.Id}; isolated.");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"ForgotPassword: token mint or event build failed for user {user.Id}.");
            // Do not re-throw — the outer catch-all returns the generic success either way.
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
