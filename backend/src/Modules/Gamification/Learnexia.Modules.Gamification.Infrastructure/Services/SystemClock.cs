using Learnexia.Shared.Kernel.Abstractions;

namespace Learnexia.Modules.Gamification.Infrastructure.Services;

/// <summary>
/// Production implementation of <see cref="ISystemClock"/> — wraps <see cref="DateTime.UtcNow"/>.
/// Registered as Singleton so the clock is shared across all scopes (stateless — no DI penalty).
/// </summary>
public sealed class SystemClock : ISystemClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
