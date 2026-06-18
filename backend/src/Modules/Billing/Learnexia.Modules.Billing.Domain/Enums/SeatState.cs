namespace Learnexia.Modules.Billing.Domain.Enums;

/// <summary>
/// Lifecycle state of a child's seat entitlement (P10-15-BE-1).
///
/// <para>Stored as <c>int</c> via <c>HasConversion&lt;int&gt;()</c> in EF config.</para>
///
/// <para>Enforcement (P10-15-BE-4) moves a child from <see cref="Active"/> to
/// <see cref="NoSeatLocked"/> when the paid-active-seat count drops below the number of
/// seated children at renewal or payment-failure grace expiry. No mid-cycle forfeit occurs;
/// already-ledgered energy stays valid.</para>
/// </summary>
public enum SeatState
{
    /// <summary>
    /// The child holds an active paid seat. Energy entitlement allocation row exists;
    /// AI spend is allowed.
    /// </summary>
    Active = 0,

    /// <summary>
    /// The child has no active seat (enforcement locked this seat). The child's allocation
    /// row has been removed (no future entitlement), but already-credited energy is preserved.
    /// AI spend is denied with a graceful localized message. The child keeps all other
    /// progress/XP/history/achievements.
    /// </summary>
    NoSeatLocked = 1,
}
