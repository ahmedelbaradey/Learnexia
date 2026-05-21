namespace Learnexia.Shared.Contracts.Identity;

public sealed record UserRegisteredIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int UserId,
    string Email,
    string UserName) : IIntegrationEvent;
