namespace Learnexia.Shared.Contracts.Identity;

// Carries only UserId + UserName (consumer uses UserName for the welcome message). Email was removed
// (security audit P4-01 #4): it was unused by every consumer, and broadcasting PII across the module
// boundary widens the exposure surface. Consumers needing email should query Identity directly.
public sealed record UserRegisteredIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int UserId,
    string UserName) : IIntegrationEvent;
