namespace Learnexia.Shared.Kernel.Abstractions;

/// <summary>
/// Seam over <see cref="DateTime.UtcNow"/> for deterministic testing.
/// Inject into any component that needs the current UTC time — never call DateTime.UtcNow directly
/// in testable code. Default implementation: <c>SystemClock</c>.
/// </summary>
public interface ISystemClock
{
    /// <summary>Returns the current UTC date and time.</summary>
    DateTime UtcNow { get; }
}
