using Learnexia.Modules.Billing.Api.Bases;
using Learnexia.Modules.Billing.Application.Features.Payments.Commands.HandleProviderWebhook;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Billing.Api.Controllers;

/// <summary>
/// Receives incoming payment provider webhooks.
///
/// <para><strong>SECURITY:</strong>
/// <list type="bullet">
///   <item>This endpoint is <strong>intentionally NOT protected by JWT</strong> — payment providers
///         cannot carry a Bearer token. Authentication is provided exclusively by HMAC
///         signature verification in <c>HandleProviderWebhookCommandHandler</c>.</item>
///   <item>The raw request body is read <em>before</em> any middleware can consume/transform it,
///         and the bytes are passed directly to the handler for signature verification.
///         Never parse the body before verifying the signature.</item>
///   <item>The endpoint returns a 200 OK for all processed events (including duplicates and
///         unhandled types) so the provider does not retry unnecessarily. It returns 401 only
///         on signature failure — with a generic message that reveals nothing about the cause.</item>
/// </list>
/// </para>
/// </summary>
[Route("api/Billing/Webhooks")]
[ApiController]
public class WebhookController : AppControllerBase
{
    /// <summary>
    /// Handles an incoming event from the payment provider.
    ///
    /// <para>No <c>[Authorize]</c> — provider-authenticated by HMAC signature.
    /// Raw body is captured before any middleware reads it so the HMAC is computed
    /// over the original bytes.</para>
    /// </summary>
    [HttpPost("Provider")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> ProviderWebhook(CancellationToken cancellationToken)
    {
        // Enable buffering so we can read the body as raw bytes without consuming the stream.
        Request.EnableBuffering();

        byte[] rawBody;
        using (var ms = new MemoryStream())
        {
            await Request.Body.CopyToAsync(ms, cancellationToken);
            rawBody = ms.ToArray();
        }

        // Reset position in case middleware needs to re-read (belt + suspenders).
        Request.Body.Position = 0;

        // Read the provider-specific signature header.
        // For FakePaymentProvider the header is "X-Hmac-Signature".
        // For Paymob it will differ — the real adapter will handle its own header name.
        var signatureHeader = Request.Headers["X-Hmac-Signature"].FirstOrDefault();

        var command = new HandleProviderWebhookCommand
        {
            RawBody         = rawBody,
            SignatureHeader = signatureHeader,
        };

        return NewResult(await Mediator.Send(command, cancellationToken));
    }
}
