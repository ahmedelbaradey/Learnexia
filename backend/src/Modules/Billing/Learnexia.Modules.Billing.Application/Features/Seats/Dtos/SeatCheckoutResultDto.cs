namespace Learnexia.Modules.Billing.Application.Features.Seats.Dtos;

/// <summary>
/// Response data returned by <c>StartSeatCheckoutCommand</c> (P10-14-BE-6).
/// The redirect URL is the web-hosted checkout page to send the parent to.
/// <para><c>Amount</c> is the server-computed, prorated EGP charge for a mid-cycle add
/// (<c>SeatPrice × quantity × remaining-cycle-ratio</c>) so the parent UI can display the
/// exact amount before redirecting — never client-supplied.</para>
/// </summary>
public sealed record SeatCheckoutResultDto(
    int     PaymentId,
    string  RedirectUrl,
    string  ProviderPaymentRef,
    decimal Amount);
