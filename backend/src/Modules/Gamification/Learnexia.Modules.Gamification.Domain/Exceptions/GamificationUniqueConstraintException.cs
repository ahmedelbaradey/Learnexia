namespace Learnexia.Modules.Gamification.Domain.Exceptions;

/// <summary>
/// Raised by <c>UnitOfWorkBehavior</c> (Infrastructure) when a database unique-constraint violation
/// occurs during <c>SaveChangesAsync</c>. By re-throwing as this domain-neutral exception, the
/// Infrastructure layer absorbs the EF-specific <c>DbUpdateException</c> so that Application handlers
/// can implement idempotency handling without referencing <c>Microsoft.EntityFrameworkCore</c>.
///
/// The <see cref="ConstraintHint"/> carries the original exception message (constraint name / SQL state)
/// so handlers can narrow their catch to the specific idempotency unique index they expect.
/// </summary>
public sealed class GamificationUniqueConstraintException : Exception
{
    /// <summary>
    /// Raw hint text sourced from the original <c>DbUpdateException.InnerException.Message</c>
    /// (contains the PostgreSQL constraint name and/or SQL state 23505).
    /// Application handlers use <see cref="string.Contains"/> on this to verify the violation
    /// is the expected idempotency index before treating it as a successful no-op.
    /// </summary>
    public string ConstraintHint { get; }

    public GamificationUniqueConstraintException(string constraintHint, Exception innerException)
        : base("Unique constraint violation during SaveChanges.", innerException)
    {
        ConstraintHint = constraintHint ?? string.Empty;
    }
}
