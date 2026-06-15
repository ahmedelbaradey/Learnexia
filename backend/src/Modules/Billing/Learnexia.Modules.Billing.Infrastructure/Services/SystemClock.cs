using Learnexia.Shared.Kernel.Abstractions;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Production implementation of <see cref="ISystemClock"/>: returns <see cref="DateTime.UtcNow"/>.
/// Mirrors <c>Gamification.Infrastructure.Services.SystemClock</c>.
/// </summary>
public sealed class SystemClock : ISystemClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
