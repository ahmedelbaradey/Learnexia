using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Features.Lifecycle.Dtos;

/// <summary>
/// Admin preview of a curriculum entity in its current (possibly Draft) state.
/// Returned by <c>GetPreviewQuery</c>. AdminOnly.
///
/// The snapshot is the full serialized entity state rendered as an admin would see it.
/// This is intentionally the raw JSON representation; the FE contract (P7-05-FE) will
/// define the final shape once the frontend batch is dispatched.
/// </summary>
public class PreviewDto
{
    public VersionedEntityType EntityType { get; set; }
    public int EntityId { get; set; }

    /// <summary>
    /// Current LifecycleState of the entity (Draft/Published/Archived).
    /// Allows the admin UI to display the state alongside the preview.
    /// </summary>
    public LifecycleState CurrentState { get; set; }

    /// <summary>Language of the owning Subject tree.</summary>
    public ContentLanguage Language { get; set; }

    /// <summary>
    /// Full serialized current state of the entity (JSON).
    /// Lifted directly from the live entity — not from a historical ContentVersion.
    /// </summary>
    public string Snapshot { get; set; } = null!;
}
