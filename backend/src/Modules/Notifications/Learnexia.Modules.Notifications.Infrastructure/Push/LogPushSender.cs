using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;

namespace Learnexia.Modules.Notifications.Infrastructure.Push;

/// <summary>
/// No-op push sender for local development (P4-09 B4-2).
/// Registered when <c>Notifications:Push:Provider = "None"</c> (the default in dev).
/// Logs the push attempt without contacting any external service.
/// Mirrors <c>LogEmailSender</c> in the Email folder.
/// </summary>
public sealed class LogPushSender : IPushSender
{
    private readonly ILoggerManager _logger;

    public LogPushSender(ILoggerManager logger)
    {
        _logger = logger;
    }

    public Task<PushSendResult> SendAsync(
        IReadOnlyList<string> expoPushTokens,
        string title,
        string body,
        string? dataJson,
        CancellationToken ct = default)
    {
        // F-08: do not log title — it may contain child behavioural data.
        _logger.LogInfo($"[LogPushSender] push skipped (dev sink): tokens={expoPushTokens.Count}");

        return Task.FromResult(new PushSendResult(Sent: 0, Failed: 0, InvalidTokens: []));
    }
}
