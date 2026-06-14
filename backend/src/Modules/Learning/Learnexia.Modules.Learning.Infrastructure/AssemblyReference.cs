namespace Learnexia.Modules.Learning.Infrastructure;

/// <summary>
/// Marker type for assembly scanning. Used by the Host's cross-module MediatR registration so
/// integration-event handlers that live in the Infrastructure layer (e.g.
/// <c>HintUsedIntegrationEventHandler</c>, which depends on <c>LearningDbContext</c> and therefore
/// cannot live in Application) are discoverable across module boundaries.
/// Mirrors <c>Learnexia.Modules.Curriculum.Infrastructure.AssemblyReference</c>.
/// </summary>
public class AssemblyReference
{
}
