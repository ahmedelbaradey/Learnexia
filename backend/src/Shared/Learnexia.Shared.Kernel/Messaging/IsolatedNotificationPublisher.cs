using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Shared.Kernel.Messaging;

/// <summary>
/// MediatR notification publishing strategy that runs each handler INDEPENDENTLY (ADR 0002 §4).
///
/// The default MediatR publisher runs handlers sequentially and throws on the first handler exception,
/// aborting the remaining handlers and the originating request. That violates the P4-01 acceptance
/// criterion "a failing handler does not block other handlers or the originating request".
///
/// This publisher invokes every handler, catches each handler's exception in isolation, logs it via
/// <see cref="ILoggerManager"/> (failures are logged, never silently swallowed), and lets the remaining
/// handlers run to completion.
/// </summary>
public sealed class IsolatedNotificationPublisher : INotificationPublisher
{
    private readonly ILoggerManager _logger;

    public IsolatedNotificationPublisher(ILoggerManager logger)
    {
        _logger = logger;
    }

    public async Task Publish(
        IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification,
        CancellationToken cancellationToken)
    {
        foreach (var handler in handlerExecutors)
        {
            try
            {
                await handler.HandlerCallback(notification, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    $"Error: notification handler '{handler.HandlerInstance.GetType().Name}' " +
                    $"failed for '{notification.GetType().Name}'. Other handlers continue.");
            }
        }
    }
}
