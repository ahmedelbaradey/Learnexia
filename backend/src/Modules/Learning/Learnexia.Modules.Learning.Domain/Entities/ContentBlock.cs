using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Learning.Domain.Entities;

/// <summary>
/// An ordered content block within a <see cref="Lesson"/>. ContentBlock *—1 Lesson.
///
/// P7-02: Represents one typed unit of lesson content (text paragraph, image, video,
/// callout). A lesson is composed of an ordered list of these blocks; the student renderer
/// reads them in <see cref="SequenceOrder"/> ascending.
///
/// Derives from <see cref="FullAuditedEntity"/> (not <see cref="AggregateRoot"/>) because
/// content blocks have no independent lifecycle — they exist only as children of their
/// owning <see cref="Lesson"/> and do not raise domain events themselves.
///
/// Soft-delete (<c>IsDeleted</c> + <c>DeletedAt</c>) is inherited from
/// <see cref="FullAuditedEntity"/>. <see cref="IsActive"/> mirrors the Subject/Unit/Lesson
/// pattern for admin-controlled visibility.
///
/// <see cref="Payload"/> is a <c>jsonb</c> column whose shape is validated per
/// <see cref="BlockType"/> in FluentValidation (app layer), not the DB layer — the same
/// pattern used by <see cref="QuizQuestion.Options"/> and <see cref="QuizQuestion.CorrectAnswer"/>.
/// </summary>
public class ContentBlock : FullAuditedEntity
{
    /// <summary>FK to the owning <see cref="Lesson"/>. Never null.</summary>
    public int LessonId { get; set; }

    /// <summary>Navigation to the owning lesson (lazy-load or explicit Include).</summary>
    public Lesson Lesson { get; set; } = null!;

    /// <summary>
    /// Discriminator for the block's content shape. Stored as int.
    /// Determines which payload schema the app layer validates and the FE renderer uses.
    /// </summary>
    public ContentBlockType BlockType { get; set; }

    /// <summary>
    /// Serialized block content (jsonb). Shape varies per <see cref="BlockType"/>:
    /// <list type="bullet">
    ///   <item><term>Text</term><description>{ "markdown": "..." }</description></item>
    ///   <item><term>Image</term><description>{ "url": "...", "altText": "..." }</description></item>
    ///   <item><term>Video</term><description>{ "url": "...", "caption": "..." }</description></item>
    ///   <item><term>Callout</term><description>{ "variant": "info|warning|tip", "markdown": "..." }</description></item>
    /// </list>
    /// Per-type shape enforced by FluentValidation in the app layer only.
    /// </summary>
    public string Payload { get; set; } = null!;

    /// <summary>
    /// Display order within the owning lesson (0-based or 1-based — convention TBD by feature agent).
    /// Composite index <c>(LessonId, SequenceOrder)</c> is defined in <c>ContentBlockConfig</c>.
    /// </summary>
    public int SequenceOrder { get; set; }

    /// <summary>
    /// P7-02: Admin-controlled active/inactive toggle. Inactive blocks are excluded from
    /// student-facing reads but are NOT deleted (use <c>IsDeleted</c> for soft-delete).
    /// Default <c>true</c>. Mirrors Subject/Unit/Lesson pattern.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
