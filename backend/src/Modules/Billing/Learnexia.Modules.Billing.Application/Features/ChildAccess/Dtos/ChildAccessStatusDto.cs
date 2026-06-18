using Learnexia.Modules.Billing.Domain.Enums;

namespace Learnexia.Modules.Billing.Application.Features.ChildAccess.Dtos;

/// <summary>
/// Per-child access status DTO (P10-18-BE-6).
///
/// <para>Combines the billing-driven <see cref="SeatState"/> with the parent-controlled
/// <see cref="ParentPauseState"/>. The <see cref="IsAccessAllowed"/> flag reflects both:
/// <c>true</c> only when both states are Active.</para>
/// </summary>
public sealed record ChildAccessStatusDto
{
    /// <summary>The child user id.</summary>
    public int ChildId { get; init; }

    /// <summary>Billing-driven seat lifecycle state (Active / NoSeatLocked).</summary>
    public SeatState SeatState { get; init; }

    /// <summary>Parent-initiated pause state (Active / Paused).</summary>
    public ParentPauseState ParentPauseState { get; init; }

    /// <summary>
    /// Combined access flag: <see langword="true"/> ONLY when
    /// <see cref="SeatState"/> == Active AND <see cref="ParentPauseState"/> == Active.
    /// Either condition alone returns <see langword="false"/>.
    /// </summary>
    public bool IsAccessAllowed { get; init; }
}
