using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Features.Lifecycle.Dtos;

/// <summary>
/// Admin-facing representation of a single publish-time version snapshot.
/// Returned by <c>GetVersionHistoryQuery</c>.
/// </summary>
public class ContentVersionDto
{
    public int Id { get; set; }

    /// <summary>Which curriculum entity type this snapshot belongs to.</summary>
    public VersionedEntityType EntityType { get; set; }

    /// <summary>PK of the versioned entity.</summary>
    public int EntityId { get; set; }

    /// <summary>Monotonically increasing version number within (EntityType, EntityId).</summary>
    public int VersionNumber { get; set; }

    /// <summary>UTC timestamp of the publish event.</summary>
    public DateTime PublishedAtUtc { get; set; }

    /// <summary>Admin user id who triggered the publish (from JWT, server-stamped).</summary>
    public int PublishedByUserId { get; set; }

    /// <summary>Language of the owning Subject tree at publish time.</summary>
    public ContentLanguage Language { get; set; }

    /// <summary>
    /// Full serialized state of the entity at the moment of publish (jsonb).
    /// Returned to admin; never returned to students.
    /// </summary>
    public string Snapshot { get; set; } = null!;
}
