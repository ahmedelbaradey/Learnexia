namespace Learnexia.Shared.Contracts.Catalog;

public sealed record ProductPublishedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid ProductId,
    string Name) : IIntegrationEvent;
