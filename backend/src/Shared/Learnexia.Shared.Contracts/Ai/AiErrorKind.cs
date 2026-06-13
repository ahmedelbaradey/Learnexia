namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// Discriminates the failure kind in <see cref="AiError"/>. Callers switch on this to decide
/// whether to retry, degrade gracefully, or surface a user-friendly message.
/// </summary>
public enum AiErrorKind
{
    /// <summary>Provider returned HTTP 4xx (bad request, auth failure, etc.).</summary>
    BadRequest,

    /// <summary>Provider rate-limited the request (HTTP 429); retry with backoff.</summary>
    RateLimited,

    /// <summary>Provider returned HTTP 5xx or was otherwise unreachable.</summary>
    Unavailable,

    /// <summary>
    /// The gateway's hard timeout (AiGatewayOptions.TimeoutSeconds) elapsed before the
    /// provider responded.
    /// </summary>
    Timeout,

    /// <summary>The student's daily AI request quota is exhausted.</summary>
    QuotaExhausted,

    /// <summary>An unexpected internal error inside the gateway itself.</summary>
    InternalError,
}
