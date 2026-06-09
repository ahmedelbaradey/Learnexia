using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Gamification.Domain.Entities;

/// <summary>
/// Catalog row for a time-bounded XP multiplier event (e.g., "Weekend XP Boost", "Ramadan League Week").
///
/// Rows are created via <c>TimedEventSeeder</c> / admin (P7). The sweep job
/// (<c>TimedEventSweepJob</c>) sets <see cref="IsActive"/> when the event enters its active
/// window and clears it when the window closes, raising the corresponding domain events.
///
/// <see cref="Multiplier"/> must be in [1.00 .. 5.00] — enforced by the factory and backed
/// by a DB check constraint (defence in depth). A multiplier of 1.00 is a no-op boost but
/// is allowed so ops can schedule a future event without removing a row.
///
/// <see cref="Scope"/> declares which XP reason(s) receive the boost. In Phase 3 only
/// <see cref="TimedEventScope.AllXp"/> is honored by <c>IXpBoostCalculator</c>;
/// <c>MissionXp</c> and <c>LeagueXp</c> are forward-compat.
///
/// Derives from <see cref="FullAuditedEntity"/> (audit trail for ops edits via P7 admin).
/// No cross-module FK constraints (module isolation rule 1).
/// </summary>
public class TimedEvent : AggregateRoot
{
    /// <summary>
    /// Stable, unique event code — e.g. "DOUBLE_XP_WEEKEND", "RAMADAN_LEAGUE_BOOST".
    /// Max 64 chars. Used as idempotency key by the seeder and as a correlation key in
    /// domain events.
    /// </summary>
    public string Code { get; private set; } = null!;

    /// <summary>English display name shown on the dashboard banner. Max 128 chars.</summary>
    public string NameEn { get; private set; } = null!;

    /// <summary>Arabic display name shown on the dashboard banner. Max 128 chars.</summary>
    public string NameAr { get; private set; } = null!;

    /// <summary>Optional English description / subtitle. Max 512 chars. Nullable.</summary>
    public string? DescriptionEn { get; private set; }

    /// <summary>Optional Arabic description / subtitle. Max 512 chars. Nullable.</summary>
    public string? DescriptionAr { get; private set; }

    /// <summary>UTC timestamp when the event window opens.</summary>
    public DateTime StartUtc { get; private set; }

    /// <summary>
    /// UTC timestamp when the event window closes. Must be strictly after <see cref="StartUtc"/>
    /// (enforced by the factory and by a DB check constraint).
    /// </summary>
    public DateTime EndUtc { get; private set; }

    /// <summary>
    /// XP multiplier applied within the event window. Precision 4,2 (e.g., 2.00, 1.50, 3.00).
    /// Range [1.00 .. 5.00] — enforced by the factory and backed by a DB check constraint.
    /// </summary>
    public decimal Multiplier { get; private set; }

    /// <summary>
    /// Which XP reason(s) this multiplier applies to.
    /// Stored as int. Phase 3 MVP only honours <see cref="TimedEventScope.AllXp"/>.
    /// </summary>
    public TimedEventScope Scope { get; private set; }

    /// <summary>
    /// Denormalized active-window flag. Set <c>true</c> by <c>TimedEventSweepJob</c> when the
    /// event transitions into its window; cleared when the window ends. Default <c>false</c>.
    /// Acts as the sweep job's idempotency latch — prevents duplicate domain events.
    /// </summary>
    public bool IsActive { get; private set; } = false;

    /// <summary>UTC timestamp when this catalog row was created. Set on insert.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    // ---------------------------------------------------------------------------
    // EF parameterless constructor
    // ---------------------------------------------------------------------------

    private TimedEvent() { }

    // ---------------------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Creates a new <see cref="TimedEvent"/> catalog row. Validates domain invariants:
    /// <list type="bullet">
    ///   <item><paramref name="startUtc"/> must be strictly before <paramref name="endUtc"/>.</item>
    ///   <item><paramref name="multiplier"/> must be in [1.00 .. 5.00].</item>
    /// </list>
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when invariants are violated.</exception>
    public static TimedEvent Create(
        string code,
        string nameEn,
        string nameAr,
        string? descriptionEn,
        string? descriptionAr,
        DateTime startUtc,
        DateTime endUtc,
        decimal multiplier,
        TimedEventScope scope)
    {
        if (startUtc >= endUtc)
            throw new ArgumentException("StartUtc must be strictly before EndUtc.", nameof(startUtc));

        if (multiplier < 1.0m || multiplier > 5.0m)
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Multiplier must be in [1.00 .. 5.00].");

        return new TimedEvent
        {
            Code = code,
            NameEn = nameEn,
            NameAr = nameAr,
            DescriptionEn = descriptionEn,
            DescriptionAr = descriptionAr,
            StartUtc = startUtc,
            EndUtc = endUtc,
            Multiplier = multiplier,
            Scope = scope,
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    // ---------------------------------------------------------------------------
    // Mutation — sweep job only
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Marks the event as active (called by <c>TimedEventSweepJob</c> on window-enter transition).
    /// No-op if already active (idempotent).
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    /// Marks the event as inactive (called by <c>TimedEventSweepJob</c> on window-exit transition).
    /// No-op if already inactive (idempotent).
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    // ---------------------------------------------------------------------------
    // Mutation — P7-13 admin write surface
    // ---------------------------------------------------------------------------

    /// <summary>
    /// P7-13: Admin update — replaces all mutable fields (copy, window, multiplier, scope).
    /// Re-applies the same invariants as <see cref="Create"/>: <paramref name="startUtc"/> must be
    /// strictly before <paramref name="endUtc"/> and <paramref name="multiplier"/> must be in [1.00 .. 5.00].
    /// <see cref="Code"/> is immutable. <see cref="IsActive"/> is NOT changed here — use
    /// <see cref="Activate"/>/<see cref="Deactivate"/> for state transitions.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when invariants are violated.</exception>
    public void AdminUpdate(
        string nameEn,
        string nameAr,
        string? descriptionEn,
        string? descriptionAr,
        DateTime startUtc,
        DateTime endUtc,
        decimal multiplier,
        TimedEventScope scope)
    {
        if (startUtc >= endUtc)
            throw new ArgumentException("StartUtc must be strictly before EndUtc.", nameof(startUtc));

        if (multiplier < 1.0m || multiplier > 5.0m)
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Multiplier must be in [1.00 .. 5.00].");

        NameEn = nameEn;
        NameAr = nameAr;
        DescriptionEn = descriptionEn;
        DescriptionAr = descriptionAr;
        StartUtc = startUtc;
        EndUtc = endUtc;
        Multiplier = multiplier;
        Scope = scope;
    }
}
