namespace Learnexia.Modules.Billing.Domain.Enums;

/// <summary>
/// Parent-initiated pause state for a child's AI access (P10-18-BE-1).
///
/// <para>Stored as <c>int</c> via <c>HasConversion&lt;int&gt;()</c> in EF config.</para>
///
/// <para>This state is <strong>entirely independent</strong> of the billing-driven
/// <see cref="SeatState"/>. A child can be in any combination of the two states:
/// the AI spend gate checks BOTH independently.</para>
///
/// <para>Default = <see cref="Active"/> (never paused). Transitions to
/// <see cref="Paused"/> when a parent explicitly pauses the child, and back to
/// <see cref="Active"/> on unpause.</para>
/// </summary>
public enum ParentPauseState
{
    /// <summary>
    /// The child has NOT been paused by the parent. AI access is allowed
    /// (subject to the seat state also being <see cref="SeatState.Active"/>).
    /// </summary>
    Active = 0,

    /// <summary>
    /// The child has been paused by the parent. AI spend is denied with a graceful localized
    /// message. Energy balance, seat state, and billing are all untouched.
    /// The pause is reversed by the parent at any time via the Unpause endpoint.
    /// </summary>
    Paused = 1,
}
