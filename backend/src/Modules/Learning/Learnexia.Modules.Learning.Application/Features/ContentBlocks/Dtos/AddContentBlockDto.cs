using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Features.ContentBlocks.Dtos;

/// <summary>
/// Input shape for adding a content block to a lesson.
/// IsActive and IsDeleted are intentionally absent — new blocks default IsActive=true.
/// SequenceOrder is absent — append-semantics: handler places the block at the end of the
/// lesson's existing sequence (max SequenceOrder + 1); explicit Reorder endpoint controls order.
/// </summary>
public record AddContentBlockDto
{
    public int LessonId { get; init; }
    public ContentBlockType BlockType { get; init; }

    /// <summary>
    /// JSON payload string. Per-type shape validated by <c>ContentBlockPayloadValidator</c>.
    /// Text:    { "markdown": "..." }
    /// Image:   { "url": "...", "altText": "..." }
    /// Video:   { "url": "...", "caption": "..." }
    /// Callout: { "variant": "info|warning|tip", "markdown": "..." }
    /// </summary>
    public string Payload { get; init; } = null!;
}
