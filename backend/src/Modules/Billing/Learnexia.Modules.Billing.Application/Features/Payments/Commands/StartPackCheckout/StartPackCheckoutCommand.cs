using Learnexia.Modules.Billing.Application.Features.Payments.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Payments.Commands.StartPackCheckout;

/// <summary>
/// Command: a parent initiates a one-off energy-pack purchase for a specific child.
///
/// <para>SECURITY:
/// <list type="bullet">
///   <item><see cref="ParentUserId"/> is set by the handler from <c>ICurrentUserService.UserId</c> (JWT),
///         never from the request body.</item>
///   <item><see cref="ChildId"/> is validated against the parent's family via
///         <c>IEnergyPackService.StartPackCheckoutAsync</c> (IDOR guard).</item>
///   <item>The pack price and size are resolved server-side from <c>IGlobalSettingsProvider</c>
///         inside the service; the client never supplies an amount.</item>
/// </list>
/// </para>
/// </summary>
public sealed record StartPackCheckoutCommand : ICommand<BaseResponse<PackCheckoutResultDto>>
{
    /// <summary>
    /// Owning parent's user id. Set by the handler from <c>ICurrentUserService.UserId</c> — NEVER
    /// from the HTTP request body. Validated by <see cref="StartPackCheckoutValidator"/>.
    /// </summary>
    public int ParentUserId { get; init; }

    /// <summary>Target child to receive the energy credits once payment succeeds.</summary>
    public int ChildId { get; init; }
}
