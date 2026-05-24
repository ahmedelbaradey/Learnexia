using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.DomainEvents;
using MediatR;

namespace Learnexia.Host.Extensions;

/// <summary>
/// Unified, cross-module MediatR registration (ADR 0002 §4, P4-01-BE-4).
///
/// MediatR was previously registered per module (each module's <c>AddXApplication</c> called
/// <c>AddMediatR</c> over its own assembly only). Because <c>AddMediatR</c> re-registers
/// <see cref="IMediator"/>/<see cref="ISender"/>/<see cref="IPublisher"/> each time, only the LAST
/// module's scan effectively won — a <c>Publish</c> from one module would NOT reach handlers in another
/// (the R1 cross-module fan-out blocker, FR-GM-7).
///
/// This registers MediatR ONCE here, scanning EVERY module's Application assembly together, and wires the
/// <see cref="IsolatedNotificationPublisher"/> so one failing notification handler does not abort its
/// siblings. Each module keeps its own validators / AutoMapper / <c>ValidationBehavior</c> registration.
/// </summary>
public static class MediatRExtensions
{
    public static IServiceCollection AddCrossModuleMediatR(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            // All module Application assemblies in one configuration so handlers (and notification
            // handlers) are discoverable across module boundaries. Add new modules' Application
            // assemblies here (e.g. Gamification) as they come online.
            cfg.RegisterServicesFromAssemblyContaining<Learnexia.Modules.Identity.Application.AssemblyReference>();
            cfg.RegisterServicesFromAssemblyContaining<Learnexia.Modules.Catalog.Application.AssemblyReference>();
            cfg.RegisterServicesFromAssemblyContaining<Learnexia.Modules.Learning.Application.AssemblyReference>();
            cfg.RegisterServicesFromAssemblyContaining<Learnexia.Modules.Parent.Application.AssemblyReference>();
            cfg.RegisterServicesFromAssemblyContaining<Learnexia.Modules.Notifications.Application.AssemblyReference>();

            // Independent fan-out: every handler runs, per-handler failures are caught + logged
            // (ADR 0002 §4). Replaces MediatR's default throw-on-first-failure publisher.
            cfg.NotificationPublisherType = typeof(IsolatedNotificationPublisher);
        });

        // Domain events raised by aggregates are dispatched after commit via this seam (ADR 0002 §2/§5).
        services.AddScoped<IDomainEventDispatcher, InProcessDomainEventDispatcher>();

        return services;
    }
}
