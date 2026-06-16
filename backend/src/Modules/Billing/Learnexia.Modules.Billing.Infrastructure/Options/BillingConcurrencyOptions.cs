namespace Learnexia.Modules.Billing.Infrastructure.Options;

/// <summary>
/// Tunable xmin optimistic-concurrency retry policy for Billing credit-debit operations.
///
/// <para>Bound from the <c>Billing:Concurrency</c> configuration section.
/// Consumed by <see cref="Services.CreditLedgerService"/> and
/// <see cref="Services.CreditSpendService"/>.</para>
///
/// <para>
/// Backoff formula (attempt index is 1-based; first attempt has NO delay):
/// <code>
///   delay = min(MaxDelayMs, BaseDelayMs * 2^(attempt - 1)) + Random.Shared.Next(0, JitterMs + 1)
/// </code>
/// The delay fires AFTER <c>ChangeTracker.Clear()</c> (i.e. after the failed transaction has
/// been rolled back and disposed) and BEFORE the next attempt begins — never while a DB
/// transaction or row lock is held.
/// </para>
/// </summary>
public sealed class BillingConcurrencyOptions
{
    /// <summary>Config section name — maps to <c>Billing:Concurrency</c> in appsettings.</summary>
    public const string SectionName = "Billing:Concurrency";

    /// <summary>
    /// Total number of retry attempts after the first failure.
    /// With MaxRetries = 6 the service will try at most 7 times (attempt 0..6).
    /// Default: 6.
    /// </summary>
    public int MaxRetries { get; init; } = 6;

    /// <summary>
    /// Base delay in milliseconds for exponential back-off.
    /// Delay for attempt i = min(MaxDelayMs, BaseDelayMs * 2^(i-1)) + jitter.
    /// Default: 20 ms.
    /// </summary>
    public int BaseDelayMs { get; init; } = 20;

    /// <summary>
    /// Upper-bound cap for the deterministic part of the back-off delay (ms).
    /// Prevents runaway delays under extreme contention.
    /// Default: 250 ms.
    /// </summary>
    public int MaxDelayMs { get; init; } = 250;

    /// <summary>
    /// Maximum random jitter added on top of the deterministic delay (ms).
    /// Actual jitter is <c>Random.Shared.Next(0, JitterMs + 1)</c>.
    /// Default: 50 ms.
    /// </summary>
    public int JitterMs { get; init; } = 50;
}
