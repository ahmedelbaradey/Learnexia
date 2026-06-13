namespace Learnexia.Modules.Curriculum.Infrastructure;

/// <summary>
/// Marker type for assembly scanning. Used by the Host's cross-module MediatR registration so the
/// Curriculum query handlers that live in the Infrastructure layer (e.g. <c>RetrieveChunksQueryHandler</c>,
/// which depends on <c>CurriculumDbContext</c> + pgvector and therefore cannot live in Application)
/// are discoverable across module boundaries.
/// </summary>
public class AssemblyReference
{
}
