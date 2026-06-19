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
            // P7-07: Identity.Infrastructure handlers (AccountDeletedDomainEventHandler) live in
            // Infrastructure because they depend on ISessionManagementService + IDistributedCache.
            // Without this scan those handlers are never discovered and post-commit side-effects
            // (session revocation, integration events) silently don't fire. Mirrors the pattern
            // established for Learning.Infrastructure and Curriculum.Infrastructure above.
            cfg.RegisterServicesFromAssemblyContaining<Learnexia.Modules.Identity.Infrastructure.AssemblyReference>();
            cfg.RegisterServicesFromAssemblyContaining<Learnexia.Modules.Learning.Application.AssemblyReference>();
            cfg.RegisterServicesFromAssemblyContaining<Learnexia.Modules.Parent.Application.AssemblyReference>();
            cfg.RegisterServicesFromAssemblyContaining<Learnexia.Modules.Notifications.Application.AssemblyReference>();
            cfg.RegisterServicesFromAssemblyContaining<Learnexia.Modules.Gamification.Application.AssemblyReference>();
            // CRITICAL: Moderation Application assembly MUST be registered here so that
            // AuditLogEventHandler is discovered. Without this line, AdminActionPerformedEvent
            // publishes into a void and the audit log silently stays empty with no error.
            cfg.RegisterServicesFromAssemblyContaining<Learnexia.Modules.Moderation.Application.AssemblyReference>();
            // Curriculum (P3-07): register BOTH layers. The retrieval query handler
            // (RetrieveChunksQueryHandler) lives in the Infrastructure assembly because it depends on
            // CurriculumDbContext + pgvector (Application may not reference Infrastructure), so the
            // Application-only scan used for other modules would miss it — RagContextProvider's
            // _mediator.Send(RetrieveChunksQuery) would throw "no handler" at runtime without this.
            cfg.RegisterServicesFromAssemblyContaining<Learnexia.Modules.Curriculum.Application.AssemblyReference>();
            cfg.RegisterServicesFromAssemblyContaining<Learnexia.Modules.Curriculum.Infrastructure.AssemblyReference>();
            // P3-05: Learning.Infrastructure handlers (e.g. HintUsedIntegrationEventHandler) live in
            // Infrastructure because they depend on LearningDbContext. Without this scan those handlers
            // are never discovered by MediatR and Publish(HintUsedIntegrationEvent) is a silent no-op.
            // Mirrors the Curriculum.Infrastructure scan above.
            cfg.RegisterServicesFromAssemblyContaining<Learnexia.Modules.Learning.Infrastructure.AssemblyReference>();
            // P3-04: Ai.Application handlers (ExplainConceptCommandHandler + future Hint/WhyWrong).
            cfg.RegisterServicesFromAssemblyContaining<Learnexia.Modules.Ai.Application.AssemblyReference>();
            // CRITICAL: Analytics Application assembly MUST be registered here so that
            // INotificationHandler<T> capture consumers (BE-3) are discovered. Without this line,
            // integration-event notifications publish into a void and the ActivityEvent sink stays
            // silently empty with no error — the same trap documented for Moderation above.
            cfg.RegisterServicesFromAssemblyContaining<Learnexia.Modules.Analytics.Application.AssemblyReference>();

            // Independent fan-out: every handler runs, per-handler failures are caught + logged
            // (ADR 0002 §4). Replaces MediatR's default throw-on-first-failure publisher.
            cfg.NotificationPublisherType = typeof(IsolatedNotificationPublisher);
        });

        // Domain events raised by aggregates are dispatched after commit via this seam (ADR 0002 §2/§5).
        services.AddScoped<IDomainEventDispatcher, InProcessDomainEventDispatcher>();

        return services;
    }
}
