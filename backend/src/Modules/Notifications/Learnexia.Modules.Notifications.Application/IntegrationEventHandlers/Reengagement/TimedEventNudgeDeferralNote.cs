// P9-05-BE-4: TimedEventStartedIntegrationEvent and TimedEventEndedIntegrationEvent handlers
// are intentionally NOT built in this wave (P9-05).
//
// Reason: both events are platform-wide broadcasts carrying TimedEventId / Code / Scope / Multiplier
// but NO StudentId. A per-child nudge requires a recipient — there is no "active student list"
// seam in the Notifications module (no IActiveStudentQuery or equivalent in Shared.Contracts).
//
// Deferred to a follow-up story: needs a new cross-module "active student ids" seam +
// a budget-aware bulk dispatch path that respects the P9-07 per-child daily push budget for every
// recipient. Materially larger than "light up a handler" — it is a separate scoped story.
//
// Backlog tracked in HANDOFF.md. Do not remove this file — it is the in-code deferral record
// that prevents future contributors from thinking the handlers were missed by accident.

namespace Learnexia.Modules.Notifications.Application.IntegrationEventHandlers.Reengagement;
