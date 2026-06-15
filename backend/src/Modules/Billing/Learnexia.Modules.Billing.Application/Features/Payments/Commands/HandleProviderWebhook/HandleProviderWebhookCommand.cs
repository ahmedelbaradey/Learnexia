using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Payments.Commands.HandleProviderWebhook;

/// <summary>
/// Command issued by the JWT-free webhook endpoint once the raw body has been
/// captured (before ASP.NET reads it again).
///
/// <para>SECURITY: the endpoint MUST pass the raw body bytes so the handler can
/// call <c>IPaymentProvider.VerifyWebhookSignature</c> before any state mutation.
/// The handler never trusts the parsed data until the signature is verified.</para>
/// </summary>
public sealed record HandleProviderWebhookCommand : ICommand<BaseResponse<WebhookHandledDto>>
{
    /// <summary>Raw HTTP request body bytes — used for HMAC signature verification.</summary>
    public byte[] RawBody { get; init; } = Array.Empty<byte>();

    /// <summary>
    /// Signature value from the provider-specific HTTP header
    /// (e.g. <c>X-Hmac-Signature</c> for the Fake provider).
    /// </summary>
    public string? SignatureHeader { get; init; }
}

/// <summary>Result DTO returned after processing a provider webhook.</summary>
/// <param name="EventId">The idempotency event id that was processed (or deduplicated).</param>
/// <param name="Outcome">Human-readable outcome for logging/audit (never leaked to untrusted callers).</param>
public sealed record WebhookHandledDto(string EventId, string Outcome);
